namespace Baseera.Api.Endpoints;

using Baseera.Api.Authorization;
using Baseera.Application.Abstractions;
using Baseera.Application.RiskManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

internal static class RiskManagementEndpoints
{
    public static void MapRiskManagementEndpoints(RouteGroupBuilder api)
    {
        var risks = api.MapGroup("/facilities/{facilityId:guid}/risks");

        risks.MapGet("/summary", async (
            Guid facilityId,
            IRiskRegisterQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetSummaryAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.RisksViewSummary);

        risks.MapGet("/", async (
            Guid facilityId,
            [AsParameters] RiskListQueryParams query,
            IRiskRegisterQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListAsync(facilityId, query.ToFilters(), ct)))
            .RequireAuthorization(AuthPolicies.RisksView);

        risks.MapGet("/categories", async (
            Guid facilityId,
            IBaseeraDbContext db,
            IRiskRegisterQueryService service,
            CancellationToken ct) =>
        {
            var organizationId = await ResolveOrganizationIdAsync(db, facilityId, ct);
            return Results.Ok(await service.ListCategoriesAsync(organizationId, ct));
        }).RequireAuthorization(AuthPolicies.RisksViewSummary);

        risks.MapPost("/categories", async (
            Guid facilityId,
            RiskCategoryUpsertRequest request,
            IBaseeraDbContext db,
            IRiskCommandService service,
            CancellationToken ct) =>
        {
            var organizationId = await ResolveOrganizationIdAsync(db, facilityId, ct);
            var id = await service.CreateCategoryAsync(organizationId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/risks/categories/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.RisksManageCategories);

        risks.MapGet("/{riskId:guid}", async (
            Guid facilityId,
            Guid riskId,
            IRiskRegisterQueryService service,
            CancellationToken ct) =>
        {
            var risk = await service.GetAsync(facilityId, riskId, ct);
            return risk is null ? Results.NotFound() : Results.Ok(risk);
        }).RequireAuthorization(AuthPolicies.RisksView);

        risks.MapPost("/", async (
            Guid facilityId,
            RiskCreateRequest request,
            IRiskCommandService service,
            CancellationToken ct) =>
        {
            var id = await service.CreateAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/risks/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.RisksCreate);

        risks.MapPut("/{riskId:guid}", async (
            Guid facilityId,
            Guid riskId,
            RiskUpdateRequest request,
            IRiskCommandService service,
            CancellationToken ct) =>
        {
            await service.UpdateAsync(facilityId, riskId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.RisksUpdate);

        risks.MapPost("/{riskId:guid}/command", async (
            Guid facilityId,
            Guid riskId,
            RiskCommandRequest request,
            IRiskCommandService service,
            CancellationToken ct) =>
        {
            await service.ExecuteCommandAsync(facilityId, riskId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.RisksView);

        MapAssessmentEndpoints(risks);
        MapControlEndpoints(risks);
        MapTreatmentEndpoints(risks);
        MapReviewEndpoints(risks);
        MapSourceLinkEndpoints(risks);

        risks.MapGet("/interventions", async (
            Guid facilityId,
            int? limit,
            IRiskReadinessService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetInterventionsAsync(facilityId, limit ?? 20, ct)))
            .RequireAuthorization(AuthPolicies.RisksViewSummary);

        risks.MapGet("/data-quality", async (
            Guid facilityId,
            IRiskDataQualityService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetDataQualityAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.RisksViewSummary);

        risks.MapPost("/import/preview", async (
            Guid facilityId,
            RiskImportPreviewRequest request,
            IRiskImportService service,
            CancellationToken ct) =>
            Results.Ok(await service.PreviewAsync(facilityId, request, ct)))
            .RequireAuthorization(AuthPolicies.RisksImport);

        risks.MapPost("/import/confirm", async (
            Guid facilityId,
            RiskImportPreviewRequest request,
            IRiskImportService service,
            CancellationToken ct) =>
            Results.Ok(await service.ConfirmAsync(facilityId, request, ct)))
            .RequireAuthorization(AuthPolicies.RisksImport);

        risks.MapGet("/reconciliation", async (
            Guid facilityId,
            IRiskReconciliationService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.RisksImport);

        risks.MapPost("/reconciliation/resolve", async (
            Guid facilityId,
            RiskReconciliationResolveRequest request,
            IRiskReconciliationService service,
            CancellationToken ct) =>
        {
            await service.ResolveAsync(facilityId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.RisksImport);

        var matrices = api.MapGroup("/risk-matrices");

        matrices.MapGet("/", async (
            Guid organizationId,
            IRiskMatrixService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListAsync(organizationId, ct)))
            .RequireAuthorization(AuthPolicies.RisksView);

        matrices.MapPost("/", async (
            Guid organizationId,
            RiskMatrixCreateRequest request,
            IRiskMatrixService service,
            CancellationToken ct) =>
        {
            var id = await service.CreateAsync(organizationId, request, ct);
            return Results.Created($"/api/v1/risk-matrices/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.RisksManageMatrices);

        matrices.MapPost("/{matrixId:guid}/approve", async (
            Guid organizationId,
            Guid matrixId,
            RiskRowVersionRequest request,
            IRiskMatrixService service,
            CancellationToken ct) =>
        {
            await service.ApproveAsync(organizationId, matrixId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.RisksApproveMatrices);

        matrices.MapPost("/{matrixId:guid}/activate", async (
            Guid organizationId,
            Guid matrixId,
            RiskRowVersionRequest request,
            IRiskMatrixService service,
            CancellationToken ct) =>
        {
            await service.ActivateAsync(organizationId, matrixId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.RisksApproveMatrices);
    }

    private static void MapAssessmentEndpoints(RouteGroupBuilder risks)
    {
        var assessments = risks.MapGroup("/{riskId:guid}/assessments");

        assessments.MapGet("/", async (Guid facilityId, Guid riskId, IRiskAssessmentService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(facilityId, riskId, ct)))
            .RequireAuthorization(AuthPolicies.RisksView);

        assessments.MapPost("/", async (Guid facilityId, Guid riskId, RiskAssessmentCreateRequest request, IRiskAssessmentService service, CancellationToken ct) =>
        {
            var id = await service.CreateAsync(facilityId, riskId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/risks/{riskId}/assessments/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.RisksAssess);

        assessments.MapPost("/{assessmentId:guid}/submit", async (Guid facilityId, Guid riskId, Guid assessmentId, RiskRowVersionRequest request, IRiskAssessmentService service, CancellationToken ct) =>
        {
            await service.SubmitAsync(facilityId, riskId, assessmentId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.RisksAssess);

        assessments.MapPost("/{assessmentId:guid}/review", async (Guid facilityId, Guid riskId, Guid assessmentId, RiskAssessmentReviewRequest request, IRiskAssessmentService service, CancellationToken ct) =>
        {
            await service.ReviewAsync(facilityId, riskId, assessmentId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.RisksReviewAssessment);

        assessments.MapPost("/{assessmentId:guid}/approve", async (Guid facilityId, Guid riskId, Guid assessmentId, RiskAssessmentApproveRequest request, IRiskAssessmentService service, CancellationToken ct) =>
        {
            await service.ApproveAsync(facilityId, riskId, assessmentId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.RisksApproveAssessment);
    }

    private static void MapControlEndpoints(RouteGroupBuilder risks)
    {
        var controls = risks.MapGroup("/{riskId:guid}/controls");

        controls.MapGet("/", async (Guid facilityId, Guid riskId, IRiskControlService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(facilityId, riskId, ct)))
            .RequireAuthorization(AuthPolicies.RisksView);

        controls.MapPost("/", async (Guid facilityId, Guid riskId, RiskControlCreateRequest request, IRiskControlService service, CancellationToken ct) =>
        {
            var id = await service.CreateAsync(facilityId, riskId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/risks/{riskId}/controls/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.RisksManageControls);

        controls.MapPost("/{controlId:guid}/test", async (Guid facilityId, Guid riskId, Guid controlId, RiskControlTestRequest request, IRiskControlService service, CancellationToken ct) =>
        {
            await service.RecordTestAsync(facilityId, riskId, controlId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.RisksManageControls);
    }

    private static void MapTreatmentEndpoints(RouteGroupBuilder risks)
    {
        var treatments = risks.MapGroup("/{riskId:guid}/treatments");

        treatments.MapGet("/", async (Guid facilityId, Guid riskId, IRiskTreatmentService service, CancellationToken ct) =>
            Results.Ok(await service.ListPlansAsync(facilityId, riskId, ct)))
            .RequireAuthorization(AuthPolicies.RisksView);

        treatments.MapPost("/", async (Guid facilityId, Guid riskId, RiskTreatmentPlanCreateRequest request, IRiskTreatmentService service, CancellationToken ct) =>
        {
            var id = await service.CreatePlanAsync(facilityId, riskId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/risks/{riskId}/treatments/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.RisksManageTreatments);

        treatments.MapPost("/{planId:guid}/command", async (Guid facilityId, Guid riskId, Guid planId, RiskTreatmentPlanCommandRequest request, IRiskTreatmentService service, CancellationToken ct) =>
        {
            await service.ExecutePlanCommandAsync(facilityId, riskId, planId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.RisksManageTreatments);

        treatments.MapPost("/{planId:guid}/actions", async (Guid facilityId, Guid riskId, Guid planId, RiskTreatmentActionCreateRequest request, IRiskTreatmentService service, CancellationToken ct) =>
        {
            var id = await service.CreateActionAsync(facilityId, riskId, planId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/risks/{riskId}/treatments/{planId}/actions/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.RisksManageTreatments);

        treatments.MapPost("/{planId:guid}/actions/{actionId:guid}/command", async (Guid facilityId, Guid riskId, Guid planId, Guid actionId, RiskTreatmentActionCommandRequest request, IRiskTreatmentService service, CancellationToken ct) =>
        {
            await service.ExecuteActionCommandAsync(facilityId, riskId, planId, actionId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.RisksView);
    }

    private static void MapReviewEndpoints(RouteGroupBuilder risks)
    {
        var reviews = risks.MapGroup("/{riskId:guid}/reviews");

        reviews.MapGet("/", async (Guid facilityId, Guid riskId, IRiskReviewService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(facilityId, riskId, ct)))
            .RequireAuthorization(AuthPolicies.RisksView);

        reviews.MapPost("/", async (Guid facilityId, Guid riskId, RiskReviewRequestDto request, IRiskReviewService service, CancellationToken ct) =>
        {
            var id = await service.RequestAsync(facilityId, riskId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/risks/{riskId}/reviews/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.RisksView);

        reviews.MapPost("/{reviewId:guid}/decision", async (Guid facilityId, Guid riskId, Guid reviewId, RiskReviewDecisionRequest request, IRiskReviewService service, CancellationToken ct) =>
        {
            await service.DecideAsync(facilityId, riskId, reviewId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.RisksView);
    }

    private static void MapSourceLinkEndpoints(RouteGroupBuilder risks)
    {
        var sources = risks.MapGroup("/{riskId:guid}/sources");

        sources.MapGet("/", async (Guid facilityId, Guid riskId, IRiskSourceLinkService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(facilityId, riskId, ct)))
            .RequireAuthorization(AuthPolicies.RisksView);

        sources.MapPost("/", async (Guid facilityId, Guid riskId, RiskSourceLinkCreateRequest request, IRiskSourceLinkService service, CancellationToken ct) =>
        {
            var id = await service.AddAsync(facilityId, riskId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/risks/{riskId}/sources/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.RisksLinkSources);

        sources.MapDelete("/{linkId:guid}", async (Guid facilityId, Guid riskId, Guid linkId, [FromBody] RiskSourceLinkRemoveRequest request, IRiskSourceLinkService service, CancellationToken ct) =>
        {
            await service.RemoveAsync(facilityId, riskId, linkId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.RisksLinkSources);
    }

    private static async Task<Guid> ResolveOrganizationIdAsync(IBaseeraDbContext db, Guid facilityId, CancellationToken cancellationToken)
    {
        var organizationId = await db.Facilities
            .Where(f => f.Id == facilityId)
            .Select(f => f.Region.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);
        return organizationId == Guid.Empty
            ? throw new KeyNotFoundException("السجن غير موجود.")
            : organizationId;
    }
}
