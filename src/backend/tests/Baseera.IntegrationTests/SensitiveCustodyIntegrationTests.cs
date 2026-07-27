namespace Baseera.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Application.SensitiveCustody;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.SensitiveCustody;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(OperationsIntegrationCollection.Name)]
public sealed class SensitiveCustodyIntegrationTests(OperationsIntegrationFixture fixture)
    : IntegrationTestBase<OperationsIntegrationFixture>(fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private BaseeraApiFactory factory => Factory;

    [IntegrationConnectionFact]
    public async Task Summary_requires_sensitive_custody_permission()
    {
        await factory.SeedUserAsync(
            "sensitive-no-permission",
            "بدون عهد",
            [RoleCodes.FormRespondent],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("sensitive-no-permission");

        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Summary_returns_not_found_outside_facility_scope()
    {
        await factory.SeedUserAsync(
            "sensitive-out-scope",
            "تسليح أ",
            [RoleCodes.ArmamentOfficer],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("sensitive-out-scope");

        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityB1}/sensitive-custody/summary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Weapon_create_stores_protected_serial_and_redacts_view_without_serial_permission()
    {
        var (weaponTypeId, armoryId) = await SeedSensitiveReferenceAsync();
        await factory.SeedUserAsync(
            "sensitive-create",
            "ضابط تسليح",
            [RoleCodes.ArmamentOfficer],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await factory.SeedUserAsync(
            "sensitive-viewer",
            "مدير سجن",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var createClient = factory.CreateAuthenticatedClient("sensitive-create");
        var serial = $"SN-{Guid.NewGuid():N}";

        var create = await createClient.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/weapons", new
        {
            weaponTypeId,
            internalAssetCode = $"WPN-{Guid.NewGuid():N}"[..16],
            serialNumber = serial,
            caliber = "9mm",
            currentArmoryLocationId = armoryId,
            currentStatus = WeaponStatus.InArmory,
            condition = WeaponCondition.Serviceable,
            criticality = WeaponCriticality.High,
            sourceReference = "integration"
        });

        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        Assert.NotNull(created);
        string storedCiphertext;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<Baseera.Application.Abstractions.ISensitiveValueProtector>();
            var stored = await db.WeaponAssets.AsNoTracking().SingleAsync(w => w.Id == created!.Id);
            storedCiphertext = stored.SerialNumberEncrypted;
            Assert.DoesNotContain(serial, stored.SerialNumberEncrypted, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(serial, stored.SerialNumberHash, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                SensitiveSerialProtection.NormalizeSerial(serial),
                protector.Unprotect(stored.SerialNumberEncrypted));
            Assert.Equal(SensitiveSerialProtection.Hash(serial), stored.SerialNumberHash);
        }

        var viewer = factory.CreateAuthenticatedClient("sensitive-viewer");
        var list = await viewer.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/weapons");

        list.EnsureSuccessStatusCode();
        var body = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain(serial, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(storedCiphertext, body, StringComparison.OrdinalIgnoreCase);
        var rows = JsonSerializer.Deserialize<IReadOnlyList<WeaponListItemResponse>>(body, JsonOptions);
        Assert.NotNull(rows);
        var row = Assert.Single(rows!, item => item.Id == created!.Id);
        Assert.Equal(SensitiveSerialProtection.RedactedMask, row.MaskedSerial);
        Assert.Null(row.FullSerial);

        var officerList = await createClient.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/weapons?search={Uri.EscapeDataString(serial)}");
        officerList.EnsureSuccessStatusCode();
        var officerRows = await officerList.Content.ReadFromJsonAsync<IReadOnlyList<WeaponListItemResponse>>(JsonOptions);
        var officerRow = Assert.Single(officerRows!, item => item.Id == created!.Id);
        Assert.Equal(SensitiveSerialProtection.NormalizeSerial(serial), officerRow.FullSerial);
        Assert.Equal(SensitiveSerialProtection.MaskPlaintext(SensitiveSerialProtection.NormalizeSerial(serial)), officerRow.MaskedSerial);
    }

    [IntegrationConnectionFact]
    public async Task Custody_lifecycle_completes_and_reverse_restores_previous_state()
    {
        var (weaponTypeId, armoryId, armoryBId) = await SeedSensitiveReferencePairAsync();
        await factory.SeedUserAsync(
            "custody-issuer",
            "ضابط تسليح",
            [RoleCodes.ArmamentOfficer],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await factory.SeedUserAsync(
            "custody-approver",
            "مدير سجن",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var issuer = factory.CreateAuthenticatedClient("custody-issuer");
        var approver = factory.CreateAuthenticatedClient("custody-approver");

        var createWeapon = await issuer.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/weapons", new
        {
            weaponTypeId,
            internalAssetCode = $"LIF-{Guid.NewGuid():N}"[..16],
            serialNumber = $"SN-{Guid.NewGuid():N}",
            caliber = "9mm",
            currentArmoryLocationId = armoryId,
            currentStatus = WeaponStatus.InArmory,
            condition = WeaponCondition.Serviceable,
            criticality = WeaponCriticality.High,
            sourceReference = "lifecycle"
        });
        createWeapon.EnsureSuccessStatusCode();
        var weapon = await createWeapon.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        Assert.NotNull(weapon);

        var createTx = await issuer.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/transactions", new
        {
            weaponAssetId = weapon!.Id,
            transactionType = CustodyTransactionType.TransferBetweenArmories,
            toCustodyType = CustodyLocationType.Armory,
            toCustodyReferenceId = armoryBId,
            purposeCode = "TRANSFER",
            reason = "نقل اختبار دورة العهدة"
        });
        createTx.EnsureSuccessStatusCode();
        var createdTx = await createTx.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        Assert.NotNull(createdTx);

        await TransitionAsync(approver, createdTx!.Id, "approve");
        await TransitionAsync(issuer, createdTx.Id, "handover");
        await TransitionAsync(issuer, createdTx.Id, "receive");
        await TransitionAsync(issuer, createdTx.Id, "complete");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var transaction = await db.CustodyTransactions.AsNoTracking().SingleAsync(t => t.Id == createdTx.Id);
            var storedWeapon = await db.WeaponAssets.AsNoTracking().SingleAsync(w => w.Id == weapon.Id);
            Assert.Equal(CustodyTransactionStatus.Completed, transaction.Status);
            Assert.True(transaction.IsCurrent);
            Assert.Equal(weapon.Id, storedWeapon.Id);
            Assert.Equal(WeaponStatus.InArmory, storedWeapon.CurrentStatus);
            Assert.Equal(createdTx.Id, storedWeapon.CurrentCustodyTransactionId);
            Assert.Equal(CustodyLocationType.Armory, storedWeapon.CurrentCustodyLocationType);
            Assert.Equal(armoryBId, storedWeapon.CurrentArmoryLocationId);
            Assert.Null(storedWeapon.CurrentFacilityUnitId);
            Assert.Equal(armoryId, transaction.FromCustodyReferenceId);
            Assert.True(transaction.PreviousTransactionId is null);
        }

        var reverseForbidden = await issuer.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/transactions/{createdTx.Id}/reverse",
            new { rowVersion = await GetTransactionRowVersionAsync(issuer, createdTx.Id), reason = "عكس غير مصرح" });
        // issuer has Approve? ArmamentOfficer does NOT have ApproveTransactions - should 403
        Assert.Equal(HttpStatusCode.Forbidden, reverseForbidden.StatusCode);

        await TransitionAsync(approver, createdTx.Id, "reverse");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var transaction = await db.CustodyTransactions.AsNoTracking().SingleAsync(t => t.Id == createdTx.Id);
            var storedWeapon = await db.WeaponAssets.AsNoTracking().SingleAsync(w => w.Id == weapon.Id);
            Assert.Equal(CustodyTransactionStatus.Reversed, transaction.Status);
            Assert.False(transaction.IsCurrent);
            Assert.Equal(WeaponStatus.InArmory, storedWeapon.CurrentStatus);
            Assert.Null(storedWeapon.CurrentCustodyTransactionId);
            Assert.Equal(CustodyLocationType.Armory, storedWeapon.CurrentCustodyLocationType);
            Assert.Equal(armoryId, storedWeapon.CurrentArmoryLocationId);
            Assert.Equal(0, await db.CustodyTransactions.CountAsync(t => t.WeaponAssetId == weapon.Id && t.IsCurrent && !t.IsDeleted));
        }
    }

    [IntegrationConnectionFact]
    public async Task Custody_complete_requires_receive_permission_and_rejects_stale_rowversion()
    {
        var (weaponTypeId, armoryId) = await SeedSensitiveReferenceAsync();
        await factory.SeedUserAsync(
            "custody-complete-officer",
            "ضابط تسليح",
            [RoleCodes.ArmamentOfficer],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await factory.SeedUserAsync(
            "custody-complete-director",
            "مدير سجن",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var officer = factory.CreateAuthenticatedClient("custody-complete-officer");
        var director = factory.CreateAuthenticatedClient("custody-complete-director");

        var createWeapon = await officer.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/weapons", new
        {
            weaponTypeId,
            internalAssetCode = $"CMP-{Guid.NewGuid():N}"[..16],
            serialNumber = $"SN-{Guid.NewGuid():N}",
            caliber = "9mm",
            currentArmoryLocationId = armoryId,
            currentStatus = WeaponStatus.InArmory,
            condition = WeaponCondition.Serviceable,
            criticality = WeaponCriticality.High
        });
        createWeapon.EnsureSuccessStatusCode();
        var weapon = await createWeapon.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);

        var createTx = await officer.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/transactions", new
        {
            weaponAssetId = weapon!.Id,
            transactionType = CustodyTransactionType.ReturnToArmory,
            toCustodyType = CustodyLocationType.Armory,
            toCustodyReferenceId = armoryId,
            purposeCode = "RETURN",
            reason = "إرجاع مباشر"
        });
        createTx.EnsureSuccessStatusCode();
        var createdTx = await createTx.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        var rowVersion = await GetTransactionRowVersionAsync(officer, createdTx!.Id);

        var forbidden = await director.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/transactions/{createdTx.Id}/complete",
            new { rowVersion, reason = "إكمال بدون صلاحية" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var stale = await officer.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/transactions/{createdTx.Id}/complete",
            new { rowVersion = Convert.ToBase64String(Guid.NewGuid().ToByteArray()), reason = "إكمال بنسخة قديمة" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var outOfScope = await officer.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityB1}/sensitive-custody/transactions/{createdTx.Id}/complete",
            new { rowVersion, reason = "خارج النطاق" });
        Assert.Equal(HttpStatusCode.NotFound, outOfScope.StatusCode);

        var beforeComplete = await director.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/transactions/{createdTx.Id}/reverse",
            new { rowVersion = await GetTransactionRowVersionAsync(director, createdTx.Id), reason = "عكس قبل الإكمال" });
        Assert.Equal(HttpStatusCode.Conflict, beforeComplete.StatusCode);

        var complete = await officer.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/transactions/{createdTx.Id}/complete",
            new { rowVersion = await GetTransactionRowVersionAsync(officer, createdTx.Id), reason = "إكمال مسموح" });
        Assert.Equal(HttpStatusCode.NoContent, complete.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Facility_workspace_contains_sensitive_custody_widget_without_raw_sensitive_fields()
    {
        var (weaponTypeId, armoryId) = await SeedSensitiveReferenceAsync();
        await factory.SeedUserAsync(
            "sensitive-workspace-create",
            "ضابط تسليح",
            [RoleCodes.ArmamentOfficer],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await factory.SeedUserAsync(
            "sensitive-workspace-view",
            "مدير سجن",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var serial = $"SN-{Guid.NewGuid():N}";
        var createClient = factory.CreateAuthenticatedClient("sensitive-workspace-create");
        var create = await createClient.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/weapons", new
        {
            weaponTypeId,
            internalAssetCode = $"WSP-{Guid.NewGuid():N}"[..16],
            serialNumber = serial,
            caliber = "9mm",
            currentArmoryLocationId = armoryId,
            currentStatus = WeaponStatus.Missing,
            condition = WeaponCondition.Serviceable,
            criticality = WeaponCriticality.MissionCritical,
            sourceReference = "workspace"
        });
        create.EnsureSuccessStatusCode();
        var client = factory.CreateAuthenticatedClient("sensitive-workspace-view");

        var response = await client.GetAsync($"/api/v1/workspaces/facility-operations?level=1&facilityId={SeedIds.FacilityA1}");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("facility.sensitive-custody", body, StringComparison.Ordinal);
        Assert.DoesNotContain(serial, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Armory-A1-Test", body, StringComparison.OrdinalIgnoreCase);
    }

    [IntegrationConnectionFact]
    public async Task Sensitive_audit_does_not_store_raw_serial()
    {
        var (weaponTypeId, armoryId) = await SeedSensitiveReferenceAsync();
        await factory.SeedUserAsync(
            "sensitive-audit",
            "ضابط تسليح",
            [RoleCodes.ArmamentOfficer],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("sensitive-audit");
        var serial = $"SN-{Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/weapons", new
        {
            weaponTypeId,
            internalAssetCode = $"AUD-{Guid.NewGuid():N}"[..16],
            serialNumber = serial,
            caliber = "9mm",
            currentArmoryLocationId = armoryId,
            currentStatus = WeaponStatus.InArmory,
            condition = WeaponCondition.Serviceable,
            criticality = WeaponCriticality.High,
            sourceReference = "audit"
        });

        response.EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var auditRows = await db.AuditLogs
            .AsNoTracking()
            .Where(log => log.Module == "SensitiveCustody")
            .Select(log => new { log.NewValuesJson, log.OldValuesJson, log.Reason })
            .ToListAsync();
        Assert.NotEmpty(auditRows);
        Assert.All(auditRows, row =>
        {
            Assert.DoesNotContain(serial, row.NewValuesJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(serial, row.OldValuesJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(serial, row.Reason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        });
    }

    private async Task<(Guid WeaponTypeId, Guid ArmoryId)> SeedSensitiveReferenceAsync()
    {
        var pair = await SeedSensitiveReferencePairAsync();
        return (pair.WeaponTypeId, pair.ArmoryAId);
    }

    private async Task<(Guid WeaponTypeId, Guid ArmoryAId, Guid ArmoryBId)> SeedSensitiveReferencePairAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var typeCode = $"WT-{Guid.NewGuid():N}"[..16];
        var weaponType = new WeaponTypeDefinition
        {
            OrganizationId = SeedIds.Organization,
            Code = typeCode,
            NameAr = "نوع اختبار حساس",
            Category = WeaponTypeCategory.Individual,
            Caliber = "9mm",
            IsIndividualWeapon = true,
            RequiresQualifiedCustodian = true,
            InspectionIntervalDays = 30,
            MinimumSafeCondition = WeaponCondition.Serviceable,
            IsSensitive = true,
            IsActive = true
        };
        var armoryA = new ArmoryLocation
        {
            OrganizationId = SeedIds.Organization,
            FacilityId = SeedIds.FacilityA1,
            Code = $"ARM-{Guid.NewGuid():N}"[..16],
            Name = "Armory-A1-Test",
            LocationClassification = "Sensitive",
            IsActive = true
        };
        var armoryB = new ArmoryLocation
        {
            OrganizationId = SeedIds.Organization,
            FacilityId = SeedIds.FacilityA1,
            Code = $"ARB-{Guid.NewGuid():N}"[..16],
            Name = "Armory-A1-B-Test",
            LocationClassification = "Sensitive",
            IsActive = true
        };
        db.WeaponTypeDefinitions.Add(weaponType);
        db.ArmoryLocations.Add(armoryA);
        db.ArmoryLocations.Add(armoryB);
        await db.SaveChangesAsync();
        return (weaponType.Id, armoryA.Id, armoryB.Id);
    }

    private async Task TransitionAsync(HttpClient client, Guid transactionId, string action)
    {
        var rowVersion = await GetTransactionRowVersionFromDbAsync(transactionId);
        var response = await client.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/transactions/{transactionId}/{action}",
            new { rowVersion, reason = $"transition-{action}" });
        if (response.StatusCode != HttpStatusCode.NoContent)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"{action} returned {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
    }

    private async Task<string> GetTransactionRowVersionAsync(HttpClient client, Guid transactionId)
    {
        var list = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/transactions?page=1&pageSize=100");
        list.EnsureSuccessStatusCode();
        var rows = await list.Content.ReadFromJsonAsync<IReadOnlyList<TransactionListItemResponse>>(JsonOptions);
        var row = Assert.Single(rows!, item => item.Id == transactionId);
        Assert.False(string.IsNullOrWhiteSpace(row.RowVersion));
        return row.RowVersion;
    }

    private async Task<string> GetTransactionRowVersionFromDbAsync(Guid transactionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var rowVersion = await db.CustodyTransactions
            .AsNoTracking()
            .Where(t => t.Id == transactionId)
            .Select(t => t.RowVersion)
            .SingleAsync();
        return Convert.ToBase64String(rowVersion);
    }

    private sealed record CreateResponse(Guid Id);

    private sealed record WeaponListItemResponse(Guid Id, string MaskedSerial, string? FullSerial);

    private sealed record TransactionListItemResponse(Guid Id, string RowVersion, CustodyTransactionStatus Status);
}
