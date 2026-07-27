using System.Net;
using System.Net.Http.Json;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Workforce;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

[Collection(WorkforceIntegrationCollection.Name)]
public sealed class WorkforceReadinessIntegrationTests(WorkforceIntegrationFixture fixture)
    : IntegrationTestBase<WorkforceIntegrationFixture>(fixture)
{
    private BaseeraApiFactory factory => Factory;

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
        Assert.True(summary.Scheduled >= summary.Present);
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
    public async Task Put_member_update_succeeds_and_audits()
    {
        await factory.SeedUserAsync(
            "workforce-put",
            "تحديث عضو",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-put");
        var create = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/members", new
        {
            displayName = "للتحديث",
            employeeNumber = $"WF-PUT-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            jobTitle = "ضابط",
            primarySpecialty = "أمن",
            employmentStatus = 0,
            isOperational = true,
            sourceType = 0
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        Assert.NotNull(created);

        byte[] rowVersion;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            rowVersion = await db.WorkforceMembers.Where(m => m.Id == created!.Id).Select(m => m.RowVersion).FirstAsync();
        }

        var update = await client.PutAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/members/{created!.Id}",
            new
            {
                displayName = "محدث",
                employmentStatus = 0,
                jobTitle = "قائد وردية",
                primarySpecialty = "قيادة",
                isOperational = true,
                isSensitiveRole = false,
                rowVersion
            });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        using var verify = factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var member = await vdb.WorkforceMembers.AsNoTracking().FirstAsync(m => m.Id == created.Id);
        Assert.Equal("محدث", member.DisplayName);
        Assert.Equal("قائد وردية", member.JobTitle);
        Assert.True(await vdb.AuditLogs.AnyAsync(a => a.Action == "WorkforceMemberUpdated" && a.EntityId == created.Id.ToString()));
    }

    [IntegrationConnectionFact]
    public async Task Put_member_forbidden_without_manage_members()
    {
        await SeedCustomRoleUserAsync(
            "workforce-put-403",
            "عرض فقط",
            [PermissionCodes.WorkforceViewMembers, PermissionCodes.WorkforceViewSummary],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-put-403");
        Guid memberId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            memberId = await db.WorkforceMembers
                .Where(m => m.CurrentOperationalFacilityId == SeedIds.FacilityA1)
                .Select(m => m.Id)
                .FirstAsync();
        }

        var response = await client.PutAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/members/{memberId}",
            new
            {
                displayName = "ممنوع",
                employmentStatus = 0,
                jobTitle = "ضابط",
                primarySpecialty = "أمن",
                isOperational = true,
                isSensitiveRole = false
            });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Put_member_404_out_of_scope()
    {
        await factory.SeedUserAsync(
            "workforce-put-404",
            "تحديث خارج",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-put-404");

        var response = await client.PutAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/members/{Guid.NewGuid()}",
            new
            {
                displayName = "مفقود",
                employmentStatus = 0,
                jobTitle = "ضابط",
                primarySpecialty = "أمن",
                isOperational = true,
                isSensitiveRole = false
            });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Reconciliation_list_resolve_and_audit()
    {
        await factory.SeedUserAsync(
            "workforce-reconcile",
            "مصالحة",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-reconcile");

        var list = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/reconciliation?page=1&pageSize=50");
        list.EnsureSuccessStatusCode();
        var payload = await list.Content.ReadFromJsonAsync<ReconciliationListResponse>(JsonOptions);
        Assert.NotNull(payload);
        Assert.True(payload!.TotalCount >= 0);

        if (payload.Items.Count == 0)
        {
            return;
        }

        var itemId = payload.Items[0].Id;
        var resolve = await client.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/reconciliation/{Uri.EscapeDataString(itemId)}/resolve",
            new { resolutionAction = "Acknowledged", notes = "اختبار" });
        Assert.Equal(HttpStatusCode.NoContent, resolve.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        Assert.True(await db.WorkforceReconciliationResolutions.AnyAsync(r =>
            r.FacilityId == SeedIds.FacilityA1 && r.ItemKey == itemId));
        Assert.True(await db.AuditLogs.AnyAsync(a =>
            a.Action == "WorkforceReconciled" && a.EntityId == itemId));
    }

    [IntegrationConnectionFact]
    public async Task Export_requires_permission_and_omits_restriction_codes()
    {
        await SeedCustomRoleUserAsync(
            "workforce-export-403",
            "بدون تصدير",
            [PermissionCodes.WorkforceViewSummary, PermissionCodes.WorkforceViewMembers],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var denied = factory.CreateAuthenticatedClient("workforce-export-403");
        var forbidden = await denied.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/export");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await factory.SeedUserAsync(
            "workforce-export-ok",
            "تصدير",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-export-ok");
        var ok = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/export?pageSize=50");
        ok.EnsureSuccessStatusCode();
        Assert.Equal("text/csv; charset=utf-8", ok.Content.Headers.ContentType?.ToString());
        var csv = await ok.Content.ReadAsStringAsync();
        Assert.Contains("EmployeeNumber", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("RestrictionCodes", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("CannotDrive", csv, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "WorkforceExported"));
    }

    [IntegrationConnectionFact]
    public async Task Critical_positions_endpoint_returns_computed_fields()
    {
        await factory.SeedUserAsync(
            "workforce-critical",
            "مناصب حرجة",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-critical");
        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/critical-positions");
        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<List<CriticalPositionResponse>>(JsonOptions);
        Assert.NotNull(rows);
        Assert.NotEmpty(rows!);
        Assert.All(rows, row =>
        {
            Assert.True(row.RequiredPrimaryCount >= 0);
            Assert.True(row.VacantPrimary >= 0);
            Assert.False(string.IsNullOrWhiteSpace(row.StatusAr));
        });
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

    [IntegrationConnectionFact]
    public async Task Get_member_hides_restrictions_without_sensitive_permission()
    {
        await SeedCustomRoleUserAsync(
            "workforce-member-plain",
            "عضو عادي",
            [PermissionCodes.WorkforceViewMembers],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-member-plain");
        Guid memberId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            memberId = await db.WorkforceMembers
                .Where(m => m.CurrentOperationalFacilityId == SeedIds.FacilityA1)
                .Select(m => m.Id)
                .FirstAsync();
            db.WorkforceAvailabilityEvents.Add(new WorkforceAvailabilityEvent
            {
                WorkforceMemberId = memberId,
                AvailabilityType = AvailabilityType.RestrictedDuty,
                StartsAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
                AffectsOperationalAvailability = true,
                RestrictionCodesCsv = "CannotDrive,CannotCarryWeapon",
                RecordedAtUtc = DateTimeOffset.UtcNow,
                RecordedBy = "test"
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/members/{memberId}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("CannotDrive", json, StringComparison.Ordinal);
        Assert.DoesNotContain("CannotCarryWeapon", json, StringComparison.Ordinal);
    }

    [IntegrationConnectionFact]
    public async Task Region_scope_can_read_facility_workforce_summary()
    {
        await SeedCustomRoleUserAsync(
            "workforce-region",
            "منطقة",
            [
                PermissionCodes.WorkforceViewSummary,
                PermissionCodes.WorkspacesViewFacility
            ],
            (ScopeType.Region, SeedIds.RegionA, null));
        var client = factory.CreateAuthenticatedClient("workforce-region");
        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkforceSummaryResponse>(JsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(SeedIds.FacilityA1, summary!.FacilityId);
        Assert.True(summary.TotalMembers >= 0);
    }

    [IntegrationConnectionFact]
    public async Task Facility_workspace_query_count_stays_within_budget()
    {
        var counter = new SqlCommandCounter();
        await using var countedFactory = BaseeraApiFactory.WithInterceptor(counter);
        await countedFactory.SeedUserAsync(
            "workforce-qcount",
            "عد استعلامات",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = countedFactory.CreateAuthenticatedClient("workforce-qcount");
        counter.Reset();
        var response = await client.GetAsync($"/api/v1/workspaces/facility-operations?level=1&facilityId={SeedIds.FacilityA1}");
        response.EnsureSuccessStatusCode();
        // Bumped 140 -> 150 for Phase D.6's Risk widget (summary + top interventions); see
        // docs/phase-d6-risk-performance.md.
        Assert.InRange(counter.SelectCount, 1, 150);
    }

    [IntegrationConnectionFact]
    public async Task Facility_workspace_payload_size_stays_within_budget()
    {
        await factory.SeedUserAsync(
            "workforce-payload",
            "حجم حمولة",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-payload");

        var response = await client.GetAsync($"/api/v1/workspaces/facility-operations?level=1&facilityId={SeedIds.FacilityA1}");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsByteArrayAsync();
        Assert.InRange(payload.Length, 1, 160_000);
    }

    [IntegrationConnectionFact]
    public async Task Large_workforce_member_list_remains_bounded()
    {
        await factory.SeedUserAsync(
            "workforce-large",
            "مجموعة كبيرة",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-large");

        var createTasks = Enumerable.Range(0, 25)
            .Select(index => client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/members", new
            {
                displayName = $"عضو حجم {index:00}",
                employeeNumber = $"WF-LG-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                jobTitle = "ضابط",
                primarySpecialty = "أمن",
                employmentStatus = 0,
                isOperational = true,
                sourceType = 0
            }))
            .ToArray();

        foreach (var create in await Task.WhenAll(createTasks))
        {
            create.EnsureSuccessStatusCode();
        }

        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/members?pageSize=10");

        response.EnsureSuccessStatusCode();
        var members = await response.Content.ReadFromJsonAsync<List<WorkforceMemberListItemResponse>>(JsonOptions);
        Assert.NotNull(members);
        Assert.InRange(members!.Count, 1, 10);
    }

    [IntegrationConnectionFact]
    public async Task Data_quality_does_not_count_published_roster_presence_as_unknown_availability()
    {
        await factory.SeedUserAsync(
            "workforce-roster-source",
            "مصدر الجدول",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-roster-source");

        var before = await ReadDataQualityIssueCountAsync(client, "unknown_availability");
        var create = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/members", new
        {
            displayName = "عضو حاضر من الجدول",
            employeeNumber = $"WF-RS-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            jobTitle = "ضابط",
            primarySpecialty = "أمن",
            employmentStatus = 0,
            isOperational = true,
            sourceType = 0
        });
        create.EnsureSuccessStatusCode();
        var member = await create.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        Assert.NotNull(member);

        var (shiftId, roleId) = await CreateUniqueShiftAndGetRoleAsync();
        var roster = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/rosters", new
        {
            shiftDefinitionId = shiftId,
            dutyDate = DateOnly.FromDateTime(DateTime.UtcNow.Date)
        });
        roster.EnsureSuccessStatusCode();
        var rosterId = await roster.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        Assert.NotNull(rosterId);

        var assignment = await client.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/rosters/{rosterId!.Id}/assignments",
            new
            {
                workforceMemberId = member!.Id,
                roleDefinitionId = roleId,
                status = 2
            });
        assignment.EnsureSuccessStatusCode();

        var publish = await client.PostAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/rosters/{rosterId.Id}/publish",
            null);
        publish.EnsureSuccessStatusCode();

        var after = await ReadDataQualityIssueCountAsync(client, "unknown_availability");
        Assert.Equal(before, after);
    }

    [IntegrationConnectionFact]
    public async Task IT03_Hq_global_scope_can_read_facility_workforce_summary()
    {
        await factory.SeedUserAsync(
            "workforce-hq",
            "مقر",
            [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var client = factory.CreateAuthenticatedClient("workforce-hq");
        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkforceSummaryResponse>(JsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(SeedIds.FacilityA1, summary!.FacilityId);
    }

    [IntegrationConnectionFact]
    public async Task IT08_to_IT14_assignment_qualification_requirement_roster_availability_lifecycle()
    {
        await factory.SeedUserAsync(
            "workforce-lifecycle",
            "دورة حياة",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-lifecycle");

        var create = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/members", new
        {
            displayName = "دورة حياة",
            employeeNumber = $"WF-LC-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            jobTitle = "ضابط",
            primarySpecialty = "أمن",
            employmentStatus = 0,
            isOperational = true,
            sourceType = 0
        });
        create.EnsureSuccessStatusCode();
        var member = await create.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        Assert.NotNull(member);

        Guid roleId;
        Guid? unitId;
        Guid? shiftId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            roleId = await db.WorkforceRoleDefinitions.AsNoTracking()
                .Where(r => !r.IsDeleted)
                .Select(r => r.Id)
                .FirstAsync();
            unitId = await db.FacilityUnits.AsNoTracking()
                .Where(u => u.FacilityId == SeedIds.FacilityA1)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync();
            shiftId = await db.ShiftDefinitions.AsNoTracking()
                .Where(s => s.FacilityId == SeedIds.FacilityA1 && !s.IsDeleted)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync();
        }

        var assignment = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/assignments", new
        {
            workforceMemberId = member!.Id,
            roleDefinitionId = roleId,
            facilityUnitId = unitId,
            assignmentType = 0,
            isPrimary = true,
            effectiveFromUtc = DateTimeOffset.UtcNow.AddDays(-1)
        });
        assignment.EnsureSuccessStatusCode();

        var qualification = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/qualifications", new
        {
            workforceMemberId = member.Id,
            roleDefinitionId = roleId,
            qualificationType = 0,
            name = "شهادة اختبار",
            status = 0,
            issuedAtUtc = DateTimeOffset.UtcNow.AddMonths(-6),
            expiresAtUtc = DateTimeOffset.UtcNow.AddMonths(6)
        });
        qualification.EnsureSuccessStatusCode();

        var requirement = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/requirements", new
        {
            roleDefinitionId = roleId,
            facilityUnitId = unitId,
            shiftDefinitionId = shiftId,
            requiredHeadcount = 2,
            minimumSafeHeadcount = 1,
            effectiveFromUtc = DateTimeOffset.UtcNow.AddDays(-1),
            sourceReference = "IT-LIFECYCLE",
            approvalReference = "TEST-REQ"
        });
        requirement.EnsureSuccessStatusCode();

        Assert.True(shiftId.HasValue);
        var roster = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/rosters", new
        {
            shiftDefinitionId = shiftId,
            facilityUnitId = unitId,
            dutyDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(2)
        });
        roster.EnsureSuccessStatusCode();
        var rosterId = await roster.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        Assert.NotNull(rosterId);

        var rosterAssignment = await client.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/rosters/{rosterId!.Id}/assignments",
            new
            {
                workforceMemberId = member.Id,
                roleDefinitionId = roleId,
                status = 0
            });
        rosterAssignment.EnsureSuccessStatusCode();

        var publish = await client.PostAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/rosters/{rosterId.Id}/publish",
            null);
        publish.EnsureSuccessStatusCode();

        var availability = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/availability", new
        {
            workforceMemberId = member.Id,
            availabilityType = 1,
            startsAtUtc = DateTimeOffset.UtcNow,
            endsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            affectsOperationalAvailability = true,
            sourceType = 0
        });
        availability.EnsureSuccessStatusCode();
    }

    [IntegrationConnectionFact]
    public async Task IT16_to_IT18_coverage_units_and_roles_endpoints()
    {
        await factory.SeedUserAsync(
            "workforce-coverage",
            "تغطية",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-coverage");

        var coverage = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/coverage");
        coverage.EnsureSuccessStatusCode();
        var coverageBody = await coverage.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(coverageBody));

        var units = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/units");
        units.EnsureSuccessStatusCode();

        var roles = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/roles");
        roles.EnsureSuccessStatusCode();

        var requirements = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/requirements");
        requirements.EnsureSuccessStatusCode();

        var rosters = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/rosters");
        rosters.EnsureSuccessStatusCode();
    }

    [IntegrationConnectionFact]
    public async Task IT21_to_IT25_workspace_interventions_timeline_data_quality()
    {
        await factory.SeedUserAsync(
            "workforce-workspace-deep",
            "مساحة عميقة",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-workspace-deep");

        var shell = await client.GetAsync($"/api/v1/workspaces/facility-operations?level=1&facilityId={SeedIds.FacilityA1}");
        shell.EnsureSuccessStatusCode();
        var json = await shell.Content.ReadAsStringAsync();
        Assert.Contains("facility.workforce", json, StringComparison.Ordinal);
        Assert.Contains("priority", json, StringComparison.OrdinalIgnoreCase);

        var dq = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/data-quality");
        dq.EnsureSuccessStatusCode();
        var dqJson = await dq.Content.ReadAsStringAsync();
        Assert.Contains("issues", dqJson, StringComparison.OrdinalIgnoreCase);
    }

    [IntegrationConnectionFact]
    public async Task IT26_import_preview_rejects_invalid_rows_before_confirm()
    {
        await factory.SeedUserAsync(
            "workforce-import-preview",
            "معاينة استيراد",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-import-preview");
        var preview = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/import/preview", new
        {
            sourceSystem = "IT-PREVIEW",
            sourceReference = $"preview-{Guid.NewGuid():N}",
            fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("preview"))),
            importKind = 0,
            rows = new[]
            {
                new { employeeNumber = "", displayName = "", jobTitle = "x", employmentStatus = 0, isOperational = true }
            }
        });
        preview.EnsureSuccessStatusCode();
        var result = await preview.Content.ReadFromJsonAsync<ImportResultResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.True(result!.RejectedRows >= 1);
        Assert.Equal(0, result.AppliedRows);
    }

    [IntegrationConnectionFact]
    public async Task IT31_migration_workforce_tables_exist()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        Assert.True(await db.WorkforceMembers.AnyAsync());
        Assert.True(await db.WorkforceRoleDefinitions.AnyAsync());
        Assert.True(await db.StaffingRequirements.AnyAsync(r => r.FacilityId == SeedIds.FacilityA1));
        Assert.NotNull(db.Model.FindEntityType(typeof(WorkforceReconciliationResolution)));
        Assert.NotNull(db.Model.FindEntityType(typeof(WorkforceImportBatch)));
    }

    [IntegrationConnectionFact]
    public async Task IT19_summary_scheduled_not_less_than_present_no_double_count_signal()
    {
        await factory.SeedUserAsync(
            "workforce-nodouble",
            "لا ازدواج",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-nodouble");
        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/summary");
        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<WorkforceSummaryResponse>(JsonOptions);
        Assert.NotNull(summary);
        Assert.True(summary!.Scheduled >= summary.Present);
        Assert.True(summary.TotalMembers >= 0);
    }

    [IntegrationConnectionFact]
    public async Task IT30_multi_kind_import_preview_accepts_assignments_and_availability_kinds()
    {
        await factory.SeedUserAsync(
            "workforce-import-kinds",
            "أنواع استيراد",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workforce-import-kinds");

        Guid memberId;
        Guid roleId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            memberId = await db.WorkforceMembers.AsNoTracking()
                .Where(m => m.CurrentOperationalFacilityId == SeedIds.FacilityA1)
                .Select(m => m.Id)
                .FirstAsync();
            roleId = await db.WorkforceRoleDefinitions.AsNoTracking()
                .Where(r => !r.IsDeleted)
                .Select(r => r.Id)
                .FirstAsync();
        }

        var employeeNumber = await GetEmployeeNumberAsync(memberId);
        var assignments = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/import/preview", new
        {
            importKind = 1,
            sourceSystem = "IT-ASSIGN",
            sourceReference = $"assign-{Guid.NewGuid():N}",
            fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("assign"))),
            rows = new[]
            {
                new
                {
                    employeeNumber,
                    roleDefinitionId = roleId,
                    assignmentType = 0,
                    effectiveFromUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    isOperational = true
                }
            }
        });
        assignments.EnsureSuccessStatusCode();
        var assignmentPreview = await assignments.Content.ReadFromJsonAsync<ImportResultResponse>(JsonOptions);
        Assert.NotNull(assignmentPreview);
        Assert.True(assignmentPreview!.TotalRows >= 1);

        var availability = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/import/preview", new
        {
            importKind = 4,
            sourceSystem = "IT-AVAIL",
            sourceReference = $"avail-{Guid.NewGuid():N}",
            fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("avail"))),
            rows = new[]
            {
                new
                {
                    employeeNumber,
                    availabilityType = 1,
                    availabilityStartsAtUtc = DateTimeOffset.UtcNow,
                    availabilityEndsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
                    isOperational = true
                }
            }
        });
        availability.EnsureSuccessStatusCode();
        var availabilityPreview = await availability.Content.ReadFromJsonAsync<ImportResultResponse>(JsonOptions);
        Assert.NotNull(availabilityPreview);
        Assert.True(availabilityPreview!.TotalRows >= 1);
    }

    private async Task<string> GetEmployeeNumberAsync(Guid memberId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        return await db.WorkforceMembers.AsNoTracking()
            .Where(m => m.Id == memberId)
            .Select(m => m.EmployeeNumber)
            .FirstAsync();
    }

    private async Task<(Guid ShiftId, Guid RoleId)> CreateUniqueShiftAndGetRoleAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var shift = new ShiftDefinition
        {
            OrganizationId = SeedIds.Organization,
            FacilityId = SeedIds.FacilityA1,
            Code = $"RS-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Name = "وردية مصدر الجدول",
            StartLocalTime = new TimeOnly(8, 0),
            EndLocalTime = new TimeOnly(16, 0),
            CrossesMidnight = false,
            Timezone = "Asia/Riyadh",
            IsActive = true
        };
        db.ShiftDefinitions.Add(shift);
        var roleId = await db.WorkforceRoleDefinitions.AsNoTracking()
            .Where(r => !r.IsDeleted)
            .Select(r => r.Id)
            .FirstAsync();
        await db.SaveChangesAsync();
        return (shift.Id, roleId);
    }

    private static async Task<int> ReadDataQualityIssueCountAsync(HttpClient client, string code)
    {
        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/workforce/data-quality");
        response.EnsureSuccessStatusCode();
        var dataQuality = await response.Content.ReadFromJsonAsync<DataQualityResponse>(JsonOptions);
        Assert.NotNull(dataQuality);
        return dataQuality!.Issues.SingleOrDefault(issue => issue.Code == code)?.Count ?? 0;
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

    private sealed record WorkforceSummaryResponse(
        Guid FacilityId,
        int TotalMembers,
        int Required,
        int Gap,
        int Scheduled,
        int Present,
        decimal? CoverageRate);

    private sealed record CreateResponse(Guid Id);

    private sealed record ImportResultResponse(int TotalRows, int ValidRows, int RejectedRows, int DuplicateRows, int AppliedRows);

    private sealed record WorkforceMemberListItemResponse(Guid Id, string DisplayName, string EmployeeNumber);

    private sealed record DataQualityResponse(IReadOnlyList<DataQualityIssueResponse> Issues);

    private sealed record DataQualityIssueResponse(string Code, int Count);

    private sealed record ReconciliationListResponse(
        IReadOnlyList<ReconciliationItemResponse> Items,
        int TotalCount,
        int Page,
        int PageSize);

    private sealed record ReconciliationItemResponse(string Id, string IssueType, string Severity);

    private sealed record CriticalPositionResponse(
        Guid Id,
        int RequiredPrimaryCount,
        int VacantPrimary,
        string StatusAr);

    private sealed record WorkspaceShellResponse(
        IReadOnlyList<WidgetDefinitionResponse> WidgetDefinitions,
        IReadOnlyList<WidgetInstanceResponse> Widgets);

    private sealed record WidgetDefinitionResponse(string Key);

    private sealed record WidgetInstanceResponse(string WidgetKey);

    private sealed class SqlCommandCounter : DbCommandInterceptor
    {
        public int SelectCount { get; private set; }

        public void Reset() => SelectCount = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            CountIfSelect(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            CountIfSelect(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void CountIfSelect(string? text)
        {
            if (!string.IsNullOrWhiteSpace(text)
                && text.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                SelectCount++;
            }
        }
    }
}
