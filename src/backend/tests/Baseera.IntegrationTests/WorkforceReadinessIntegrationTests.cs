using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Workforce;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

public sealed class WorkforceReadinessIntegrationTests(BaseeraApiFactory factory) : IClassFixture<BaseeraApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [IntegrationConnectionFact]
    public async Task Facility_workforce_summary_requires_permission()
    {
        await factory.SeedUserAsync(
            "workforce-no-permission",
            "بلا قوى بشرية",
            [RoleCodes.FormRespondent],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-no-permission");

        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Workforce_summary_returns_not_found_outside_facility_scope()
    {
        await factory.SeedUserAsync(
            "workforce-out-scope",
            "خارج القوى",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-out-scope");

        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityB1}/workforce/summary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Facility_workforce_summary_redacts_member_names()
    {
        await factory.SeedUserAsync(
            "workforce-summary",
            "ملخص قوى",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-summary");

        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/summary");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("أحمد", json, StringComparison.Ordinal);
        Assert.DoesNotContain("القحطاني", json, StringComparison.Ordinal);
        var summary = await response.Content.ReadFromJsonAsync<WorkforceSummaryResponse>(JsonOptions);
        Assert.NotNull(summary);
        Assert.True(summary!.TotalMembers >= 4);
        Assert.True(summary.Required > 0);
    }

    [IntegrationConnectionFact]
    public async Task Facility_workspace_contains_workforce_widget_when_domain_permission_exists()
    {
        await factory.SeedUserAsync(
            "workforce-workspace",
            "مساحة قوى",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-workspace");

        var response = await client.GetAsync($"/api/v1/workspaces/facility-operations?level=1&facilityId={SeedIds.FacilityA1}");

        response.EnsureSuccessStatusCode();
        var shell = await response.Content.ReadFromJsonAsync<WorkspaceShellResponse>(JsonOptions);
        Assert.NotNull(shell);
        Assert.Contains(shell!.WidgetDefinitions, widget => widget.Key == "facility.workforce");
        Assert.Contains(shell.Widgets, widget => widget.WidgetKey == "facility.workforce");
    }

    [IntegrationConnectionFact]
    public async Task Created_workforce_member_is_facility_scoped_and_audited()
    {
        await factory.SeedUserAsync(
            "workforce-create",
            "إنشاء عضو",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-create");
        var body = new
        {
            displayName = "عضو اختبار",
            employeeNumber = $"WF-TST-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            jobTitle = "ضابط أمن",
            primarySpecialty = "أمن",
            employmentStatus = 0,
            isOperational = true,
            sourceType = 0
        };

        var response = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/members", body);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        Assert.NotNull(result);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var member = await db.WorkforceMembers.AsNoTracking().FirstAsync(m => m.Id == result!.Id);
        Assert.Equal(SeedIds.FacilityA1, member.CurrentOperationalFacilityId);
        Assert.Equal(EmploymentStatus.Active, member.EmploymentStatus);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "WorkforceMemberCreated" && a.EntityId == member.Id.ToString()));
    }

    [IntegrationConnectionFact]
    public async Task Import_confirm_is_idempotent()
    {
        await factory.SeedUserAsync(
            "workforce-import-idempotent",
            "استيراد قوى",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-import-idempotent");
        var number = $"WF-IMP-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var body = new
        {
            sourceSystem = "integration",
            sourceReference = $"wf-ref-{Guid.NewGuid():N}",
            fileHash = $"wf-hash-{Guid.NewGuid():N}",
            rows = new[]
            {
                new
                {
                    employeeNumber = number,
                    displayName = "مستورد",
                    jobTitle = "ضابط",
                    primarySpecialty = "أمن",
                    employmentStatus = 0,
                    isOperational = true
                }
            }
        };

        var preview = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/import/preview", body);
        preview.EnsureSuccessStatusCode();
        var first = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/import/confirm", body);
        first.EnsureSuccessStatusCode();
        var firstResult = await first.Content.ReadFromJsonAsync<ImportResultResponse>(JsonOptions);
        var second = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/import/confirm", body);
        second.EnsureSuccessStatusCode();
        var secondResult = await second.Content.ReadFromJsonAsync<ImportResultResponse>(JsonOptions);

        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(1, firstResult!.AppliedRows);
        Assert.Equal(firstResult.AppliedRows, secondResult!.AppliedRows);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        Assert.Equal(1, await db.WorkforceMembers.CountAsync(m => m.EmployeeNumber == number));
        Assert.Equal(1, await db.WorkforceImportBatches.CountAsync(b =>
            b.FacilityId == SeedIds.FacilityA1
            && b.SourceSystem == body.sourceSystem
            && b.SourceReference == body.sourceReference
            && b.FileHash == body.fileHash));
    }

    private sealed record WorkforceSummaryResponse(
        Guid FacilityId,
        int TotalMembers,
        int Required,
        int Gap,
        decimal? CoverageRate);

    private sealed record CreateResponse(Guid Id);

    private sealed record ImportResultResponse(int TotalRows, int ValidRows, int RejectedRows, int DuplicateRows, int AppliedRows);

    private sealed record WorkspaceShellResponse(
        IReadOnlyList<WidgetDefinitionResponse> WidgetDefinitions,
        IReadOnlyList<WidgetInstanceResponse> Widgets);

    private sealed record WidgetDefinitionResponse(string Key);

    private sealed record WidgetInstanceResponse(string WidgetKey);
}
