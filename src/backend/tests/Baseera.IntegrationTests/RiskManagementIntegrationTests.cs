namespace Baseera.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Application.RiskManagement;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.RiskManagement;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(RiskManagementIntegrationCollection.Name)]
public sealed class RiskManagementIntegrationTests(RiskManagementIntegrationFixture fixture)
    : IntegrationTestBase<RiskManagementIntegrationFixture>(fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private BaseeraApiFactory factory => Factory;

    [IntegrationConnectionFact]
    public async Task Summary_requires_permission_and_facility_scope()
    {
        await factory.SeedUserAsync("risk-no-permission", "بدون صلاحية", [RoleCodes.FormRespondent], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await factory.SeedUserAsync("risk-scoped-a1", "ضابط مخاطر أ1", [RoleCodes.RiskOfficer], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        var noPermission = factory.CreateAuthenticatedClient("risk-no-permission");
        var forbidden = await noPermission.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/summary");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var scoped = factory.CreateAuthenticatedClient("risk-scoped-a1");
        var outOfScope = await scoped.GetAsync($"/api/v1/facilities/{SeedIds.FacilityB1}/risks/summary");
        Assert.Equal(HttpStatusCode.NotFound, outOfScope.StatusCode);

        var inScope = await scoped.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/summary");
        inScope.EnsureSuccessStatusCode();
    }

    [IntegrationConnectionFact]
    public async Task Create_risk_generates_sequential_code_and_starts_in_draft()
    {
        var reference = await SeedRiskReferenceAsync();
        await factory.SeedUserAsync("risk-create-1", "ضابط مخاطر", [RoleCodes.RiskOfficer], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("risk-create-1");

        var riskId = await CreateRiskAsync(client, reference.CategoryId, "خطر اختبار أول");

        var detail = await GetDetailAsync(client, riskId);
        Assert.StartsWith("RSK-", detail.RiskCode);
        Assert.Equal(0, detail.Status);
        Assert.False(detail.AllowedActions.Count == 0);
    }

    [IntegrationConnectionFact]
    public async Task Archive_is_rejected_from_a_status_the_lifecycle_does_not_allow()
    {
        // Regression test for a real bug: TransitionAsync previously mutated status/history/audit before any
        // lifecycle check, so ArchiveAsync could archive a risk from any status. A freshly created risk starts
        // in Draft, and Archive is only ever allowed from Closed — this must now be rejected with a conflict,
        // and the risk's status/RowVersion must be left completely untouched.
        var reference = await SeedRiskReferenceAsync();
        await factory.SeedUserAsync("risk-archive-1", "ضابط مخاطر", [RoleCodes.RiskOfficer], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("risk-archive-1");
        var riskId = await CreateRiskAsync(client, reference.CategoryId, "خطر أرشفة غير مسموحة");

        var beforeRowVersion = await GetRowVersionAsync(riskId);

        var archive = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/command", new
        {
            command = RiskCommandTypes.Archive,
            rowVersion = beforeRowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, archive.StatusCode);

        var detail = await GetDetailAsync(client, riskId);
        Assert.Equal(0, detail.Status); // still Draft
        Assert.Equal(beforeRowVersion, await GetRowVersionAsync(riskId));
    }

    [IntegrationConnectionFact]
    public async Task Update_risk_detects_row_version_conflict()
    {
        var reference = await SeedRiskReferenceAsync();
        await factory.SeedUserAsync("risk-update-1", "ضابط مخاطر", [RoleCodes.RiskOfficer], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("risk-update-1");
        var riskId = await CreateRiskAsync(client, reference.CategoryId, "خطر تحديث");
        var detail = await GetDetailAsync(client, riskId);

        var badUpdate = await client.PutAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}", new
        {
            title = "خطر محدث",
            riskCategoryId = reference.CategoryId,
            riskType = 0,
            rowVersion = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
        });

        Assert.Equal(HttpStatusCode.Conflict, badUpdate.StatusCode);

        var goodUpdate = await client.PutAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}", new
        {
            title = "خطر محدث",
            riskCategoryId = reference.CategoryId,
            riskType = 0,
            rowVersion = detail.RowVersion
        });
        Assert.Equal(HttpStatusCode.NoContent, goodUpdate.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Matrix_lifecycle_create_approve_activate_retires_previous_default()
    {
        var firstMatrixId = await SeedActiveMatrixDirectAsync("MTXA");
        await factory.SeedUserAsync("matrix-creator", "منشئ مصفوفة", [RoleCodes.RiskOfficer], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await factory.SeedUserAsync("matrix-approver", "معتمد مصفوفة", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var creator = factory.CreateAuthenticatedClient("matrix-creator");
        var approver = factory.CreateAuthenticatedClient("matrix-approver");

        Guid dimensionId;
        using (var seedScope = factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var dimension = new ImpactDimension { OrganizationId = SeedIds.Organization, Code = $"MTXB-DIM-{Guid.NewGuid():N}"[..16], NameAr = "بعد", IsActive = true };
            seedDb.Add(dimension);
            await seedDb.SaveChangesAsync();
            dimensionId = dimension.Id;
        }

        var createResponse = await creator.PostAsJsonAsync("/api/v1/risk-matrices?organizationId=" + SeedIds.Organization, new
        {
            code = "MTXB",
            name = "مصفوفة ثانية",
            scoreFormula = 0,
            effectiveFromUtc = DateTimeOffset.UtcNow,
            isDefault = true,
            likelihoodLevels = Enumerable.Range(1, 5).Select(i => new { code = $"L{i}", name = $"L{i}", numericValue = i }).ToArray(),
            impactLevels = Enumerable.Range(1, 5).Select(i => new { impactDimensionId = dimensionId, code = $"I{i}", name = $"I{i}", numericValue = i }).ToArray(),
            ratingBands = new[]
            {
                new { code = "LOW", labelAr = "منخفضة", minimumScore = 1m, maximumScore = 25m, severity = 0, escalationRequired = false, colorToken = "info" }
            }
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        Assert.NotNull(created);

        // Cannot activate directly from Draft.
        var invalidActivate = await approver.PostAsJsonAsync($"/api/v1/risk-matrices/{created!.Id}/activate?organizationId={SeedIds.Organization}", new { rowVersion = await GetMatrixRowVersionAsync(created.Id) });
        Assert.Equal(HttpStatusCode.Conflict, invalidActivate.StatusCode);

        var approve = await approver.PostAsJsonAsync($"/api/v1/risk-matrices/{created.Id}/approve?organizationId={SeedIds.Organization}", new { rowVersion = await GetMatrixRowVersionAsync(created.Id) });
        approve.EnsureSuccessStatusCode();

        var activate = await approver.PostAsJsonAsync($"/api/v1/risk-matrices/{created.Id}/activate?organizationId={SeedIds.Organization}", new { rowVersion = await GetMatrixRowVersionAsync(created.Id) });
        activate.EnsureSuccessStatusCode();

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var previous = await verifyDb.RiskAssessmentMatrices.AsNoTracking().SingleAsync(m => m.Id == firstMatrixId);
        Assert.Equal(MatrixStatus.Retired, previous.Status);
        var activated = await verifyDb.RiskAssessmentMatrices.AsNoTracking().SingleAsync(m => m.Id == created.Id);
        Assert.Equal(MatrixStatus.Active, activated.Status);
        Assert.True(activated.IsDefault);
    }

    [IntegrationConnectionFact]
    public async Task Matrix_operations_reject_organization_ids_outside_the_callers_scope()
    {
        // Regression test for a real gap: RiskMatrixService previously trusted the organizationId query
        // parameter with no scope check at all, so any caller with Risks.View could pass an arbitrary
        // organizationId and read/write a different organization's matrices. Seed a second organization the
        // facility-scoped user has no scope over, and confirm every matrix operation now returns 404 for it.
        Guid otherOrganizationId;
        using (var seedScope = factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var otherOrg = new Baseera.Domain.Organization.Organization { Code = $"ORG-{Guid.NewGuid():N}"[..12], NameAr = "منظمة أخرى", IsActive = true };
            seedDb.Add(otherOrg);
            await seedDb.SaveChangesAsync();
            otherOrganizationId = otherOrg.Id;
        }

        await factory.SeedUserAsync("matrix-cross-org", "ضابط مخاطر نطاق محدود", [RoleCodes.RiskOfficer], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("matrix-cross-org");

        var list = await client.GetAsync($"/api/v1/risk-matrices?organizationId={otherOrganizationId}");
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);

        var create = await client.PostAsJsonAsync($"/api/v1/risk-matrices?organizationId={otherOrganizationId}", new
        {
            code = "XORG",
            name = "مصفوفة خارج النطاق",
            scoreFormula = 0,
            effectiveFromUtc = DateTimeOffset.UtcNow,
            isDefault = false,
            likelihoodLevels = new[] { new { code = "L1", name = "L1", numericValue = 1 } },
            impactLevels = Array.Empty<object>(),
            ratingBands = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Matrix_operations_succeed_for_national_scope_user_regardless_of_organization()
    {
        // National (Global) scope must still work end-to-end for organization-wide resources like matrices —
        // the scope fix must not accidentally lock out the one scope tier that legitimately spans every org.
        Guid dimensionId;
        using (var seedScope = factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var dimension = new ImpactDimension { OrganizationId = SeedIds.Organization, Code = $"NAT-DIM-{Guid.NewGuid():N}"[..16], NameAr = "بعد", IsActive = true };
            seedDb.Add(dimension);
            await seedDb.SaveChangesAsync();
            dimensionId = dimension.Id;
        }

        await factory.SeedUserAsync("matrix-national", "مدير نظام", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var client = factory.CreateAuthenticatedClient("matrix-national");

        var list = await client.GetAsync($"/api/v1/risk-matrices?organizationId={SeedIds.Organization}");
        list.EnsureSuccessStatusCode();

        var create = await client.PostAsJsonAsync($"/api/v1/risk-matrices?organizationId={SeedIds.Organization}", new
        {
            code = $"NAT-{Guid.NewGuid():N}"[..8],
            name = "مصفوفة وطنية",
            scoreFormula = 0,
            effectiveFromUtc = DateTimeOffset.UtcNow,
            isDefault = false,
            likelihoodLevels = new[] { new { code = "L1", name = "L1", numericValue = 1 } },
            impactLevels = new[] { new { impactDimensionId = dimensionId, code = "I1", name = "I1", numericValue = 1 } },
            ratingBands = new[] { new { code = "ONLY", labelAr = "الوحيد", minimumScore = 1m, maximumScore = 1m, severity = 0, escalationRequired = false, colorToken = "info" } }
        });
        create.EnsureSuccessStatusCode();
    }

    [IntegrationConnectionFact]
    public async Task Assessment_score_is_server_computed_and_activates_risk_on_approval()
    {
        var reference = await SeedRiskReferenceAsync();
        // The creator also holds the approver role so the four-eyes checks below are exercised as a
        // genuine "same actor, has permission, but is still blocked" case rather than a 403 permission gap.
        await factory.SeedUserAsync("assess-creator", "ضابط مخاطر", [RoleCodes.RiskOfficer, RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await factory.SeedUserAsync("assess-approver", "مدير سجن", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var creator = factory.CreateAuthenticatedClient("assess-creator");
        var approver = factory.CreateAuthenticatedClient("assess-approver");

        var riskId = await CreateRiskAsync(creator, reference.CategoryId, "خطر تقييم");

        var assessmentId = await CreateAssessmentAsync(creator, riskId, reference, likelihoodValue: 3, impactValue: 4, assessmentType: 0);

        var afterCreate = await GetDetailAsync(creator, riskId);
        Assert.Equal(1, afterCreate.Status); // UnderAssessment

        var submit = await creator.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments/{assessmentId}/submit", new { rowVersion = await GetAssessmentRowVersionAsync(riskId, assessmentId) });
        submit.EnsureSuccessStatusCode();

        // Four-eyes: creator cannot review their own assessment.
        var selfReview = await creator.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments/{assessmentId}/review", new { approve = true, rowVersion = await GetAssessmentRowVersionAsync(riskId, assessmentId) });
        Assert.Equal(HttpStatusCode.Conflict, selfReview.StatusCode);

        var review = await approver.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments/{assessmentId}/review", new { approve = true, rowVersion = await GetAssessmentRowVersionAsync(riskId, assessmentId) });
        review.EnsureSuccessStatusCode();

        // Four-eyes: creator cannot approve their own assessment either.
        var selfApprove = await creator.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments/{assessmentId}/approve", new { rowVersion = await GetAssessmentRowVersionAsync(riskId, assessmentId) });
        Assert.Equal(HttpStatusCode.Conflict, selfApprove.StatusCode);

        var approve = await approver.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments/{assessmentId}/approve", new { rowVersion = await GetAssessmentRowVersionAsync(riskId, assessmentId) });
        approve.EnsureSuccessStatusCode();

        var finalDetail = await GetDetailAsync(creator, riskId);
        Assert.Equal(3, finalDetail.Status); // Active
        Assert.NotNull(finalDetail.CurrentAssessment);
        Assert.Equal(12m, finalDetail.CurrentAssessment!.CalculatedScore); // likelihood 3 * max impact 4
    }

    [IntegrationConnectionFact]
    public async Task High_severity_assessment_requires_rationale()
    {
        var reference = await SeedRiskReferenceAsync();
        await factory.SeedUserAsync("assess-rationale", "ضابط مخاطر", [RoleCodes.RiskOfficer], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("assess-rationale");
        var riskId = await CreateRiskAsync(client, reference.CategoryId, "خطر بلا مبرر");

        var response = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments", new
        {
            assessmentType = 0,
            likelihoodLevelId = reference.LikelihoodLevelIds[4],
            impacts = new[] { new { impactDimensionId = reference.ImpactDimensionId, impactLevelId = reference.ImpactLevelIdsByValue[5] } }
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Control_can_be_created_and_tested()
    {
        var reference = await SeedRiskReferenceAsync();
        await factory.SeedUserAsync("control-officer", "ضابط مخاطر", [RoleCodes.RiskOfficer], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("control-officer");
        var riskId = await CreateRiskAsync(client, reference.CategoryId, "خطر ضوابط");

        var create = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/controls", new
        {
            controlType = 0,
            title = "ضابط تجريبي",
            evidenceRequired = false
        });
        create.EnsureSuccessStatusCode();
        var control = await create.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var stored = await db.RiskControls.AsNoTracking().SingleAsync(c => c.Id == control!.Id);
        Assert.Equal(ControlEffectiveness.NotTested, stored.ControlEffectiveness);

        var test = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/controls/{control!.Id}/test", new
        {
            controlEffectiveness = (int)ControlEffectiveness.Effective,
            rowVersion = Convert.ToBase64String(stored.RowVersion)
        });
        test.EnsureSuccessStatusCode();
    }

    [IntegrationConnectionFact]
    public async Task Treatment_plan_and_action_lifecycle_requires_four_eyes_and_blocks_closure_gate()
    {
        var reference = await SeedRiskReferenceAsync();
        // The officer also holds the director role so the four-eyes checks below (self-approve-plan,
        // self-verify-action) are exercised as "same actor, has permission, still blocked" rather than a
        // 403 permission gap — treatment approval/verification has no separate permission from creation,
        // only the four-eyes same-actor check.
        await factory.SeedUserAsync("treatment-officer", "ضابط مخاطر", [RoleCodes.RiskOfficer, RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await factory.SeedUserAsync("treatment-director", "مدير سجن", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var officer = factory.CreateAuthenticatedClient("treatment-officer");
        var director = factory.CreateAuthenticatedClient("treatment-director");

        var riskId = await CreateRiskAsync(officer, reference.CategoryId, "خطر معالجة");
        await ApproveInherentAssessmentAsync(officer, director, riskId, reference, likelihoodValue: 2, impactValue: 2);

        var planResponse = await officer.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/treatments", new
        {
            strategy = 1,
            title = "خطة معالجة",
            objective = "خفض الاحتمالية",
            dueAtUtc = DateTimeOffset.UtcNow.AddDays(30)
        });
        planResponse.EnsureSuccessStatusCode();
        var plan = await planResponse.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);

        await SubmitPlanCommandAsync(officer, riskId, plan!.Id, "Submit");

        // Four-eyes: plan creator cannot approve their own plan.
        var selfApprovePlan = await officer.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/treatments/{plan.Id}/command",
            new { command = "Approve", rowVersion = await GetPlanRowVersionAsync(plan.Id) });
        Assert.Equal(HttpStatusCode.Conflict, selfApprovePlan.StatusCode);

        var approvePlan = await director.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/treatments/{plan.Id}/command",
            new { command = "Approve", rowVersion = await GetPlanRowVersionAsync(plan.Id) });
        approvePlan.EnsureSuccessStatusCode();

        var riskAfterApproval = await GetDetailAsync(officer, riskId);
        Assert.Equal(4, riskAfterApproval.Status); // UnderTreatment

        var actionResponse = await officer.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/treatments/{plan.Id}/actions", new
        {
            title = "إجراء معالجة",
            dueAtUtc = DateTimeOffset.UtcNow.AddDays(10),
            completionEvidenceRequired = false
        });
        actionResponse.EnsureSuccessStatusCode();
        var action = await actionResponse.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);

        await SubmitPlanCommandAsync(officer, riskId, plan.Id, "Start");

        // Closing the plan before all actions are terminal must fail.
        var earlyComplete = await officer.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/treatments/{plan.Id}/command",
            new { command = "Complete", rowVersion = await GetPlanRowVersionAsync(plan.Id) });
        Assert.Equal(HttpStatusCode.Conflict, earlyComplete.StatusCode);

        await SubmitActionCommandAsync(officer, riskId, plan.Id, action!.Id, "Assign");
        await SubmitActionCommandAsync(officer, riskId, plan.Id, action.Id, "Start");
        await SubmitActionCommandAsync(officer, riskId, plan.Id, action.Id, "SubmitForVerification");

        var selfVerify = await officer.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/treatments/{plan.Id}/actions/{action.Id}/command",
            new { command = "Verify", rowVersion = await GetActionRowVersionAsync(action.Id) });
        Assert.Equal(HttpStatusCode.Conflict, selfVerify.StatusCode);

        var verify = await director.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/treatments/{plan.Id}/actions/{action.Id}/command",
            new { command = "Verify", rowVersion = await GetActionRowVersionAsync(action.Id) });
        verify.EnsureSuccessStatusCode();

        var completePlan = await officer.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/treatments/{plan.Id}/command",
            new { command = "Complete", rowVersion = await GetPlanRowVersionAsync(plan.Id) });
        completePlan.EnsureSuccessStatusCode();
    }

    [IntegrationConnectionFact]
    public async Task Closure_requires_approved_residual_assessment_and_reopen_restarts_assessment()
    {
        var reference = await SeedRiskReferenceAsync();
        await factory.SeedUserAsync("closure-officer", "ضابط مخاطر", [RoleCodes.RiskOfficer], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await factory.SeedUserAsync("closure-director", "مدير سجن", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var officer = factory.CreateAuthenticatedClient("closure-officer");
        var director = factory.CreateAuthenticatedClient("closure-director");

        var riskId = await CreateRiskAsync(officer, reference.CategoryId, "خطر إغلاق");
        await ApproveInherentAssessmentAsync(officer, director, riskId, reference, likelihoodValue: 1, impactValue: 1);

        var monitoring = await officer.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/command", new
        {
            command = "StartMonitoring",
            rowVersion = await GetRowVersionAsync(riskId)
        });
        monitoring.EnsureSuccessStatusCode();

        // Requesting closure without an approved residual assessment must fail.
        var earlyClosure = await officer.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/reviews", new
        {
            reviewType = 3, // ClosureApproval
            closureReason = "تمت معالجة الخطر بالكامل."
        });
        Assert.Equal(HttpStatusCode.Conflict, earlyClosure.StatusCode);

        await CreateAndApproveResidualAssessmentAsync(officer, director, riskId, reference);

        var closureRequest = await officer.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/reviews", new
        {
            reviewType = 3,
            closureReason = "تمت معالجة الخطر بالكامل ولا يوجد أثر متبقٍ يستدعي الاستمرار."
        });
        closureRequest.EnsureSuccessStatusCode();
        var review = await closureRequest.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);

        var closureApproval = await director.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/reviews/{review!.Id}/decision", new
        {
            decision = 0, // Approved
            rowVersion = await GetReviewRowVersionAsync(review.Id)
        });
        closureApproval.EnsureSuccessStatusCode();

        var closedDetail = await GetDetailAsync(officer, riskId);
        Assert.Equal(9, closedDetail.Status); // Closed
        Assert.NotNull(closedDetail.ClosedAtUtc);

        var reopen = await officer.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/command", new
        {
            command = "Reopen",
            reason = "ظهر دليل جديد يستدعي إعادة الفتح.",
            rowVersion = await GetRowVersionAsync(riskId)
        });
        reopen.EnsureSuccessStatusCode();

        var reopenedDetail = await GetDetailAsync(officer, riskId);
        Assert.Equal(1, reopenedDetail.Status); // UnderAssessment (auto-chained after Reopened)
        Assert.Equal(1, reopenedDetail.ReopenedCount);
    }

    [IntegrationConnectionFact]
    public async Task Source_link_rejects_cross_facility_scope()
    {
        var reference = await SeedRiskReferenceAsync();
        await factory.SeedUserAsync("source-officer", "ضابط مخاطر", [RoleCodes.RiskOfficer], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("source-officer");
        var riskId = await CreateRiskAsync(client, reference.CategoryId, "خطر مصادر");

        var otherFacilityResourceId = await SeedResourceInFacilityAsync(SeedIds.FacilityB1);

        var response = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/sources", new
        {
            sourceEntityType = 5, // ResourceAsset
            sourceEntityId = otherFacilityResourceId,
            relationshipType = 7 // Related
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Import_confirm_is_idempotent_on_same_file_hash()
    {
        var reference = await SeedRiskReferenceAsync();
        await factory.SeedUserAsync("import-officer", "ضابط استيراد", [RoleCodes.RiskOfficer], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("import-officer");
        var fileHash = $"hash-{Guid.NewGuid():N}";

        var payload = new
        {
            sourceSystem = "legacy",
            sourceReference = "batch-1",
            fileHash,
            rows = new[]
            {
                new
                {
                    rowKey = "row-1",
                    title = "خطر مستورد",
                    categoryCode = reference.CategoryCode,
                    riskType = 0,
                    matrixId = reference.MatrixId,
                    likelihoodCode = "L2",
                    impactCodesByDimensionCode = new Dictionary<string, string> { ["SEC"] = "I2" }
                }
            }
        };

        var preview = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/import/preview", payload);
        preview.EnsureSuccessStatusCode();

        var confirm1 = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/import/confirm", payload);
        confirm1.EnsureSuccessStatusCode();
        var result1 = await confirm1.Content.ReadFromJsonAsync<ImportResultResponse>(JsonOptions);
        Assert.Equal(1, result1!.AppliedRows);

        var confirm2 = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/import/confirm", payload);
        confirm2.EnsureSuccessStatusCode();
        var result2 = await confirm2.Content.ReadFromJsonAsync<ImportResultResponse>(JsonOptions);
        Assert.Equal(1, result2!.AppliedRows);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var count = await db.RiskRecords.CountAsync(r => r.SourceReference == "row-1");
        Assert.Equal(1, count);
    }

    [IntegrationConnectionFact]
    public async Task Data_quality_reports_missing_owner_issue()
    {
        var reference = await SeedRiskReferenceAsync();
        await factory.SeedUserAsync("dq-officer", "ضابط مخاطر", [RoleCodes.RiskOfficer], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("dq-officer");
        await CreateRiskAsync(client, reference.CategoryId, "خطر بلا مالك لفحص الجودة");

        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/data-quality");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<DataQualityResponse>(JsonOptions);

        Assert.NotNull(payload);
        Assert.Contains(payload!.Issues, issue => issue.Code == RiskDataQualityCodes.MissingOwner && issue.Count > 0);
    }

    // ---------- helpers ----------

    private async Task<Guid> CreateRiskAsync(HttpClient client, Guid categoryId, string title)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks", new
        {
            title,
            riskCategoryId = categoryId,
            riskType = 0
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        return created!.Id;
    }

    private async Task<RiskDetailResponse> GetDetailAsync(HttpClient client, Guid riskId)
    {
        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RiskDetailResponse>(JsonOptions))!;
    }

    private async Task<string> GetRowVersionAsync(Guid riskId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var risk = await db.RiskRecords.AsNoTracking().SingleAsync(r => r.Id == riskId);
        return Convert.ToBase64String(risk.RowVersion);
    }

    private async Task<Guid> CreateAssessmentAsync(HttpClient client, Guid riskId, RiskReference reference, int likelihoodValue, int impactValue, int assessmentType, string rationale = "مبرر اختباري كافٍ للتقييم.")
    {
        var response = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments", new
        {
            assessmentType,
            likelihoodLevelId = reference.LikelihoodLevelIds[likelihoodValue - 1],
            impacts = new[] { new { impactDimensionId = reference.ImpactDimensionId, impactLevelId = reference.ImpactLevelIdsByValue[impactValue] } },
            rationale
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        return created!.Id;
    }

    private async Task<string> GetAssessmentRowVersionAsync(Guid riskId, Guid assessmentId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var assessment = await db.RiskAssessments.AsNoTracking().SingleAsync(a => a.Id == assessmentId && a.RiskRecordId == riskId);
        return Convert.ToBase64String(assessment.RowVersion);
    }

    private async Task<string> GetMatrixRowVersionAsync(Guid matrixId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var matrix = await db.RiskAssessmentMatrices.AsNoTracking().SingleAsync(m => m.Id == matrixId);
        return Convert.ToBase64String(matrix.RowVersion);
    }

    private async Task<string> GetPlanRowVersionAsync(Guid planId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var plan = await db.RiskTreatmentPlans.AsNoTracking().SingleAsync(p => p.Id == planId);
        return Convert.ToBase64String(plan.RowVersion);
    }

    private async Task<string> GetActionRowVersionAsync(Guid actionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var action = await db.RiskTreatmentActions.AsNoTracking().SingleAsync(a => a.Id == actionId);
        return Convert.ToBase64String(action.RowVersion);
    }

    private async Task<string> GetReviewRowVersionAsync(Guid reviewId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var review = await db.RiskReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
        return Convert.ToBase64String(review.RowVersion);
    }

    private async Task SubmitPlanCommandAsync(HttpClient client, Guid riskId, Guid planId, string command)
    {
        var rowVersion = await GetPlanRowVersionAsync(planId);
        var response = await client.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/treatments/{planId}/command",
            new { command, rowVersion });
        response.EnsureSuccessStatusCode();
    }

    private async Task SubmitActionCommandAsync(HttpClient client, Guid riskId, Guid planId, Guid actionId, string command)
    {
        var rowVersion = await GetActionRowVersionAsync(actionId);
        var response = await client.PostAsJsonAsync(
            $"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/treatments/{planId}/actions/{actionId}/command",
            new { command, rowVersion });
        response.EnsureSuccessStatusCode();
    }

    private async Task ApproveInherentAssessmentAsync(HttpClient creator, HttpClient approver, Guid riskId, RiskReference reference, int likelihoodValue, int impactValue)
    {
        var assessmentId = await CreateAssessmentAsync(creator, riskId, reference, likelihoodValue, impactValue, assessmentType: 0);
        var submit = await creator.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments/{assessmentId}/submit", new { rowVersion = await GetAssessmentRowVersionAsync(riskId, assessmentId) });
        submit.EnsureSuccessStatusCode();
        var review = await approver.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments/{assessmentId}/review", new { approve = true, rowVersion = await GetAssessmentRowVersionAsync(riskId, assessmentId) });
        review.EnsureSuccessStatusCode();
        var approve = await approver.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments/{assessmentId}/approve", new { rowVersion = await GetAssessmentRowVersionAsync(riskId, assessmentId) });
        approve.EnsureSuccessStatusCode();
    }

    private async Task CreateAndApproveResidualAssessmentAsync(HttpClient creator, HttpClient approver, Guid riskId, RiskReference reference)
    {
        var assessmentId = await CreateAssessmentAsync(creator, riskId, reference, likelihoodValue: 1, impactValue: 1, assessmentType: 2 /* Residual */);
        var submit = await creator.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments/{assessmentId}/submit", new { rowVersion = await GetAssessmentRowVersionAsync(riskId, assessmentId) });
        submit.EnsureSuccessStatusCode();
        var review = await approver.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments/{assessmentId}/review", new { approve = true, rowVersion = await GetAssessmentRowVersionAsync(riskId, assessmentId) });
        review.EnsureSuccessStatusCode();
        var approve = await approver.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments/{assessmentId}/approve", new { rowVersion = await GetAssessmentRowVersionAsync(riskId, assessmentId) });
        approve.EnsureSuccessStatusCode();
    }

    private async Task<Guid> SeedResourceInFacilityAsync(Guid facilityId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var asset = new Baseera.Domain.Resources.ResourceAsset
        {
            OrganizationId = SeedIds.Organization,
            OwnershipOrganizationId = SeedIds.Organization,
            ResourceType = Baseera.Domain.Resources.ResourceType.OperationalEquipment,
            AssetCode = $"AST-{Guid.NewGuid():N}"[..16],
            DisplayName = "أصل اختبار",
            OperationalFacilityId = facilityId,
            CurrentStatus = Baseera.Domain.Resources.ResourceStatus.Available,
            Condition = Baseera.Domain.Resources.ResourceCondition.Good,
            Criticality = Baseera.Domain.Resources.ResourceCriticality.Low
        };
        db.Add(asset);
        await db.SaveChangesAsync();
        return asset.Id;
    }

    private async Task<Guid> SeedActiveMatrixDirectAsync(string code)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var matrix = new RiskAssessmentMatrix
        {
            OrganizationId = SeedIds.Organization,
            Code = code,
            Name = "مصفوفة تحقق",
            Version = 1,
            Status = MatrixStatus.Active,
            ScoreFormula = ScoreFormulaType.LikelihoodTimesMaximumImpact,
            EffectiveFromUtc = DateTimeOffset.UtcNow.AddDays(-30),
            IsDefault = true
        };
        matrix.RatingBands.Add(new RiskRatingBand { Code = "ANY", LabelAr = "أي درجة", MinimumScore = 1, MaximumScore = 25, Severity = RiskRatingSeverity.Low, ColorToken = "info" });
        db.Add(matrix);
        await db.SaveChangesAsync();
        return matrix.Id;
    }

    private async Task<RiskReference> SeedRiskReferenceAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();

        var categoryCode = $"CAT-{Guid.NewGuid():N}"[..12];
        var category = new RiskCategory
        {
            OrganizationId = SeedIds.Organization,
            Code = categoryCode,
            NameAr = "تصنيف اختبار",
            IsActive = true
        };
        db.Add(category);

        // Get-or-create: RiskManagementIntegrationCollection shares one fixture database, and
        // ImpactDimensionConfiguration enforces a unique filtered index on (OrganizationId, Code) — this
        // helper must not assume it is the only test seeding the "SEC" dimension for this organization.
        var dimension = await db.ImpactDimensions.FirstOrDefaultAsync(d => d.OrganizationId == SeedIds.Organization && d.Code == "SEC")
            ?? new ImpactDimension
            {
                OrganizationId = SeedIds.Organization,
                Code = "SEC",
                NameAr = "أمني",
                IsActive = true
            };
        if (db.Entry(dimension).State == EntityState.Detached)
        {
            db.Add(dimension);
        }

        var matrix = new RiskAssessmentMatrix
        {
            OrganizationId = SeedIds.Organization,
            Code = $"MTX-{Guid.NewGuid():N}"[..12],
            Name = "مصفوفة اختبار",
            Version = 1,
            Status = MatrixStatus.Active,
            ScoreFormula = ScoreFormulaType.LikelihoodTimesMaximumImpact,
            EffectiveFromUtc = DateTimeOffset.UtcNow.AddDays(-1),
            IsDefault = true
        };

        var likelihoodLevels = Enumerable.Range(1, 5)
            .Select(i => new LikelihoodLevel { Code = $"L{i}", Name = $"احتمالية {i}", NumericValue = i, DisplayOrder = i })
            .ToList();
        foreach (var level in likelihoodLevels)
        {
            matrix.LikelihoodLevels.Add(level);
        }

        var impactLevels = Enumerable.Range(1, 5)
            .Select(i => new ImpactLevel { ImpactDimensionId = dimension.Id, Code = $"I{i}", Name = $"أثر {i}", NumericValue = i, DisplayOrder = i })
            .ToList();
        foreach (var level in impactLevels)
        {
            matrix.ImpactLevels.Add(level);
        }

        var bands = new[]
        {
            new RiskRatingBand { Code = "LOW", LabelAr = "منخفضة", MinimumScore = 1, MaximumScore = 5, Severity = RiskRatingSeverity.Low, ColorToken = "info", ReviewFrequencyDays = 180 },
            new RiskRatingBand { Code = "MED", LabelAr = "متوسطة", MinimumScore = 5, MaximumScore = 12, Severity = RiskRatingSeverity.Medium, ColorToken = "warn", ReviewFrequencyDays = 90 },
            new RiskRatingBand { Code = "HIGH", LabelAr = "عالية", MinimumScore = 12, MaximumScore = 20, Severity = RiskRatingSeverity.High, ColorToken = "danger", ReviewFrequencyDays = 30 },
            new RiskRatingBand { Code = "CRIT", LabelAr = "حرجة", MinimumScore = 20, MaximumScore = 25, Severity = RiskRatingSeverity.Critical, ColorToken = "critical", ReviewFrequencyDays = 7 }
        };
        foreach (var band in bands)
        {
            matrix.RatingBands.Add(band);
        }

        db.Add(matrix);
        await db.SaveChangesAsync();

        return new RiskReference(
            category.Id,
            categoryCode,
            matrix.Id,
            likelihoodLevels.Select(l => l.Id).ToList(),
            dimension.Id,
            impactLevels.ToDictionary(l => l.NumericValue, l => l.Id));
    }

    private sealed record CreateResponse(Guid Id);

    private sealed record RiskReference(
        Guid CategoryId,
        string CategoryCode,
        Guid MatrixId,
        IReadOnlyList<Guid> LikelihoodLevelIds,
        Guid ImpactDimensionId,
        IReadOnlyDictionary<int, Guid> ImpactLevelIdsByValue);

    private sealed record RiskDetailResponse(
        string RiskCode,
        int Status,
        int ReopenedCount,
        DateTimeOffset? ClosedAtUtc,
        RiskScoreExplanationResponse? CurrentAssessment,
        IReadOnlyList<string> AllowedActions,
        string RowVersion);

    private sealed record RiskScoreExplanationResponse(decimal CalculatedScore, string RatingBandCode);

    private sealed record ImportResultResponse(int TotalRows, int ValidRows, int RejectedRows, int DuplicateRows, int AppliedRows);

    private sealed record DataQualityIssueResponse(string Code, int Count);

    private sealed record DataQualityResponse(IReadOnlyList<DataQualityIssueResponse> Issues);
}
