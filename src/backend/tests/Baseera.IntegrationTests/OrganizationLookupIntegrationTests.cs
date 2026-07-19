using System.Net;
using System.Net.Http.Json;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

/// <summary>
/// Minimal cascading-form lookups added to close Phase B.1 gaps: facility units (scoped,
/// soft-delete filtered) and departments (organization-wide, soft-delete filtered).
/// </summary>
public sealed class OrganizationLookupIntegrationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;

    public OrganizationLookupIntegrationTests(BaseeraApiFactory factory) => _factory = factory;

    [IntegrationConnectionFact]
    public async Task Facility_units_endpoint_requires_facility_id()
    {
        await _factory.SeedUserAsync("orglookup-nofac", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var client = _factory.CreateAuthenticatedClient("orglookup-nofac");

        var response = await client.GetAsync("/api/v1/facility-units");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Facility_units_endpoint_lists_units_for_accessible_facility_and_hides_soft_deleted()
    {
        await _factory.SeedUserAsync("orglookup-fac-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("orglookup-fac-admin");

        Guid activeUnitId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var active = new FacilityUnit { FacilityId = SeedIds.FacilityA1, Code = "UNIT-A", NameAr = "وحدة نشطة" };
            var deleted = new FacilityUnit
            {
                FacilityId = SeedIds.FacilityA1,
                Code = "UNIT-B",
                NameAr = "وحدة مؤرشفة",
                IsDeleted = true,
                DeletedAtUtc = DateTimeOffset.UtcNow
            };
            db.FacilityUnits.AddRange(active, deleted);
            await db.SaveChangesAsync();
            activeUnitId = active.Id;
        }

        var response = await admin.GetFromJsonAsync<PagedEnvelope<FacilityUnitItem>>(
            $"/api/v1/facility-units?facilityId={SeedIds.FacilityA1}&page=1&pageSize=50");
        Assert.NotNull(response);
        Assert.Contains(response!.Items, u => u.Id == activeUnitId);
        Assert.DoesNotContain(response.Items, u => u.NameAr == "وحدة مؤرشفة");
    }

    [IntegrationConnectionFact]
    public async Task Facility_units_endpoint_is_scope_checked_like_facilities()
    {
        await _factory.SeedUserAsync("orglookup-fac-admin2", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("orglookup-outsider", "خارج النطاق", [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            db.FacilityUnits.Add(new FacilityUnit { FacilityId = SeedIds.FacilityB1, Code = "UNIT-B1", NameAr = "وحدة سجن ب1" });
            await db.SaveChangesAsync();
        }

        var outsider = _factory.CreateAuthenticatedClient("orglookup-outsider");
        var response = await outsider.GetAsync($"/api/v1/facility-units?facilityId={SeedIds.FacilityB1}&page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Departments_endpoint_lists_non_deleted_departments_org_wide()
    {
        await _factory.SeedUserAsync("orglookup-dept-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("orglookup-dept-admin");

        Guid activeDeptId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var active = new Department { OrganizationId = SeedIds.Organization, Code = "DPT-ACTIVE", NameAr = "إدارة نشطة" };
            var deleted = new Department
            {
                OrganizationId = SeedIds.Organization,
                Code = "DPT-DELETED",
                NameAr = "إدارة مؤرشفة",
                IsDeleted = true,
                DeletedAtUtc = DateTimeOffset.UtcNow
            };
            db.Departments.AddRange(active, deleted);
            await db.SaveChangesAsync();
            activeDeptId = active.Id;
        }

        var response = await admin.GetFromJsonAsync<PagedEnvelope<DepartmentItem>>("/api/v1/departments?page=1&pageSize=50");
        Assert.NotNull(response);
        Assert.Contains(response!.Items, d => d.Id == activeDeptId);
        Assert.DoesNotContain(response.Items, d => d.NameAr == "إدارة مؤرشفة");
    }

    [IntegrationConnectionFact]
    public async Task Departments_endpoint_requires_organization_view_permission()
    {
        await _factory.SeedUserAsync("orglookup-dept-noperm", "بلا صلاحية", [], (ScopeType.Global, null, null));
        var client = _factory.CreateAuthenticatedClient("orglookup-dept-noperm");

        var response = await client.GetAsync("/api/v1/departments?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

internal sealed record FacilityUnitItem(Guid Id, Guid FacilityId, Guid? ParentUnitId, string Code, string NameAr, bool IsActive);
internal sealed record DepartmentItem(Guid Id, Guid OrganizationId, Guid? ParentDepartmentId, string Code, string NameAr, bool IsActive);
