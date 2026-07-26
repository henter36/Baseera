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
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var stored = await db.WeaponAssets.AsNoTracking().SingleAsync(w => w.Id == created!.Id);
            Assert.DoesNotContain(serial, stored.SerialNumberEncrypted, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(serial, stored.SerialNumberHash, StringComparison.OrdinalIgnoreCase);
        }

        var viewer = factory.CreateAuthenticatedClient("sensitive-viewer");
        var list = await viewer.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/sensitive-custody/weapons");

        list.EnsureSuccessStatusCode();
        var body = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain(serial, body, StringComparison.OrdinalIgnoreCase);
        var rows = JsonSerializer.Deserialize<IReadOnlyList<WeaponListItemResponse>>(body, JsonOptions);
        Assert.NotNull(rows);
        var row = Assert.Single(rows!, item => item.Id == created!.Id);
        Assert.StartsWith("***-", row.MaskedSerial, StringComparison.Ordinal);
        Assert.Null(row.FullSerial);
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
        var armory = new ArmoryLocation
        {
            OrganizationId = SeedIds.Organization,
            FacilityId = SeedIds.FacilityA1,
            Code = $"ARM-{Guid.NewGuid():N}"[..16],
            Name = "Armory-A1-Test",
            LocationClassification = "Sensitive",
            IsActive = true
        };
        db.WeaponTypeDefinitions.Add(weaponType);
        db.ArmoryLocations.Add(armory);
        await db.SaveChangesAsync();
        return (weaponType.Id, armory.Id);
    }

    private sealed record CreateResponse(Guid Id);

    private sealed record WeaponListItemResponse(Guid Id, string MaskedSerial, string? FullSerial);
}
