using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Baseera.Domain.Resources;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

public sealed class ResourceReadinessIntegrationTests(BaseeraApiFactory factory) : IClassFixture<BaseeraApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [IntegrationConnectionFact]
    public async Task Facility_resource_summary_uses_seeded_assets_and_requirements()
    {
        await factory.SeedUserAsync(
            "resources-facility-director",
            "مدير موارد",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("resources-facility-director");

        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/summary");

        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<ResourceSummaryResponse>(JsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(6, summary!.TotalRegistered);
        Assert.Equal(17, summary.Required);
        Assert.True(summary.Gap > 0);
        Assert.True(summary.MissionCriticalUnavailable > 0);
        Assert.Contains(summary.Warnings, warning => warning.Contains("موارد حرجة", StringComparison.Ordinal));
    }

    [IntegrationConnectionFact]
    public async Task Resource_summary_requires_resource_permission()
    {
        await factory.SeedUserAsync(
            "resources-no-permission",
            "بلا موارد",
            [RoleCodes.FormRespondent],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("resources-no-permission");

        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Resource_assets_return_not_found_outside_facility_scope()
    {
        await factory.SeedUserAsync(
            "resources-out-scope",
            "خارج الموارد",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("resources-out-scope");

        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityB1}/resources/summary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Facility_workspace_contains_resource_widget_when_domain_permission_exists()
    {
        await factory.SeedUserAsync(
            "resources-workspace",
            "مساحة موارد",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("resources-workspace");

        var response = await client.GetAsync($"/api/v1/workspaces/facility-operations?level=1&facilityId={SeedIds.FacilityA1}");

        response.EnsureSuccessStatusCode();
        var shell = await response.Content.ReadFromJsonAsync<WorkspaceShellResponse>(JsonOptions);
        Assert.NotNull(shell);
        Assert.Contains(shell!.WidgetDefinitions, widget => widget.Key == "facility.resources");
        Assert.Contains(shell.Widgets, widget => widget.WidgetKey == "facility.resources");
    }

    [IntegrationConnectionFact]
    public async Task Created_resource_is_facility_scoped_and_audited()
    {
        await factory.SeedUserAsync(
            "resources-create",
            "إنشاء مورد",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("resources-create");
        var body = new
        {
            resourceType = 1,
            assetCode = $"COM-TST-{Guid.NewGuid():N}"[..16],
            displayName = "جهاز اتصال اختبار",
            ownershipOrganizationId = SeedIds.Organization,
            currentStatus = 0,
            condition = 1,
            criticality = 2,
            sourceType = 0,
            sourceReference = "integration-test"
        };

        var response = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/assets", body);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        Assert.NotNull(result);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var asset = await db.ResourceAssets.AsNoTracking().FirstAsync(a => a.Id == result!.Id);
        Assert.Equal(SeedIds.FacilityA1, asset.OperationalFacilityId);
        Assert.Equal(SeedIds.Organization, asset.OwnershipOrganizationId);
        Assert.True(await db.ResourceStatusEvents.AnyAsync(e => e.ResourceAssetId == asset.Id));
        Assert.True(await db.ResourcePlacements.AnyAsync(p => p.ResourceAssetId == asset.Id && p.EffectiveToUtc == null));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "ResourceCreated" && a.EntityId == asset.Id.ToString()));
    }

    [IntegrationConnectionFact]
    public async Task List_and_get_enforce_type_permissions()
    {
        const string subject = "resources-type-limited";
        await SeedCustomRoleUserAsync(
            subject,
            "صلاحيات نوع محدودة",
            [
                PermissionCodes.ResourcesViewAssets,
                PermissionCodes.ResourcesViewCommunicationDevices
            ],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient(subject);

        var listResponse = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/assets");
        listResponse.EnsureSuccessStatusCode();
        var assets = await listResponse.Content.ReadFromJsonAsync<List<AssetListItemResponse>>(JsonOptions);
        Assert.NotNull(assets);
        Assert.NotEmpty(assets!);
        Assert.Contains(assets, asset => asset.ResourceType == ResourceType.CommunicationDevice);
        Assert.DoesNotContain(assets, asset => asset.ResourceType == ResourceType.Vehicle);
        Assert.All(assets, asset => Assert.Equal(ResourceType.CommunicationDevice, asset.ResourceType));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            Assert.True(await db.ResourceAssets.AnyAsync(a =>
                a.OperationalFacilityId == SeedIds.FacilityA1 && a.ResourceType == ResourceType.Vehicle));
            Assert.True(await db.ResourceAssets.AnyAsync(a =>
                a.OperationalFacilityId == SeedIds.FacilityA1 && a.ResourceType == ResourceType.CommunicationDevice));
        }

        var vehicleFilter = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/assets?resourceType=0");
        Assert.Equal(HttpStatusCode.Forbidden, vehicleFilter.StatusCode);

        Guid vehicleId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            vehicleId = await db.ResourceAssets
                .Where(a => a.OperationalFacilityId == SeedIds.FacilityA1 && a.ResourceType == ResourceType.Vehicle)
                .Select(a => a.Id)
                .FirstAsync();
        }

        var getVehicle = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/assets/{vehicleId}");
        Assert.Equal(HttpStatusCode.Forbidden, getVehicle.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Get_asset_returns_404_for_missing_and_out_of_scope()
    {
        await factory.SeedUserAsync(
            "resources-get-404",
            "جلب مورد",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("resources-get-404");

        var missing = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/assets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        Guid foreignAssetId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var foreign = new ResourceAsset
            {
                OrganizationId = SeedIds.Organization,
                ResourceType = ResourceType.CommunicationDevice,
                AssetCode = $"COM-B1-{Guid.NewGuid():N}"[..16],
                DisplayName = "خارج النطاق",
                OwnershipOrganizationId = SeedIds.Organization,
                OperationalFacilityId = SeedIds.FacilityB1,
                CurrentStatus = ResourceStatus.Available,
                Condition = ResourceCondition.Good,
                Criticality = ResourceCriticality.Medium,
                SourceType = ResourceSourceType.Manual
            };
            db.ResourceAssets.Add(foreign);
            await db.SaveChangesAsync();
            foreignAssetId = foreign.Id;
        }

        var outOfScope = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/assets/{foreignAssetId}");
        Assert.Equal(HttpStatusCode.NotFound, outOfScope.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Duplicate_asset_code_across_facilities_same_org_returns_conflict()
    {
        await factory.SeedUserAsync(
            "resources-dup-code",
            "تكرار كود",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1),
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA2));
        var client = factory.CreateAuthenticatedClient("resources-dup-code");
        var code = $"DUP-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var first = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/assets", new
        {
            resourceType = 1,
            assetCode = code,
            displayName = "أول",
            ownershipOrganizationId = SeedIds.Organization,
            currentStatus = 0,
            condition = 1,
            criticality = 1,
            sourceType = 0
        });
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA2}/resources/assets", new
        {
            resourceType = 1,
            assetCode = code.ToLowerInvariant(),
            displayName = "ثاني",
            ownershipOrganizationId = SeedIds.Organization,
            currentStatus = 0,
            condition = 1,
            criticality = 1,
            sourceType = 0
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Import_confirm_is_idempotent()
    {
        await factory.SeedUserAsync(
            "resources-import-idempotent",
            "استيراد",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("resources-import-idempotent");
        var code = $"IMP-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var body = new
        {
            sourceSystem = "integration",
            sourceReference = $"ref-{Guid.NewGuid():N}",
            fileHash = $"hash-{Guid.NewGuid():N}",
            rows = new[]
            {
                new
                {
                    resourceType = 1,
                    assetCode = code,
                    displayName = "مستورد",
                    currentStatus = 0,
                    condition = 1,
                    criticality = 1
                }
            }
        };

        var first = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/import/confirm", body);
        first.EnsureSuccessStatusCode();
        var firstResult = await first.Content.ReadFromJsonAsync<ImportResultResponse>(JsonOptions);
        var second = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/import/confirm", body);
        second.EnsureSuccessStatusCode();
        var secondResult = await second.Content.ReadFromJsonAsync<ImportResultResponse>(JsonOptions);

        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(1, firstResult!.AppliedRows);
        Assert.Equal(firstResult.AppliedRows, secondResult!.AppliedRows);
        Assert.Equal(firstResult.ValidRows, secondResult.ValidRows);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        Assert.Equal(1, await db.ResourceAssets.CountAsync(a => a.AssetCode == code));
        Assert.Equal(1, await db.ResourceImportBatches.CountAsync(b =>
            b.FacilityId == SeedIds.FacilityA1
            && b.SourceSystem == body.sourceSystem
            && b.SourceReference == body.sourceReference
            && b.FileHash == body.fileHash));
    }

    [IntegrationConnectionFact]
    public async Task Concurrent_maintenance_work_orders_receive_unique_numbers()
    {
        await factory.SeedUserAsync(
            "resources-mwo-race",
            "أوامر صيانة",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var clientA = factory.CreateAuthenticatedClient("resources-mwo-race");
        var clientB = factory.CreateAuthenticatedClient("resources-mwo-race");

        Guid assetId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            assetId = await db.ResourceAssets
                .Where(a => a.OperationalFacilityId == SeedIds.FacilityA1)
                .Select(a => a.Id)
                .FirstAsync();
        }

        var body = new
        {
            resourceAssetId = assetId,
            maintenanceType = 1,
            priority = 2,
            reportedAtUtc = DateTimeOffset.UtcNow,
            problemDescription = "عطل متزامن",
            partsRequired = false
        };

        var responses = await Task.WhenAll(
            clientA.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/maintenance", body),
            clientB.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/maintenance", body));
        Assert.All(responses, response => response.EnsureSuccessStatusCode());
        var ids = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions)));

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var numbers = await verifyDb.MaintenanceWorkOrders
            .Where(o => ids.Select(x => x!.Id).Contains(o.Id))
            .Select(o => o.WorkOrderNumber)
            .ToListAsync();
        Assert.Equal(2, numbers.Distinct(StringComparer.Ordinal).Count());
    }

    [IntegrationConnectionFact]
    public async Task Soft_deleted_asset_hides_dependents()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var asset = new ResourceAsset
        {
            OrganizationId = SeedIds.Organization,
            ResourceType = ResourceType.FacilityAsset,
            AssetCode = $"SOFT-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            DisplayName = "محذوف",
            OwnershipOrganizationId = SeedIds.Organization,
            OperationalFacilityId = SeedIds.FacilityA1,
            CurrentStatus = ResourceStatus.Available,
            Condition = ResourceCondition.Good,
            Criticality = ResourceCriticality.Low,
            SourceType = ResourceSourceType.Manual
        };
        db.ResourceAssets.Add(asset);
        db.FacilityAssetProfiles.Add(new FacilityAssetProfile
        {
            ResourceAssetId = asset.Id,
            AssetCategory = FacilityAssetCategory.Other,
            FixedAsset = true
        });
        db.ResourceStatusEvents.Add(new ResourceStatusEvent
        {
            ResourceAssetId = asset.Id,
            NewStatus = ResourceStatus.Available,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Reason = "seed",
            RecordedAtUtc = DateTimeOffset.UtcNow
        });
        db.MaintenanceWorkOrders.Add(new MaintenanceWorkOrder
        {
            OrganizationId = SeedIds.Organization,
            ResourceAssetId = asset.Id,
            WorkOrderNumber = $"MWO-SOFT-{Guid.NewGuid():N}"[..16],
            MaintenanceType = MaintenanceType.Corrective,
            Priority = MaintenancePriority.Low,
            Status = MaintenanceStatus.Open,
            ReportedAtUtc = DateTimeOffset.UtcNow,
            ProblemDescription = "صيانة"
        });
        await db.SaveChangesAsync();

        asset.IsDeleted = true;
        asset.DeletedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        Assert.False(await db.ResourceAssets.AnyAsync(a => a.Id == asset.Id));
        Assert.False(await db.FacilityAssetProfiles.AnyAsync(p => p.ResourceAssetId == asset.Id));
        Assert.False(await db.ResourceStatusEvents.AnyAsync(e => e.ResourceAssetId == asset.Id));
        Assert.False(await db.MaintenanceWorkOrders.AnyAsync(o => o.ResourceAssetId == asset.Id));
        Assert.True(await db.ResourceAssets.IgnoreQueryFilters().AnyAsync(a => a.Id == asset.Id && a.IsDeleted));
    }

    [IntegrationConnectionFact]
    public async Task Resource_status_events_are_append_only()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var statusEvent = await db.ResourceStatusEvents.FirstAsync();
        statusEvent.Reason = "mutated";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        db.ChangeTracker.Clear();
        var reload = await db.ResourceStatusEvents.FirstAsync(e => e.Id == statusEvent.Id);
        db.Remove(reload);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [IntegrationConnectionFact]
    public async Task Unsupported_import_batch_status_is_rejected_by_database()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        db.ResourceImportBatches.Add(new ResourceImportBatch
        {
            FacilityId = SeedIds.FacilityA1,
            SourceSystem = "test",
            SourceReference = $"unsupported-{Guid.NewGuid():N}",
            FileHash = Guid.NewGuid().ToString("N"),
            Status = "Pending",
            TotalRows = 0,
            ValidRows = 0,
            RejectedRows = 0,
            DuplicateRows = 0,
            AppliedRows = 0
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("CK_ResourceImportBatches", ex.InnerException?.Message + ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [IntegrationConnectionFact]
    public async Task Cross_facility_unit_is_rejected_on_create_place_and_requirement()
    {
        await factory.SeedUserAsync(
            "resources-cross-unit",
            "وحدة خاطئة",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("resources-cross-unit");

        var create = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/assets", new
        {
            resourceType = 1,
            assetCode = $"XUNIT-{Guid.NewGuid():N}"[..14].ToUpperInvariant(),
            displayName = "وحدة خاطئة",
            ownershipOrganizationId = SeedIds.Organization,
            operationalFacilityUnitId = SeedIds.FacilityA1UnitNorth,
            currentStatus = 0,
            condition = 1,
            criticality = 1,
            sourceType = 0
        });
        // Create a foreign unit under Facility B1 and try to use it against A1
        Guid foreignUnitId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            foreignUnitId = Guid.NewGuid();
            db.FacilityUnits.Add(new FacilityUnit
            {
                Id = foreignUnitId,
                FacilityId = SeedIds.FacilityB1,
                Code = $"B1-{Guid.NewGuid():N}"[..8],
                NameAr = "وحدة خارجية"
            });
            await db.SaveChangesAsync();
        }

        var badCreate = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/assets", new
        {
            resourceType = 1,
            assetCode = $"XUNIT-{Guid.NewGuid():N}"[..14].ToUpperInvariant(),
            displayName = "وحدة خاطئة",
            ownershipOrganizationId = SeedIds.Organization,
            operationalFacilityUnitId = foreignUnitId,
            currentStatus = 0,
            condition = 1,
            criticality = 1,
            sourceType = 0
        });
        Assert.Equal(HttpStatusCode.NotFound, badCreate.StatusCode);

        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);

        var place = await client.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/resources/assets/{created!.Id}/placements",
            new
            {
                ownershipOrganizationId = SeedIds.Organization,
                operationalFacilityUnitId = foreignUnitId,
                effectiveFromUtc = DateTimeOffset.UtcNow,
                assignmentType = 0
            });
        Assert.Equal(HttpStatusCode.NotFound, place.StatusCode);

        var requirement = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/requirements", new
        {
            facilityUnitId = foreignUnitId,
            resourceType = 1,
            resourceCategory = "radios",
            requiredQuantity = 2,
            minimumOperationalQuantity = 1,
            effectiveFromUtc = DateTimeOffset.UtcNow,
            sourceReference = "cross-unit"
        });
        Assert.Equal(HttpStatusCode.NotFound, requirement.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Duplicate_active_requirement_rejected_while_non_overlapping_allowed()
    {
        await factory.SeedUserAsync(
            "resources-req-overlap",
            "احتياج",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("resources-req-overlap");
        var category = $"cat-{Guid.NewGuid():N}"[..12];
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var first = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/requirements", new
        {
            resourceType = 1,
            resourceCategory = category,
            requiredQuantity = 4,
            minimumOperationalQuantity = 2,
            effectiveFromUtc = from,
            effectiveToUtc = from.AddMonths(3),
            sourceReference = "req-1"
        });
        first.EnsureSuccessStatusCode();

        var overlap = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/requirements", new
        {
            resourceType = 1,
            resourceCategory = category,
            requiredQuantity = 5,
            minimumOperationalQuantity = 2,
            effectiveFromUtc = from.AddMonths(1),
            effectiveToUtc = from.AddMonths(4),
            sourceReference = "req-2"
        });
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);

        var nonOverlap = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/requirements", new
        {
            resourceType = 1,
            resourceCategory = category,
            requiredQuantity = 3,
            minimumOperationalQuantity = 1,
            effectiveFromUtc = from.AddMonths(3),
            effectiveToUtc = from.AddMonths(6),
            sourceReference = "req-3"
        });
        nonOverlap.EnsureSuccessStatusCode();
    }

    private async Task SeedCustomRoleUserAsync(
        string subject,
        string displayName,
        string[] permissions,
        params (ScopeType ScopeType, Guid? RegionId, Guid? FacilityId)[] scopes)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var roleCode = $"Custom_{subject}";
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == roleCode);
        if (role is null)
        {
            role = new Role
            {
                Code = roleCode,
                NameAr = displayName,
                IsSystem = false
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }

        foreach (var code in permissions)
        {
            var permission = await db.Permissions.FirstAsync(p => p.Code == code);
            if (!await db.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id))
            {
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            }
        }

        await db.SaveChangesAsync();
        await factory.SeedUserAsync(subject, displayName, [roleCode], scopes);
    }

    private sealed record ResourceSummaryResponse(
        int TotalRegistered,
        int Required,
        int Gap,
        int MissionCriticalUnavailable,
        IReadOnlyList<string> Warnings);

    private sealed record WorkspaceShellResponse(
        IReadOnlyList<WidgetDefinitionResponse> WidgetDefinitions,
        IReadOnlyList<WidgetEnvelopeResponse> Widgets);

    private sealed record WidgetDefinitionResponse(string Key);

    private sealed record WidgetEnvelopeResponse(string WidgetKey);

    private sealed record CreateResponse(Guid Id);

    private sealed record AssetListItemResponse(ResourceType ResourceType, string AssetCode);

    private sealed record ImportResultResponse(int TotalRows, int ValidRows, int RejectedRows, int DuplicateRows, int AppliedRows);
}
