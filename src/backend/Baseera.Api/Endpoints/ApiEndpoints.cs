namespace Baseera.Api.Endpoints;

using Baseera.Api.Authorization;
using Baseera.Application.Abstractions;
using Baseera.Application.Attachments;
using Baseera.Application.Audit;
using Baseera.Application.Common;
using Baseera.Application.CorrectiveActions;
using Baseera.Application.Dashboard;
using Baseera.Application.Escalations;
using Baseera.Application.Forms;
using Baseera.Application.Forms.Campaigns;
using Baseera.Application.Forms.Compliance;
using Baseera.Application.Forms.Responses;
using Baseera.Application.Identity;
using Baseera.Application.Notes;
using Baseera.Application.Occupancy;
using Baseera.Application.Organization;
using Baseera.Application.Resources;
using Baseera.Application.SensitiveCustody;
using Baseera.Application.Workforce;
using Baseera.Application.Workspaces;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Notes;
using Baseera.Domain.Resources;
using FluentValidation;

public static class ApiEndpoints
{
    private const string EntityIdRoute = "/{id:guid}";
    private const string ArchiveSuffix = "/archive";
    private const string SummaryRoute = "/summary";

    public static RouteGroupBuilder MapBaseeraApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization();

        api.MapGet("/me", async (IUserAdminService users, CancellationToken ct) =>
            Results.Ok(await users.GetMeAsync(ct)));

        api.MapGet("/regions", async (int? page, int? pageSize, string? search, string? sortBy, bool? sortDesc, IOrganizationService org, CancellationToken ct) =>
            Results.Ok(await org.ListRegionsAsync(new PagedQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 20,
                Search = search,
                SortBy = sortBy,
                SortDesc = sortDesc ?? false
            }, ct))).RequireAuthorization(AuthPolicies.OrganizationView);

        api.MapGet("/regions/{id:guid}", async (Guid id, IOrganizationService org, CancellationToken ct) =>
        {
            var item = await org.GetRegionAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization(AuthPolicies.OrganizationView);

        api.MapPut("/regions/{id:guid}", async (Guid id, UpdateRegionRequest request, IValidator<UpdateRegionRequest> validator, IOrganizationService org, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await org.UpdateRegionAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.OrganizationManage);

        api.MapGet("/facilities", async (int? page, int? pageSize, string? search, Guid? regionId, IOrganizationService org, CancellationToken ct) =>
            Results.Ok(await org.ListFacilitiesAsync(new PagedQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 20,
                Search = search
            }, regionId, ct))).RequireAuthorization(AuthPolicies.OrganizationView);

        api.MapGet("/facilities/{id:guid}", async (Guid id, IOrganizationService org, CancellationToken ct) =>
        {
            var item = await org.GetFacilityAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization(AuthPolicies.OrganizationView);

        api.MapPost("/facilities", async (CreateFacilityRequest request, IValidator<CreateFacilityRequest> validator, IOrganizationService org, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var created = await org.CreateFacilityAsync(request, ct);
            return Results.Created($"/api/v1/facilities/{created.Id}", created);
        }).RequireAuthorization(AuthPolicies.OrganizationManage);

        api.MapGet("/facility-units", async (Guid? facilityId, int? page, int? pageSize, string? search, IOrganizationService org, CancellationToken ct) =>
        {
            if (facilityId is null)
            {
                return Results.BadRequest(new { detail = "facilityId مطلوب." });
            }

            return Results.Ok(await org.ListFacilityUnitsAsync(facilityId.Value, new PagedQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 50,
                Search = search
            }, ct));
        }).RequireAuthorization(AuthPolicies.OrganizationView);

        api.MapGet("/departments", async (int? page, int? pageSize, string? search, IOrganizationService org, CancellationToken ct) =>
            Results.Ok(await org.ListDepartmentsAsync(new PagedQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 50,
                Search = search
            }, ct))).RequireAuthorization(AuthPolicies.OrganizationView);

        api.MapGet("/users", async (int? page, int? pageSize, string? search, IUserAdminService users, CancellationToken ct) =>
            Results.Ok(await users.ListUsersAsync(new PagedQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 20,
                Search = search
            }, ct))).RequireAuthorization(AuthPolicies.UsersView);

        api.MapGet("/users/{id:guid}", async (Guid id, IUserAdminService users, CancellationToken ct) =>
        {
            var item = await users.GetUserAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization(AuthPolicies.UsersView);

        api.MapPost("/users/{id:guid}/roles", async (Guid id, AssignRoleRequest request, IUserAdminService users, CancellationToken ct) =>
        {
            await users.AssignRoleAsync(id, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.RolesManage);

        api.MapGet("/users/{id:guid}/scopes", async (Guid id, IUserAdminService users, CancellationToken ct) =>
            Results.Ok(await users.ListScopesAsync(id, ct))).RequireAuthorization(AuthPolicies.ScopesManage);

        api.MapPost("/users/{id:guid}/scopes", async (Guid id, AssignScopeRequest request, IValidator<AssignScopeRequest> validator, IUserAdminService users, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await users.AssignScopeAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.ScopesManage);

        api.MapGet("/roles", async (IUserAdminService users, CancellationToken ct) =>
            Results.Ok(await users.ListRolesAsync(ct))).RequireAuthorization(AuthPolicies.UsersView);

        api.MapGet("/audit-logs", async (int? page, int? pageSize, string? search, string? module, IAuditQueryService audit, CancellationToken ct) =>
            Results.Ok(await audit.ListAsync(new PagedQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 20,
                Search = search
            }, module, ct))).RequireAuthorization(AuthPolicies.AuditView);

        api.MapPost("/attachments", UploadAttachmentAsync).RequireAuthorization(AuthPolicies.AttachmentsUpload).DisableAntiforgery();

        api.MapGet("/attachments/{id:guid}/download", async (Guid id, IAttachmentAppService attachments, CancellationToken ct) =>
        {
            var (meta, content) = await attachments.DownloadAsync(id, ct);
            return Results.File(content, meta.ContentType, meta.OriginalFileName);
        }).RequireAuthorization(AuthPolicies.AttachmentsDownload);

        MapNotesEndpoints(api);
        MapNoteTypeEndpoints(api);
        MapNoteRoutingEndpoints(api);
        MapCorrectiveActionEndpoints(api);
        MapEscalationEndpoints(api);
        MapNotificationEndpoints(api);
        MapOperationalDashboardEndpoints(api);
        MapWorkspaceEndpoints(api);
        MapFormComplianceEndpoints(api);
        MapOccupancyEndpoints(api);
        MapResourceEndpoints(api);
        MapWorkforceEndpoints(api);
        MapSensitiveCustodyEndpoints(api);
        MapFormsEndpoints(api);
        MapFormTemplateEndpoints(api);
        MapFormCampaignEndpoints(api);
        MapFormResponseEndpoints(api);

        return api;
    }

    private static void MapOccupancyEndpoints(RouteGroupBuilder api)
    {
        var occupancy = api.MapGroup("/facilities/{facilityId:guid}/occupancy");

        occupancy.MapGet(SummaryRoute, async (
            Guid facilityId,
            DateTimeOffset? asOfUtc,
            IOccupancyQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetSummaryAsync(facilityId, asOfUtc ?? DateTimeOffset.UtcNow, ct)))
            .RequireAuthorization(AuthPolicies.OccupancyViewSummary);

        occupancy.MapGet("/units", async (
            Guid facilityId,
            DateTimeOffset? asOfUtc,
            IOccupancyQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetUnitBreakdownAsync(facilityId, asOfUtc ?? DateTimeOffset.UtcNow, ct)))
            .RequireAuthorization(AuthPolicies.OccupancyViewUnitBreakdown);

        occupancy.MapGet("/movements/summary", async (
            Guid facilityId,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            IOccupancyQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetMovementSummaryAsync(facilityId, fromUtc, toUtc, ct)))
            .RequireAuthorization(AuthPolicies.OccupancyViewMovements);

        occupancy.MapPost("/capacity", async (
            Guid facilityId,
            OccupancyCapacityRequest request,
            IOccupancyCommandService service,
            CancellationToken ct) =>
        {
            var id = await service.RecordCapacityAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/occupancy/capacity/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.OccupancyManageCapacity);

        occupancy.MapPost("/snapshots", async (
            Guid facilityId,
            OccupancySnapshotRequest request,
            IOccupancyCommandService service,
            CancellationToken ct) =>
        {
            var id = await service.RecordSnapshotAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/occupancy/snapshots/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.OccupancyRecordSnapshot);

        occupancy.MapPost("/movements/import", async (
            Guid facilityId,
            InmateMovementImportRequest request,
            IOccupancyCommandService service,
            CancellationToken ct) =>
            Results.Ok(await service.ImportMovementsAsync(facilityId, request, ct)))
            .RequireAuthorization(AuthPolicies.OccupancyImport);
    }

    private static void MapResourceEndpoints(RouteGroupBuilder api)
    {
        var resources = api.MapGroup("/facilities/{facilityId:guid}/resources");

        resources.MapGet(SummaryRoute, async (
            Guid facilityId,
            IResourceReadinessQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetSummaryAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.ResourcesViewSummary);

        resources.MapGet("/categories", async (
            Guid facilityId,
            IResourceReadinessQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetCategoriesAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.ResourcesViewAssets);

        resources.MapGet("/exceptions", async (
            Guid facilityId,
            int? limit,
            IResourceReadinessQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetExceptionsAsync(facilityId, limit ?? 20, ct)))
            .RequireAuthorization(AuthPolicies.ResourcesViewAssets);

        resources.MapGet("/units", async (
            Guid facilityId,
            IResourceReadinessQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetUnitDistributionAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.ResourcesViewAssets);

        resources.MapGet("/timeline", async (
            Guid facilityId,
            int? limit,
            IResourceReadinessQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetTimelineAsync(facilityId, limit ?? 50, ct)))
            .RequireAuthorization(AuthPolicies.ResourcesViewMaintenance);

        resources.MapGet("/assets", async (
            Guid facilityId,
            ResourceType? resourceType,
            string? search,
            int? pageSize,
            IResourceReadinessQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListAssetsAsync(facilityId, resourceType, search, pageSize ?? 50, ct)))
            .RequireAuthorization(AuthPolicies.ResourcesViewAssets);

        resources.MapGet("/assets/{assetId:guid}", async (
            Guid facilityId,
            Guid assetId,
            IResourceReadinessQueryService service,
            CancellationToken ct) =>
        {
            var asset = await service.GetAssetAsync(facilityId, assetId, ct);
            return asset is null ? Results.NotFound() : Results.Ok(asset);
        })
            .RequireAuthorization(AuthPolicies.ResourcesViewAssets);

        resources.MapPost("/assets", async (
            Guid facilityId,
            ResourceAssetCreateRequest request,
            IResourceAssetCommandService service,
            CancellationToken ct) =>
        {
            var id = await service.CreateAssetAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/resources/assets/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.ResourcesManageAssets);

        resources.MapPost("/assets/{assetId:guid}/status", async (
            Guid facilityId,
            Guid assetId,
            ResourceStatusChangeRequest request,
            IResourceAssetCommandService service,
            CancellationToken ct) =>
        {
            await service.ChangeStatusAsync(facilityId, assetId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.ResourcesManageStatus);

        resources.MapPost("/assets/{assetId:guid}/placements", async (
            Guid facilityId,
            Guid assetId,
            ResourcePlacementRequest request,
            IResourceAssetCommandService service,
            CancellationToken ct) =>
        {
            await service.PlaceAssetAsync(facilityId, assetId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.ResourcesManagePlacements);

        resources.MapPost("/maintenance", async (
            Guid facilityId,
            MaintenanceWorkOrderRequest request,
            IMaintenanceWorkOrderService service,
            CancellationToken ct) =>
        {
            var id = await service.CreateWorkOrderAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/resources/maintenance/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.ResourcesManageMaintenance);

        resources.MapPost("/requirements", async (
            Guid facilityId,
            ResourceRequirementRequest request,
            IResourceRequirementService service,
            CancellationToken ct) =>
        {
            var id = await service.RecordRequirementAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/resources/requirements/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.ResourcesManageRequirements);

        resources.MapPost("/import/preview", async (
            Guid facilityId,
            ResourceImportPreviewRequest request,
            IResourceImportService service,
            CancellationToken ct) =>
            Results.Ok(await service.PreviewAsync(facilityId, request, ct)))
            .RequireAuthorization(AuthPolicies.ResourcesImport);

        resources.MapPost("/import/confirm", async (
            Guid facilityId,
            ResourceImportPreviewRequest request,
            IResourceImportService service,
            CancellationToken ct) =>
            Results.Ok(await service.ConfirmAsync(facilityId, request, ct)))
            .RequireAuthorization(AuthPolicies.ResourcesImport);
    }

    private static void MapWorkforceEndpoints(RouteGroupBuilder api)
    {
        var workforce = api.MapGroup("/facilities/{facilityId:guid}/workforce");

        workforce.MapGet(SummaryRoute, async (
            Guid facilityId,
            IWorkforceReadinessQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetSummaryAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.WorkforceViewSummary);

        workforce.MapGet("/coverage", async (
            Guid facilityId,
            IWorkforceReadinessQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetCoverageAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.WorkforceViewCoverage);

        workforce.MapGet("/units", async (
            Guid facilityId,
            IWorkforceReadinessQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetUnitsAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.WorkforceViewCoverage);

        workforce.MapGet("/roles", async (
            Guid facilityId,
            IWorkforceReadinessQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetRolesAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.WorkforceViewMembers);

        workforce.MapGet("/members", async (
            Guid facilityId,
            string? search,
            int? pageSize,
            IWorkforceReadinessQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListMembersAsync(facilityId, search, pageSize ?? 50, ct)))
            .RequireAuthorization(AuthPolicies.WorkforceViewMembers);

        workforce.MapGet("/members/{memberId:guid}", async (
            Guid facilityId,
            Guid memberId,
            IWorkforceReadinessQueryService service,
            CancellationToken ct) =>
        {
            var member = await service.GetMemberAsync(facilityId, memberId, ct);
            return member is null ? Results.NotFound() : Results.Ok(member);
        }).RequireAuthorization(AuthPolicies.WorkforceViewMembers);

        workforce.MapPost("/members", async (
            Guid facilityId,
            WorkforceMemberCreateRequest request,
            IWorkforceMemberCommandService service,
            CancellationToken ct) =>
        {
            var id = await service.CreateMemberAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/workforce/members/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.WorkforceManageMembers);

        workforce.MapPut("/members/{memberId:guid}", async (
            Guid facilityId,
            Guid memberId,
            WorkforceMemberUpdateRequest request,
            IWorkforceMemberCommandService service,
            CancellationToken ct) =>
        {
            await service.UpdateMemberAsync(facilityId, memberId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.WorkforceManageMembers);

        workforce.MapPost("/assignments", async (
            Guid facilityId,
            WorkforceAssignmentRequest request,
            IWorkforceMemberCommandService service,
            CancellationToken ct) =>
        {
            var id = await service.CreateAssignmentAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/workforce/assignments/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.WorkforceManageAssignments);

        workforce.MapPost("/qualifications", async (
            Guid facilityId,
            WorkforceQualificationRequest request,
            IWorkforceMemberCommandService service,
            CancellationToken ct) =>
        {
            var id = await service.CreateQualificationAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/workforce/qualifications/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.WorkforceManageQualifications);

        workforce.MapGet("/qualifications", async (
            Guid facilityId,
            int? page,
            int? pageSize,
            IWorkforceReadinessQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListQualificationsAsync(facilityId, page ?? 1, pageSize ?? 50, ct)))
            .RequireAuthorization(AuthPolicies.WorkforceViewMembers);

        workforce.MapGet("/requirements", async (
            Guid facilityId,
            IStaffingRequirementService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListRequirementsAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.WorkforceViewCoverage);

        workforce.MapPost("/requirements", async (
            Guid facilityId,
            StaffingRequirementRequest request,
            IStaffingRequirementService service,
            CancellationToken ct) =>
        {
            var id = await service.RecordRequirementAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/workforce/requirements/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.WorkforceManageRequirements);

        workforce.MapGet("/rosters", async (
            Guid facilityId,
            IDutyRosterService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListRostersAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.WorkforceViewCoverage);

        workforce.MapPost("/rosters", async (
            Guid facilityId,
            DutyRosterCreateRequest request,
            IDutyRosterService service,
            CancellationToken ct) =>
        {
            var id = await service.CreateRosterAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/workforce/rosters/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.WorkforceManageRosters);

        workforce.MapPost("/rosters/{rosterId:guid}/assignments", async (
            Guid facilityId,
            Guid rosterId,
            DutyRosterAssignmentRequest request,
            IDutyRosterService service,
            CancellationToken ct) =>
        {
            var id = await service.AddAssignmentAsync(facilityId, rosterId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/workforce/rosters/{rosterId}/assignments/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.WorkforceManageRosters);

        workforce.MapPost("/rosters/{rosterId:guid}/publish", async (
            Guid facilityId,
            Guid rosterId,
            IDutyRosterService service,
            CancellationToken ct) =>
        {
            await service.PublishAsync(facilityId, rosterId, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.WorkforceManageRosters);

        workforce.MapPost("/availability", async (
            Guid facilityId,
            WorkforceAvailabilityRequest request,
            IWorkforceAvailabilityService service,
            CancellationToken ct) =>
        {
            var id = await service.RecordAvailabilityAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/workforce/availability/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.WorkforceRecordAvailability);

        workforce.MapPost("/import/preview", async (
            Guid facilityId,
            WorkforceImportPreviewRequest request,
            IWorkforceImportService service,
            CancellationToken ct) =>
            Results.Ok(await service.PreviewAsync(facilityId, request, ct)))
            .RequireAuthorization(AuthPolicies.WorkforceImport);

        workforce.MapPost("/import/confirm", async (
            Guid facilityId,
            WorkforceImportPreviewRequest request,
            IWorkforceImportService service,
            CancellationToken ct) =>
            Results.Ok(await service.ConfirmAsync(facilityId, request, ct)))
            .RequireAuthorization(AuthPolicies.WorkforceImport);

        workforce.MapGet("/data-quality", async (
            Guid facilityId,
            IWorkforceReadinessQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetDataQualityAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.WorkforceViewSummary);

        workforce.MapGet("/critical-positions", async (
            Guid facilityId,
            IWorkforceCriticalPositionQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListCriticalPositionsAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.WorkforceViewCoverage);

        workforce.MapGet("/export", async (
            Guid facilityId,
            string? search,
            int? pageSize,
            IWorkforceExportService service,
            CancellationToken ct) =>
        {
            var file = await service.ExportAsync(facilityId, search, pageSize ?? WorkforceExportOptions.DefaultLimit, ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).RequireAuthorization(AuthPolicies.WorkforceExport);

        workforce.MapGet("/reconciliation", async (
            Guid facilityId,
            int? page,
            int? pageSize,
            IWorkforceReconciliationService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListReconciliationAsync(facilityId, page ?? 1, pageSize ?? 50, ct)))
            .RequireAuthorization(AuthPolicies.WorkforceReconcile);

        workforce.MapPost("/reconciliation/{itemId}/resolve", async (
            Guid facilityId,
            string itemId,
            WorkforceReconciliationResolveRequest request,
            IWorkforceReconciliationService service,
            CancellationToken ct) =>
        {
            await service.ResolveReconciliationAsync(facilityId, itemId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.WorkforceReconcile);
    }

    private static void MapSensitiveCustodyEndpoints(RouteGroupBuilder api)
    {
        var custody = api.MapGroup("/facilities/{facilityId:guid}/sensitive-custody");

        custody.MapGet(SummaryRoute, async (
            Guid facilityId,
            ISensitiveCustodyReadinessService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetSummaryAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.SensitiveCustodyViewSummary);

        custody.MapGet("/weapons", async (
            Guid facilityId,
            string? search,
            int? pageSize,
            IWeaponAssetQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListWeaponsAsync(facilityId, search, pageSize ?? 50, ct)))
            .RequireAuthorization(AuthPolicies.SensitiveCustodyViewWeapons);

        custody.MapGet("/weapons/{weaponId:guid}", async (
            Guid facilityId,
            Guid weaponId,
            IWeaponAssetQueryService service,
            CancellationToken ct) =>
        {
            var weapon = await service.GetWeaponAsync(facilityId, weaponId, ct);
            return weapon is null ? Results.NotFound() : Results.Ok(weapon);
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyViewWeapons);

        custody.MapPost("/weapons", async (
            Guid facilityId,
            WeaponAssetCreateRequest request,
            IWeaponAssetCommandService service,
            CancellationToken ct) =>
        {
            var id = await service.CreateWeaponAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/sensitive-custody/weapons/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyManageWeapons);

        custody.MapPut("/weapons/{weaponId:guid}", async (
            Guid facilityId,
            Guid weaponId,
            WeaponAssetUpdateRequest request,
            IWeaponAssetCommandService service,
            CancellationToken ct) =>
        {
            await service.UpdateWeaponAsync(facilityId, weaponId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyManageWeapons);

        custody.MapGet("/transactions", async (
            Guid facilityId,
            int? page,
            int? pageSize,
            ICustodyTransactionService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListTransactionsAsync(facilityId, page ?? 1, pageSize ?? 50, ct)))
            .RequireAuthorization(AuthPolicies.SensitiveCustodyViewCustodyTransactions);

        custody.MapPost("/transactions", async (
            Guid facilityId,
            CustodyTransactionCreateRequest request,
            ICustodyTransactionService service,
            CancellationToken ct) =>
        {
            var id = await service.CreateTransactionAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/sensitive-custody/transactions/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyIssueWeapons);

        custody.MapPost("/transactions/{transactionId:guid}/approve", async (
            Guid facilityId,
            Guid transactionId,
            SensitiveCustodyTransitionRequest request,
            ICustodyTransactionService service,
            CancellationToken ct) =>
        {
            await service.ApproveAsync(facilityId, transactionId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyApproveTransactions);

        custody.MapPost("/transactions/{transactionId:guid}/handover", async (
            Guid facilityId,
            Guid transactionId,
            SensitiveCustodyTransitionRequest request,
            ICustodyTransactionService service,
            CancellationToken ct) =>
        {
            await service.HandoverAsync(facilityId, transactionId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyIssueWeapons);

        custody.MapPost("/transactions/{transactionId:guid}/receive", async (
            Guid facilityId,
            Guid transactionId,
            SensitiveCustodyTransitionRequest request,
            ICustodyTransactionService service,
            CancellationToken ct) =>
        {
            await service.ReceiveAsync(facilityId, transactionId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyReceiveWeapons);

        custody.MapPost("/transactions/{transactionId:guid}/complete", async (
            Guid facilityId,
            Guid transactionId,
            SensitiveCustodyTransitionRequest request,
            ICustodyTransactionService service,
            CancellationToken ct) =>
        {
            await service.CompleteAsync(facilityId, transactionId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyReceiveWeapons);

        custody.MapPost("/transactions/{transactionId:guid}/reverse", async (
            Guid facilityId,
            Guid transactionId,
            SensitiveCustodyTransitionRequest request,
            ICustodyTransactionService service,
            CancellationToken ct) =>
        {
            await service.ReverseAsync(facilityId, transactionId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyApproveTransactions);

        custody.MapGet("/ammunition", async (
            Guid facilityId,
            int? page,
            int? pageSize,
            IAmmunitionLedgerService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListLotsAsync(facilityId, page ?? 1, pageSize ?? 50, ct)))
            .RequireAuthorization(AuthPolicies.SensitiveCustodyViewAmmunition);

        custody.MapGet("/ammunition/ledger", async (
            Guid facilityId,
            int? page,
            int? pageSize,
            IAmmunitionLedgerService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListLedgerAsync(facilityId, page ?? 1, pageSize ?? 50, ct)))
            .RequireAuthorization(AuthPolicies.SensitiveCustodyViewAmmunition);

        custody.MapPost("/ammunition/transactions", async (
            Guid facilityId,
            AmmunitionTransactionRequest request,
            IAmmunitionLedgerService service,
            CancellationToken ct) =>
        {
            var id = await service.RecordTransactionAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/sensitive-custody/ammunition/transactions/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyManageAmmunition);

        custody.MapGet("/inventories", async (
            Guid facilityId,
            int? page,
            int? pageSize,
            IInventorySessionService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListInventoriesAsync(facilityId, page ?? 1, pageSize ?? 50, ct)))
            .RequireAuthorization(AuthPolicies.SensitiveCustodyConductInventory);

        custody.MapPost("/inventories", async (
            Guid facilityId,
            InventorySessionCreateRequest request,
            IInventorySessionService service,
            CancellationToken ct) =>
        {
            var id = await service.StartInventoryAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/sensitive-custody/inventories/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyConductInventory);

        custody.MapPost("/inventories/{inventoryId:guid}/entries", async (
            Guid facilityId,
            Guid inventoryId,
            InventoryEntryRequest request,
            IInventorySessionService service,
            CancellationToken ct) =>
        {
            var id = await service.AddEntryAsync(facilityId, inventoryId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/sensitive-custody/inventories/{inventoryId}/entries/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyConductInventory);

        custody.MapPost("/inventories/{inventoryId:guid}/complete", async (
            Guid facilityId,
            Guid inventoryId,
            SensitiveCustodyTransitionRequest request,
            IInventorySessionService service,
            CancellationToken ct) =>
        {
            await service.CompleteAsync(facilityId, inventoryId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyConductInventory);

        custody.MapPost("/inventories/{inventoryId:guid}/approve", async (
            Guid facilityId,
            Guid inventoryId,
            SensitiveCustodyTransitionRequest request,
            IInventorySessionService service,
            CancellationToken ct) =>
        {
            await service.ApproveInventoryAsync(facilityId, inventoryId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyApproveInventory);

        custody.MapGet("/inspections", async (
            Guid facilityId,
            int? page,
            int? pageSize,
            IWeaponInspectionService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListInspectionsAsync(facilityId, page ?? 1, pageSize ?? 50, ct)))
            .RequireAuthorization(AuthPolicies.SensitiveCustodyManageInspections);

        custody.MapPost("/inspections", async (
            Guid facilityId,
            WeaponInspectionRequest request,
            IWeaponInspectionService service,
            CancellationToken ct) =>
        {
            var id = await service.RecordInspectionAsync(facilityId, request, ct);
            return Results.Created($"/api/v1/facilities/{facilityId}/sensitive-custody/inspections/{id}", new { id });
        }).RequireAuthorization(AuthPolicies.SensitiveCustodyManageInspections);

        custody.MapGet("/data-quality", async (
            Guid facilityId,
            ISensitiveCustodyDataQualityService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetDataQualityAsync(facilityId, ct)))
            .RequireAuthorization(AuthPolicies.SensitiveCustodyViewSummary);

        custody.MapPost("/import/preview", async (
            Guid facilityId,
            SensitiveCustodyImportPreviewRequest request,
            ISensitiveCustodyImportService service,
            CancellationToken ct) =>
            Results.Ok(await service.PreviewAsync(facilityId, request, ct)))
            .RequireAuthorization(AuthPolicies.SensitiveCustodyImport);

        custody.MapPost("/import/confirm", async (
            Guid facilityId,
            SensitiveCustodyImportPreviewRequest request,
            ISensitiveCustodyImportService service,
            CancellationToken ct) =>
            Results.Ok(await service.ConfirmAsync(facilityId, request, ct)))
            .RequireAuthorization(AuthPolicies.SensitiveCustodyImport);

        custody.MapPost("/reconcile", async (
            Guid facilityId,
            ISensitiveCustodyReconciliationService service,
            CancellationToken ct) =>
            Results.Ok(new { count = await service.ReconcileAsync(facilityId, ct) }))
            .RequireAuthorization(AuthPolicies.SensitiveCustodyReconcile);
    }

    private static void MapFormsEndpoints(RouteGroupBuilder api)
    {
        var forms = api.MapGroup("/forms");

        forms.MapGet("/governance-policy", async (IFormGovernanceService service, CancellationToken ct) =>
            Results.Ok(await service.GetPolicyAsync(ct)))
            .RequireAuthorization(AuthPolicies.FormsManageGovernance);

        forms.MapPut("/governance-policy", async (
            UpdateFormGovernancePolicyRequest request,
            IValidator<UpdateFormGovernancePolicyRequest> validator,
            IFormGovernanceService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.UpdatePolicyAsync(request, ct));
        }).RequireAuthorization(AuthPolicies.FormsManageGovernance);

        forms.MapGet("/", async ([AsParameters] FormListQueryParams query, IFormQueryService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(query.ToQuery(), ct)))
            .RequireAuthorization(AuthPolicies.FormsView);

        forms.MapGet(EntityIdRoute, async (Guid id, IFormQueryService service, CancellationToken ct) =>
        {
            var item = await service.GetDetailAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization(AuthPolicies.FormsView);

        forms.MapPost("/", async (
            CreateFormRequest request,
            IValidator<CreateFormRequest> validator,
            IFormCommandService commands,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var created = await commands.CreateDraftAsync(request, ct);
            return Results.Created($"/api/v1/forms/{created.Id}", created);
        }).RequireAuthorization(AuthPolicies.FormsCreate);

        forms.MapPut(EntityIdRoute, async (
            Guid id,
            UpdateFormRequest request,
            IValidator<UpdateFormRequest> validator,
            IFormCommandService commands,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await commands.UpdateAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsUpdateDraft);

        forms.MapPost("/{id:guid}/submit-review", async (
            Guid id,
            FormTransitionRequest request,
            IValidator<FormTransitionRequest> validator,
            IFormWorkflowService workflow,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.SubmitForReviewAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsSubmitForReview);

        forms.MapPost("/{id:guid}/request-changes", async (
            Guid id,
            FormTransitionRequest request,
            IValidator<FormTransitionRequest> validator,
            IFormWorkflowService workflow,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.RequestChangesAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsRequestChanges);

        forms.MapPost("/{id:guid}/approve", async (
            Guid id,
            FormTransitionRequest request,
            IValidator<FormTransitionRequest> validator,
            IFormWorkflowService workflow,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.ApproveAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsApprove);

        forms.MapPost("/{id:guid}/reject", async (
            Guid id,
            FormTransitionRequest request,
            FormRejectTransitionRequestValidator validator,
            IFormWorkflowService workflow,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.RejectAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsReject);

        forms.MapPost("/{id:guid}/archive", async (
            Guid id,
            FormTransitionRequest request,
            FormArchiveTransitionRequestValidator validator,
            IFormCommandService commands,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            await commands.ArchiveAsync(id, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.FormsArchive);

        forms.MapPost("/{id:guid}/restore", async (
            Guid id,
            FormTransitionRequest request,
            IValidator<FormTransitionRequest> validator,
            IFormCommandService commands,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            await commands.RestoreAsync(id, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.FormsRestore);

        forms.MapGet("/{id:guid}/review-decisions", async (Guid id, IFormQueryService service, CancellationToken ct) =>
            Results.Ok(await service.GetReviewDecisionsAsync(id, ct)))
            .RequireAuthorization(AuthPolicies.FormsView);

        forms.MapGet("/{id:guid}/retention-status", async (Guid id, IFormQueryService service, CancellationToken ct) =>
            Results.Ok(await service.GetRetentionStatusAsync(id, ct)))
            .RequireAuthorization(AuthPolicies.FormsView);

        forms.MapGet("/{id:guid}/access-grants", async (Guid id, IFormAccessGrantService service, CancellationToken ct) =>
            Results.Ok(await service.ListGrantsAsync(id, ct)))
            .RequireAuthorization(AuthPolicies.FormsManageAccess);

        forms.MapPost("/{id:guid}/access-grants", async (
            Guid id,
            CreateFormAccessGrantRequest request,
            IValidator<CreateFormAccessGrantRequest> validator,
            IFormAccessGrantService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var created = await service.CreateGrantAsync(id, request, ct);
            return Results.Created($"/api/v1/forms/{id}/access-grants/{created.Id}", created);
        }).RequireAuthorization(AuthPolicies.FormsManageAccess);

        forms.MapPost("/{id:guid}/access-grants/{grantId:guid}/revoke", async (
            Guid id,
            Guid grantId,
            FormTransitionRequest request,
            IValidator<FormTransitionRequest> validator,
            IFormAccessGrantService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            await service.RevokeGrantAsync(id, grantId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.FormsManageAccess);

        forms.MapGet("/{formId:guid}/versions", async (Guid formId, IFormVersionService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(formId, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewVersionHistory);

        forms.MapGet("/{formId:guid}/versions/{versionId:guid}", async (Guid formId, Guid versionId, IFormVersionService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(formId, versionId, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewVersionHistory);

        forms.MapPost("/{formId:guid}/versions", async (
            Guid formId,
            CreateFormVersionRequest? request,
            IFormVersionService service,
            CancellationToken ct) =>
        {
            var created = await service.CreateAsync(formId, request ?? new CreateFormVersionRequest(null), ct);
            return Results.Created($"/api/v1/forms/{formId}/versions/{created.Id}", created);
        }).RequireAuthorization(AuthPolicies.FormsUpdateDraft);

        forms.MapPost("/{formId:guid}/versions/{versionId:guid}/clone", async (Guid formId, Guid versionId, IFormVersionService service, CancellationToken ct) =>
        {
            var created = await service.CloneAsync(formId, versionId, ct);
            return Results.Created($"/api/v1/forms/{formId}/versions/{created.Id}", created);
        }).RequireAuthorization(AuthPolicies.FormsCloneVersion);

        forms.MapPut("/{formId:guid}/versions/{versionId:guid}/schema", async (
            Guid formId, Guid versionId, SaveFormSchemaRequest request,
            IValidator<SaveFormSchemaRequest> validator, IFormVersionService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.SaveSchemaAsync(formId, versionId, request, autosave: false, ct));
        }).RequireAuthorization(AuthPolicies.FormsUpdateDraft);

        forms.MapPost("/{formId:guid}/versions/{versionId:guid}/autosave", async (
            Guid formId, Guid versionId, SaveFormSchemaRequest request,
            IValidator<SaveFormSchemaRequest> validator, IFormVersionService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.SaveSchemaAsync(formId, versionId, request, autosave: true, ct));
        }).RequireAuthorization(AuthPolicies.FormsUpdateDraft);

        forms.MapPost("/{formId:guid}/versions/{versionId:guid}/validate", async (
            Guid formId, Guid versionId, SaveFormSchemaRequest request,
            IValidator<SaveFormSchemaRequest> validator, IFormVersionService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.ValidateAsync(formId, versionId, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsView);

        forms.MapPost("/{formId:guid}/versions/{versionId:guid}/submit-review", async (
            Guid formId, Guid versionId, FormVersionTransitionRequest request,
            IValidator<FormVersionTransitionRequest> validator, IFormVersionService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.SubmitForReviewAsync(formId, versionId, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsSubmitForReview);

        forms.MapPost("/{formId:guid}/versions/{versionId:guid}/request-changes", async (
            Guid formId, Guid versionId, FormVersionTransitionRequest request,
            IValidator<FormVersionTransitionRequest> validator, IFormVersionService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.RequestChangesAsync(formId, versionId, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsRequestChanges);

        forms.MapPost("/{formId:guid}/versions/{versionId:guid}/reject", async (
            Guid formId, Guid versionId, FormVersionTransitionRequest request,
            IValidator<FormVersionTransitionRequest> validator, IFormVersionService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.RejectAsync(formId, versionId, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsReject);

        forms.MapPost("/{formId:guid}/versions/{versionId:guid}/reopen", async (
            Guid formId, Guid versionId, FormVersionTransitionRequest request,
            IValidator<FormVersionTransitionRequest> validator, IFormVersionService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.ReopenAsync(formId, versionId, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsUpdateDraft);

        forms.MapPost("/{formId:guid}/versions/{versionId:guid}/approve-lock", async (
            Guid formId, Guid versionId, FormVersionTransitionRequest request,
            IValidator<FormVersionTransitionRequest> validator, IFormVersionService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.ApproveAndLockAsync(formId, versionId, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsApprove);

        forms.MapGet("/{formId:guid}/versions/{versionId:guid}/snapshot", async (Guid formId, Guid versionId, IFormVersionService service, CancellationToken ct) =>
            Results.Ok(await service.GetSnapshotAsync(formId, versionId, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewVersionHistory);

        forms.MapGet("/{formId:guid}/versions/{versionId:guid}/review-decisions", async (Guid formId, Guid versionId, IFormVersionService service, CancellationToken ct) =>
            Results.Ok(await service.GetReviewDecisionsAsync(formId, versionId, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewVersionHistory);

    }

    private static void MapFormTemplateEndpoints(RouteGroupBuilder api)
    {
        var templates = api.MapGroup("/form-templates");

        templates.MapGet("/", async (IFormTemplateService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)))
            .RequireAuthorization(AuthPolicies.FormsView);

        templates.MapPost("/", async (
            CreateFormTemplateRequest request,
            IValidator<CreateFormTemplateRequest> validator,
            IFormTemplateService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var created = await service.CreateFromLockedVersionAsync(request, ct);
            return Results.Created($"/api/v1/form-templates/{created.Id}", created);
        }).RequireAuthorization(AuthPolicies.FormsManageTemplates);

        templates.MapPost("/{templateId:guid}/create-form", async (
            Guid templateId,
            CreateFormFromTemplateRequest request,
            IValidator<CreateFormFromTemplateRequest> validator,
            IFormTemplateService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var created = await service.CreateFormFromTemplateAsync(templateId, request, ct);
            return Results.Created($"/api/v1/forms/{created.Id}", created);
        }).RequireAuthorization(AuthPolicies.FormsManageTemplates);
    }

    private static void MapFormCampaignEndpoints(RouteGroupBuilder api)
    {
        var campaigns = api.MapGroup("/form-campaigns");

        campaigns.MapGet("/", async (
            int? page, int? pageSize, string? search, FormCampaignStatus? status, Guid? formDefinitionId,
            IFormCampaignService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(new PagedQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 20,
                Search = search
            }, status, formDefinitionId, ct)))
            .RequireAuthorization(AuthPolicies.FormsView);

        campaigns.MapGet("/target-options/regions", async (
            int? page, int? pageSize, string? search, IFormCampaignService service, CancellationToken ct) =>
            Results.Ok(await service.ListTargetOptionRegionsAsync(new PagedQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 50,
                Search = search
            }, ct)))
            .RequireAuthorization(AuthPolicies.FormsPreviewTargets);

        campaigns.MapGet("/target-options/facilities", async (
            int? page, int? pageSize, string? search, Guid? regionId, IFormCampaignService service, CancellationToken ct) =>
            Results.Ok(await service.ListTargetOptionFacilitiesAsync(regionId, new PagedQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 50,
                Search = search
            }, ct)))
            .RequireAuthorization(AuthPolicies.FormsPreviewTargets);

        campaigns.MapPost("/schedule-preview", async (
            FormCampaignScheduleRequest request,
            string? timeZoneId,
            IFormCampaignService service,
            CancellationToken ct) =>
            Results.Ok(await service.PreviewUpcomingAsync(request, timeZoneId, 10, ct)))
            .RequireAuthorization(AuthPolicies.FormsManageCampaigns);

        campaigns.MapGet("/{campaignId:guid}", async (Guid campaignId, IFormCampaignService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(campaignId, ct)))
            .RequireAuthorization(AuthPolicies.FormsView);

        campaigns.MapPost("/", async (
            CreateFormCampaignRequest request,
            IValidator<CreateFormCampaignRequest> validator,
            IFormCampaignService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/v1/form-campaigns/{created.Id}", created);
        }).RequireAuthorization(AuthPolicies.FormsManageCampaigns);

        campaigns.MapPut("/{campaignId:guid}", async (
            Guid campaignId,
            UpdateFormCampaignRequest request,
            IValidator<UpdateFormCampaignRequest> validator,
            IFormCampaignService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.UpdateAsync(campaignId, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsManageCampaigns);

        campaigns.MapPost("/{campaignId:guid}/clone", async (Guid campaignId, IFormCampaignService service, CancellationToken ct) =>
            Results.Ok(await service.CloneAsync(campaignId, ct)))
            .RequireAuthorization(AuthPolicies.FormsManageCampaigns);

        campaigns.MapPost("/{campaignId:guid}/target-preview", async (Guid campaignId, IFormCampaignService service, CancellationToken ct) =>
            Results.Ok(await service.PreviewTargetsAsync(campaignId, ct)))
            .RequireAuthorization(AuthPolicies.FormsPreviewTargets);

        campaigns.MapPost("/{campaignId:guid}/publish", async (
            Guid campaignId,
            PublishFormCampaignRequest request,
            IValidator<PublishFormCampaignRequest> validator,
            IFormCampaignService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.PublishAsync(campaignId, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsPublish);

        campaigns.MapPost("/{campaignId:guid}/pause", async (
            Guid campaignId,
            FormCampaignTransitionRequest request,
            IValidator<FormCampaignTransitionRequest> validator,
            IFormCampaignService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.PauseAsync(campaignId, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsPauseCampaign);

        campaigns.MapPost("/{campaignId:guid}/resume", async (
            Guid campaignId,
            FormCampaignTransitionRequest request,
            IValidator<FormCampaignTransitionRequest> validator,
            IFormCampaignService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.ResumeAsync(campaignId, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsPauseCampaign);

        campaigns.MapPost("/{campaignId:guid}/cancel", async (
            Guid campaignId,
            FormCampaignTransitionRequest request,
            IValidator<FormCampaignTransitionRequest> validator,
            IFormCampaignService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.CancelAsync(campaignId, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsCancelCampaign);

        campaigns.MapPost("/{campaignId:guid}/complete", async (
            Guid campaignId,
            FormCampaignTransitionRequest request,
            IValidator<FormCampaignTransitionRequest> validator,
            IFormCampaignService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.CompleteAsync(campaignId, request, ct));
        }).RequireAuthorization(AuthPolicies.FormsPublish);

        campaigns.MapGet("/{campaignId:guid}/cycles", async (
            Guid campaignId, int? page, int? pageSize, IFormCampaignService service, CancellationToken ct) =>
            Results.Ok(await service.ListCyclesAsync(campaignId, new PagedQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 20
            }, ct)))
            .RequireAuthorization(AuthPolicies.FormsView);

        campaigns.MapGet("/{campaignId:guid}/cycles/{cycleId:guid}", async (
            Guid campaignId, Guid cycleId, IFormCampaignService service, CancellationToken ct) =>
            Results.Ok(await service.GetCycleAsync(campaignId, cycleId, ct)))
            .RequireAuthorization(AuthPolicies.FormsView);

        campaigns.MapGet("/{campaignId:guid}/cycles/{cycleId:guid}/assignments", async (
            Guid campaignId, Guid cycleId, int? page, int? pageSize, IFormCampaignService service, CancellationToken ct) =>
            Results.Ok(await service.ListAssignmentsAsync(campaignId, cycleId, new PagedQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 50
            }, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewCampaignAssignments);
    }


    private static void MapFormResponseEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/form-response-workspace", async (
            [AsParameters] FormResponseWorkspaceQuery query,
            IFormResponseService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListWorkspaceAsync(query, ct)))
            .RequireAuthorization(AuthPolicies.FormsRespond);

        api.MapGet("/form-assignments/{assignmentId:guid}/response", async (
            Guid assignmentId, IFormResponseService service, CancellationToken ct) =>
            Results.Ok(await service.GetAssignmentResponseAsync(assignmentId, ct)))
            .RequireAuthorization(AuthPolicies.FormsRespond);

        api.MapPut("/form-assignments/{assignmentId:guid}/response/draft", async (
            Guid assignmentId, FormResponseDraftSaveRequest request, IFormResponseService service, CancellationToken ct) =>
            Results.Ok(await service.SaveDraftAsync(assignmentId, request, ct)))
            .RequireAuthorization(AuthPolicies.FormsRespond);

        api.MapPost("/form-assignments/{assignmentId:guid}/response/validate", async (
            Guid assignmentId, FormResponseValidateRequest request, IFormResponseService service, CancellationToken ct) =>
            Results.Ok(await service.ValidateAsync(assignmentId, request, ct)))
            .RequireAuthorization(AuthPolicies.FormsRespond);

        api.MapPost("/form-assignments/{assignmentId:guid}/response/submit", async (
            Guid assignmentId, FormResponseSubmitRequest request, IFormResponseService service, CancellationToken ct) =>
            Results.Ok(await service.SubmitAsync(assignmentId, request, ct)))
            .RequireAuthorization(AuthPolicies.FormsRespond);

        api.MapGet("/form-response-reviews", async (
            [AsParameters] FormResponseReviewInboxQuery query,
            IFormResponseReviewService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListInboxAsync(query, ct)))
            .RequireAuthorization(AuthPolicies.FormsReviewResponses);

        api.MapGet("/form-responses/{responseId:guid}/review", async (
            Guid responseId, IFormResponseReviewService service, CancellationToken ct) =>
            Results.Ok(await service.GetReviewAsync(responseId, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewResponseDetail);

        api.MapPost("/form-responses/{responseId:guid}/review/start", async (
            Guid responseId, FormResponseCloseRequest request, IFormResponseReviewService service, CancellationToken ct) =>
        {
            await service.StartReviewAsync(responseId, request.RowVersion, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.FormsReviewResponses);

        api.MapPost("/form-responses/{responseId:guid}/return", async (
            Guid responseId, FormResponseReturnRequest request, IFormResponseReviewService service, CancellationToken ct) =>
        {
            await service.ReturnAsync(responseId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.FormsReviewResponses);

        api.MapPost("/form-responses/{responseId:guid}/approve", async (
            Guid responseId, FormResponseApproveRequest request, IFormResponseReviewService service, CancellationToken ct) =>
        {
            await service.ApproveAsync(responseId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.FormsApproveResponses);

        api.MapPost("/form-responses/{responseId:guid}/reject", async (
            Guid responseId, FormResponseRejectRequest request, IFormResponseReviewService service, CancellationToken ct) =>
        {
            await service.RejectAsync(responseId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.FormsReviewResponses);

        api.MapPost("/form-responses/{responseId:guid}/close", async (
            Guid responseId, FormResponseCloseRequest request, IFormResponseReviewService service, CancellationToken ct) =>
        {
            await service.CloseAsync(responseId, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.FormsCloseResponses);

        api.MapGet("/form-responses/{responseId:guid}/submissions", async (
            Guid responseId, IFormResponseReviewService service, CancellationToken ct) =>
            Results.Ok(await service.ListSubmissionsAsync(responseId, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewResponseDetail);

        api.MapGet("/form-responses/{responseId:guid}/submissions/{submissionNumber:int}", async (
            Guid responseId, int submissionNumber, IFormResponseReviewService service, CancellationToken ct) =>
            Results.Ok(await service.GetSubmissionAsync(responseId, submissionNumber, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewResponseDetail);

        api.MapGet("/form-responses/{responseId:guid}/history", async (
            Guid responseId, IFormResponseReviewService service, CancellationToken ct) =>
            Results.Ok(await service.GetHistoryAsync(responseId, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewResponseDetail);
    }

    private static void MapOperationalDashboardEndpoints(RouteGroupBuilder api)
    {
        var dashboard = api.MapGroup("/dashboard/operations");

        dashboard.MapGet(SummaryRoute, async (
            [AsParameters] OperationalDashboardQuery query,
            IOperationalDashboardQueryService service,
            CancellationToken ct) =>
            Results.Ok(
                await service.GetSummaryAsync(
                    query,
                    ct)));

        dashboard.MapGet("/trends", async (
            [AsParameters] OperationalDashboardQuery query,
            IOperationalDashboardQueryService service,
            CancellationToken ct) =>
            Results.Ok(
                await service.GetTrendsAsync(
                    query,
                    ct)))
            .RequireAuthorization(
                AuthPolicies.DashboardViewOperational);

        dashboard.MapGet("/breakdowns", async (
            [AsParameters] OperationalDashboardQuery query,
            IOperationalDashboardQueryService service,
            CancellationToken ct) =>
            Results.Ok(
                await service.GetBreakdownsAsync(
                    query,
                    ct)))
            .RequireAuthorization(
                AuthPolicies.DashboardViewOperational);

        dashboard.MapGet("/priority-queues", async (
            [AsParameters] OperationalDashboardQuery query,
            IOperationalDashboardQueryService service,
            CancellationToken ct) =>
            Results.Ok(
                await service.GetPriorityQueuesAsync(
                    query,
                    ct)));
    }

    private static void MapWorkspaceEndpoints(RouteGroupBuilder api)
    {
        var workspaces = api.MapGroup("/workspaces").RequireAuthorization(AuthPolicies.WorkspacesView);

        workspaces.MapGet("/{workspaceKey}", async (
            string workspaceKey,
            [AsParameters] WorkspaceQueryParams query,
            IWorkspaceQueryService service,
            CancellationToken ct) =>
            await ToWorkspaceResultAsync(() => service.GetWorkspaceAsync(query.ToRequest(workspaceKey), ct)));

        workspaces.MapGet("/{workspaceKey}/widgets", async (
            string workspaceKey,
            [AsParameters] WorkspaceQueryParams query,
            IWorkspaceQueryService service,
            CancellationToken ct) =>
            await ToWorkspaceResultAsync(() => service.GetWidgetsAsync(query.ToRequest(workspaceKey), ct)));

        workspaces.MapGet("/{workspaceKey}/widgets/{widgetKey}", async (
            string workspaceKey,
            string widgetKey,
            [AsParameters] WorkspaceQueryParams query,
            IWorkspaceQueryService service,
            CancellationToken ct) =>
            await ToWorkspaceResultAsync(() => service.GetWidgetAsync(query.ToRequest(workspaceKey), widgetKey, ct)));
    }

    private static async Task<IResult> ToWorkspaceResultAsync<T>(Func<Task<T?>> load)
        where T : class
    {
        try
        {
            var result = await load();
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { detail = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(title: "Forbidden", detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static void MapFormComplianceEndpoints(RouteGroupBuilder api)
    {
        var compliance = api.MapGroup("/form-compliance");

        compliance.MapGet(SummaryRoute, async (
            [AsParameters] FormComplianceQuery query,
            IFormComplianceQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetSummaryAsync(query, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewComplianceDashboard);

        compliance.MapGet("/regions", async (
            [AsParameters] FormComplianceQuery query,
            IFormComplianceQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetRegionsAsync(query, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewComplianceDashboard);

        compliance.MapGet("/facilities", async (
            [AsParameters] FormComplianceQuery query,
            IFormComplianceQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetFacilitiesAsync(query, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewComplianceDashboard);

        compliance.MapGet("/cycles", async (
            [AsParameters] FormComplianceQuery query,
            IFormComplianceQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetCyclesAsync(query, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewComplianceDashboard);

        compliance.MapGet("/pending", async (
            [AsParameters] FormComplianceQuery query,
            IFormComplianceQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetPendingAsync(query, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewComplianceDashboard);

        compliance.MapGet("/trend", async (
            [AsParameters] FormComplianceQuery query,
            IFormComplianceQueryService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetTrendAsync(query, ct)))
            .RequireAuthorization(AuthPolicies.FormsViewComplianceDashboard);

        compliance.MapGet("/export.csv", async (
            [AsParameters] FormComplianceQuery query,
            IFormComplianceQueryService service,
            CancellationToken ct) =>
        {
            var result = await service.ExportCsvAsync(query, ct);
            return Results.File(result.Content, result.ContentType, result.FileName);
        }).RequireAuthorization(AuthPolicies.FormsExportComplianceDashboard);
    }

    private static void MapNoteTypeEndpoints(RouteGroupBuilder api)
    {
        var noteTypes = api.MapGroup("/note-types");
        noteTypes.MapGet("/", async (INoteTypeManagementService service, CancellationToken ct) =>
            Results.Ok(await service.ListNoteTypesAsync(cancellationToken: ct))).RequireAuthorization(AuthPolicies.NotesView);

        noteTypes.MapGet(EntityIdRoute, async (Guid id, INoteTypeManagementService service, CancellationToken ct) =>
        {
            var item = await service.GetNoteTypeAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization(AuthPolicies.NotesView);

        noteTypes.MapPost("/", async (CreateNoteTypeRequest request, IValidator<CreateNoteTypeRequest> validator, INoteTypeManagementService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var created = await service.CreateNoteTypeAsync(request, ct);
            return Results.Created($"/api/v1/note-types/{created.Id}", created);
        }).RequireAuthorization(AuthPolicies.NotesManageTypes);

        noteTypes.MapPut(EntityIdRoute, async (Guid id, UpdateNoteTypeRequest request, IValidator<UpdateNoteTypeRequest> validator, INoteTypeManagementService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.UpdateNoteTypeAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesManageTypes);

        noteTypes.MapPost(EntityIdRoute + "/activate", async (Guid id, TransitionNoteRequest request, IValidator<TransitionNoteRequest> validator, INoteTypeManagementService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.ActivateNoteTypeAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesManageTypes);

        noteTypes.MapPost(EntityIdRoute + "/deactivate", async (Guid id, TransitionNoteRequest request, IValidator<TransitionNoteRequest> validator, INoteTypeManagementService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.DeactivateNoteTypeAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesManageTypes);

        api.MapGet("/roles/{id:guid}/note-type-grants", async (Guid id, INoteTypeManagementService service, CancellationToken ct) =>
            Results.Ok(await service.GetRoleGrantsAsync(id, ct))).RequireAuthorization(AuthPolicies.NotesManageRoleTypeAccess);

        api.MapPut("/roles/{id:guid}/note-type-grants", async (Guid id, ReplaceRoleNoteTypeGrantsRequest request, IValidator<ReplaceRoleNoteTypeGrantsRequest> validator, INoteTypeManagementService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.ReplaceRoleGrantsAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesManageRoleTypeAccess);

        api.MapGet("/users/{id:guid}/note-type-overrides", async (Guid id, INoteTypeManagementService service, CancellationToken ct) =>
            Results.Ok(await service.GetUserOverridesAsync(id, ct))).RequireAuthorization(AuthPolicies.NotesManageUserTypeOverrides);

        api.MapPut("/users/{id:guid}/note-type-overrides", async (Guid id, ReplaceUserNoteTypeOverridesRequest request, IValidator<ReplaceUserNoteTypeOverridesRequest> validator, INoteTypeManagementService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.ReplaceUserOverridesAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesManageUserTypeOverrides);

        api.MapGet("/users/{id:guid}/effective-note-type-access", async (Guid id, INoteTypeManagementService service, CancellationToken ct) =>
            Results.Ok(await service.GetEffectiveAccessAsync(id, ct))).RequireAuthorization(AuthPolicies.NotesManageUserTypeOverrides);

        api.MapGet("/users/{id:guid}/note-intake-profile", async (Guid id, INoteTypeManagementService service, CancellationToken ct) =>
            Results.Ok(await service.GetIntakeProfileAsync(id, ct))).RequireAuthorization(AuthPolicies.NotesManageIntakeProfiles);

        api.MapPut("/users/{id:guid}/note-intake-profile", async (Guid id, UpdateUserNoteIntakeProfileRequest request, IValidator<UpdateUserNoteIntakeProfileRequest> validator, INoteTypeManagementService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.UpdateIntakeProfileAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesManageIntakeProfiles);

        api.MapGet("/me/note-intake-context", async (INoteTypeManagementService service, CancellationToken ct) =>
            Results.Ok(await service.GetMyIntakeContextAsync(ct))).RequireAuthorization(AuthPolicies.NotesCreate);

        api.MapGet("/me/note-intake-context/facilities", async (Guid regionId, INoteTypeManagementService service, CancellationToken ct) =>
            Results.Ok(await service.GetMyIntakeFacilitiesAsync(regionId, ct))).RequireAuthorization(AuthPolicies.NotesCreate);

        api.MapGet("/me/note-types", async (INoteTypeAccessService service, CancellationToken ct) =>
            Results.Ok(await service.GetAccessibleNoteTypesAsync(NoteTypeCapability.View, ct))).RequireAuthorization(AuthPolicies.NotesView);

        api.MapGet("/me/note-type-access", async (INoteTypeAccessService service, ICurrentUser currentUser, CancellationToken ct) =>
            Results.Ok(await service.GetEffectiveAccessAsync(currentUser.UserId ?? Guid.Empty, ct))).RequireAuthorization(AuthPolicies.NotesView);
    }

    private static void MapEscalationEndpoints(RouteGroupBuilder api)
    {
        var policies = api.MapGroup("/escalation-policies");
        policies.MapGet("/", async ([AsParameters] EscalationPolicyQueryParams query, IEscalationPolicyService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(query.ToQuery(), ct))).RequireAuthorization(AuthPolicies.EscalationsView);

        policies.MapGet(EntityIdRoute, async (Guid id, IEscalationPolicyService service, CancellationToken ct) =>
        {
            var item = await service.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization(AuthPolicies.EscalationsView);

        policies.MapPost("/", async (CreateEscalationPolicyRequest request, IValidator<CreateEscalationPolicyRequest> validator, IEscalationPolicyService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/v1/escalation-policies/{created.Id}", created);
        }).RequireAuthorization(AuthPolicies.EscalationsManage);

        policies.MapPut(EntityIdRoute, async (Guid id, UpdateEscalationPolicyRequest request, IValidator<UpdateEscalationPolicyRequest> validator, IEscalationPolicyService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.UpdateAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.EscalationsManage);

        policies.MapPost(EntityIdRoute + "/activate", async (Guid id, RowVersionRequest request, IEscalationPolicyService service, CancellationToken ct) =>
            Results.Ok(await service.ActivateAsync(id, request, ct))).RequireAuthorization(AuthPolicies.EscalationsActivate);

        policies.MapPost(EntityIdRoute + "/deactivate", async (Guid id, RowVersionRequest request, IEscalationPolicyService service, CancellationToken ct) =>
            Results.Ok(await service.DeactivateAsync(id, request, ct))).RequireAuthorization(AuthPolicies.EscalationsActivate);

        policies.MapPost(EntityIdRoute + ArchiveSuffix, async (Guid id, RowVersionRequest request, IEscalationPolicyService service, CancellationToken ct) =>
        {
            await service.ArchiveAsync(id, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.EscalationsManage);

        policies.MapPost(EntityIdRoute + "/restore", async (Guid id, RowVersionRequest request, IEscalationPolicyService service, CancellationToken ct) =>
        {
            await service.RestoreAsync(id, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.EscalationsManage);

        policies.MapGet(EntityIdRoute + "/rules", async (Guid id, IEscalationRuleService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(id, ct))).RequireAuthorization(AuthPolicies.EscalationsView);

        policies.MapPost(EntityIdRoute + "/rules", async (Guid id, CreateEscalationRuleRequest request, IValidator<CreateEscalationRuleRequest> validator, IEscalationRuleService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Created($"/api/v1/escalation-policies/{id}/rules", await service.CreateAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.EscalationsManage);

        var rules = api.MapGroup("/escalation-rules");
        rules.MapPut(EntityIdRoute, async (Guid id, UpdateEscalationRuleRequest request, IValidator<UpdateEscalationRuleRequest> validator, IEscalationRuleService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.UpdateAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.EscalationsManage);

        rules.MapPost(EntityIdRoute + "/enable", async (Guid id, RowVersionRequest request, IEscalationRuleService service, CancellationToken ct) =>
            Results.Ok(await service.EnableAsync(id, request, ct))).RequireAuthorization(AuthPolicies.EscalationsManage);

        rules.MapPost(EntityIdRoute + "/disable", async (Guid id, RowVersionRequest request, IEscalationRuleService service, CancellationToken ct) =>
            Results.Ok(await service.DisableAsync(id, request, ct))).RequireAuthorization(AuthPolicies.EscalationsManage);

        var escalations = api.MapGroup("/escalations");
        escalations.MapPost("/run", async (IEscalationProcessor processor, CancellationToken ct) =>
            Results.Ok(await processor.RunAsync("manual-api", cancellationToken: ct))).RequireAuthorization(AuthPolicies.EscalationsRun);

        escalations.MapGet("/occurrences", async ([AsParameters] EscalationOccurrenceQueryParams query, IEscalationOccurrenceService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(query.ToQuery(), ct))).RequireAuthorization(AuthPolicies.EscalationsViewOccurrences);

        escalations.MapGet("/occurrences" + EntityIdRoute, async (Guid id, IEscalationOccurrenceService service, CancellationToken ct) =>
        {
            var item = await service.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization(AuthPolicies.EscalationsViewOccurrences);

        escalations.MapPost("/occurrences" + EntityIdRoute + "/retry", async (Guid id, IEscalationOccurrenceService service, CancellationToken ct) =>
        {
            await service.RetryAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.EscalationsRetryFailed);
    }

    private static void MapNotificationEndpoints(RouteGroupBuilder api)
    {
        var notifications = api.MapGroup("/notifications");
        notifications.MapGet("/", async ([AsParameters] NotificationQueryParams query, INotificationService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(query.ToQuery(), ct))).RequireAuthorization(AuthPolicies.NotificationsViewOwn);

        notifications.MapGet("/unread-count", async (INotificationService service, CancellationToken ct) =>
            Results.Ok(new { count = await service.GetUnreadCountAsync(ct) })).RequireAuthorization(AuthPolicies.NotificationsViewOwn);

        notifications.MapGet(EntityIdRoute, async (Guid id, INotificationService service, CancellationToken ct) =>
        {
            var item = await service.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization(AuthPolicies.NotificationsViewOwn);

        notifications.MapPost(EntityIdRoute + "/read", async (Guid id, RowVersionRequest request, INotificationService service, CancellationToken ct) =>
            Results.Ok(await service.MarkReadAsync(id, request, ct))).RequireAuthorization(AuthPolicies.NotificationsMarkRead);

        notifications.MapPost("/read-all", async (INotificationService service, CancellationToken ct) =>
            Results.Ok(new { count = await service.MarkAllReadAsync(ct) })).RequireAuthorization(AuthPolicies.NotificationsMarkRead);

        notifications.MapPost(EntityIdRoute + ArchiveSuffix, async (Guid id, RowVersionRequest request, INotificationService service, CancellationToken ct) =>
            Results.Ok(await service.ArchiveAsync(id, request, ct))).RequireAuthorization(AuthPolicies.NotificationsArchiveOwn);
    }

    private static void MapCorrectiveActionEndpoints(RouteGroupBuilder api)
    {
        var actions = api.MapGroup("/corrective-actions");

        actions.MapGet("/", async ([AsParameters] CorrectiveActionListQueryParams query, ICorrectiveActionQueryService queries, CancellationToken ct) =>
            Results.Ok(await queries.ListAsync(query.ToQuery(), ct))).RequireAuthorization(AuthPolicies.CorrectiveActionsView);

        actions.MapGet(EntityIdRoute, async (Guid id, ICorrectiveActionQueryService queries, CancellationToken ct) =>
        {
            var item = await queries.GetDetailAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsView);

        actions.MapPut(EntityIdRoute, async (Guid id, UpdateCorrectiveActionRequest request, IValidator<UpdateCorrectiveActionRequest> validator, ICorrectiveActionCommandService commands, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await commands.UpdateAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsUpdate);

        actions.MapPost(EntityIdRoute + "/submit", async (Guid id, TransitionCorrectiveActionRequest request, IValidator<TransitionCorrectiveActionRequest> validator, ICorrectiveActionCommandService commands, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await commands.SubmitAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsUpdate);

        actions.MapPost(EntityIdRoute + "/assign", async (Guid id, AssignCorrectiveActionRequest request, IValidator<AssignCorrectiveActionRequest> validator, ICorrectiveActionAssignmentService assignments, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await assignments.AssignAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsAssign);

        actions.MapPost(EntityIdRoute + "/start-work", async (Guid id, TransitionCorrectiveActionRequest request, IValidator<TransitionCorrectiveActionRequest> validator, ICorrectiveActionWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.StartWorkAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsStartWork);

        actions.MapPost(EntityIdRoute + "/submit-for-verification", async (Guid id, CompleteCorrectiveActionRequest request, IValidator<CompleteCorrectiveActionRequest> validator, ICorrectiveActionWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.SubmitForVerificationAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsSubmitForVerification);

        actions.MapPost(EntityIdRoute + "/return-for-rework", async (Guid id, TransitionCorrectiveActionRequest request, IValidator<TransitionCorrectiveActionRequest> validator, ICorrectiveActionWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.ReturnForReworkAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsReturnForRework);

        actions.MapPost(EntityIdRoute + "/verify-completion", async (Guid id, CompleteCorrectiveActionRequest request, IValidator<CompleteCorrectiveActionRequest> validator, ICorrectiveActionWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.VerifyCompletionAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsVerifyCompletion);

        actions.MapPost(EntityIdRoute + "/reopen", async (Guid id, ReopenCorrectiveActionRequest request, IValidator<ReopenCorrectiveActionRequest> validator, ICorrectiveActionWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.ReopenAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsReopen);

        actions.MapPost(EntityIdRoute + "/cancel", async (Guid id, TransitionCorrectiveActionRequest request, IValidator<TransitionCorrectiveActionRequest> validator, ICorrectiveActionWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.CancelAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsCancel);

        actions.MapPost(EntityIdRoute + ArchiveSuffix, async (Guid id, TransitionCorrectiveActionRequest request, IValidator<TransitionCorrectiveActionRequest> validator, ICorrectiveActionCommandService commands, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            await commands.ArchiveAsync(id, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsArchive);

        actions.MapPost(EntityIdRoute + "/restore", async (Guid id, TransitionCorrectiveActionRequest request, IValidator<TransitionCorrectiveActionRequest> validator, ICorrectiveActionCommandService commands, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            await commands.RestoreAsync(id, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsRestore);

        actions.MapGet(EntityIdRoute + "/history", async (Guid id, ICorrectiveActionQueryService queries, CancellationToken ct) =>
            Results.Ok(await queries.GetHistoryAsync(id, ct))).RequireAuthorization(AuthPolicies.CorrectiveActionsView);

        actions.MapGet(EntityIdRoute + "/assignments", async (Guid id, ICorrectiveActionQueryService queries, CancellationToken ct) =>
            Results.Ok(await queries.GetAssignmentsAsync(id, ct))).RequireAuthorization(AuthPolicies.CorrectiveActionsView);

        actions.MapGet(EntityIdRoute + "/attachments", async (Guid id, IAttachmentAppService attachments, CancellationToken ct) =>
            Results.Ok(await attachments.ListForEntityAsync("CorrectiveAction", id, ct))).RequireAuthorization(AuthPolicies.CorrectiveActionsView);
    }

    private static void MapNoteRoutingEndpoints(RouteGroupBuilder api)
    {
        var rules = api.MapGroup("/note-routing-rules");
        rules.MapGet("/", async (
            [AsParameters] NoteRoutingRuleQueryParams query,
            INoteRoutingService service,
            CancellationToken ct) =>
            Results.Ok(
                await service.ListRulesAsync(
                    query.ToQuery(),
                    ct)))
            .RequireAuthorization(AuthPolicies.NotesViewRouting);

        rules.MapGet(EntityIdRoute, async (Guid id, INoteRoutingService service, CancellationToken ct) =>
        {
            var item = await service.GetRuleAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization(AuthPolicies.NotesViewRouting);

        rules.MapPost("/", async (CreateNoteRoutingRuleRequest request, IValidator<CreateNoteRoutingRuleRequest> validator, INoteRoutingService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var created = await service.CreateRuleAsync(request, ct);
            return Results.Created($"/api/v1/note-routing-rules/{created.Id}", created);
        }).RequireAuthorization(AuthPolicies.NotesManageRoutingRules);

        rules.MapPut(EntityIdRoute, async (Guid id, UpdateNoteRoutingRuleRequest request, IValidator<UpdateNoteRoutingRuleRequest> validator, INoteRoutingService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.UpdateRuleAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesManageRoutingRules);

        rules.MapPost(EntityIdRoute + "/activate", async (Guid id, TransitionNoteRequest request, IValidator<TransitionNoteRequest> validator, INoteRoutingService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.ActivateRuleAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesActivateRoutingRules);

        rules.MapPost(EntityIdRoute + "/deactivate", async (Guid id, TransitionNoteRequest request, IValidator<TransitionNoteRequest> validator, INoteRoutingService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.DeactivateRuleAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesActivateRoutingRules);

        rules.MapPost(EntityIdRoute + ArchiveSuffix, async (Guid id, TransitionNoteRequest request, IValidator<TransitionNoteRequest> validator, INoteRoutingService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            await service.ArchiveRuleAsync(id, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.NotesManageRoutingRules);

        rules.MapPost(EntityIdRoute + "/restore", async (Guid id, TransitionNoteRequest request, IValidator<TransitionNoteRequest> validator, INoteRoutingService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.RestoreRuleAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesManageRoutingRules);

        rules.MapPost("/validate", async (CreateNoteRoutingRuleRequest request, IValidator<CreateNoteRoutingRuleRequest> validator, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(new { valid = true });
        }).RequireAuthorization(AuthPolicies.NotesManageRoutingRules);

        rules.MapPost("/preview", async (Guid noteId, PreviewNoteRoutingRequest request, INoteRoutingService service, CancellationToken ct) =>
            Results.Ok(await service.PreviewNoteAsync(noteId, request, ct))).RequireAuthorization(AuthPolicies.NotesViewRouting);

        api.MapPost("/notes/{id:guid}/routing/run", async (Guid id, RunNoteRoutingRequest request, IValidator<RunNoteRoutingRequest> validator, INoteRoutingService service, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await service.RunManualAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesRunRouting);

        api.MapPost("/notes/{id:guid}/routing/preview", async (Guid id, PreviewNoteRoutingRequest request, INoteRoutingService service, CancellationToken ct) =>
            Results.Ok(await service.PreviewNoteAsync(id, request, ct))).RequireAuthorization(AuthPolicies.NotesViewRouting);

        api.MapGet("/note-routing/effectiveness", async (DateTimeOffset? fromUtc, DateTimeOffset? toUtc, INoteRoutingService service, CancellationToken ct) =>
            Results.Ok(await service.GetEffectivenessAsync(new NoteRoutingEffectivenessQuery(fromUtc, toUtc), ct))).RequireAuthorization(AuthPolicies.NotesViewRoutingDiagnostics);
    }

    private static void MapNotesEndpoints(RouteGroupBuilder api)
    {
        var notes = api.MapGroup("/notes");

        notes.MapGet("/workspace", async ([AsParameters] NoteListQueryParams query, INoteWorkspaceQueryService workspace, CancellationToken ct) =>
            Results.Ok(await workspace.ListAsync(query.ToQuery(), ct))).RequireAuthorization(AuthPolicies.NotesView);

        notes.MapGet("/", ListNotesAsync).RequireAuthorization(AuthPolicies.NotesView);

        notes.MapGet("/{id:guid}/workspace", async (Guid id, INoteWorkspaceQueryService workspace, CancellationToken ct) =>
        {
            var item = await workspace.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization(AuthPolicies.NotesView);

        notes.MapGet(EntityIdRoute, async (Guid id, INoteQueryService queries, CancellationToken ct) =>
        {
            var item = await queries.GetDetailAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization(AuthPolicies.NotesView);

        notes.MapPost("/", async (CreateNoteRequest request, IValidator<CreateNoteRequest> validator, INoteCommandService commands, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var created = await commands.CreateDraftAsync(request, ct);
            return Results.Created($"/api/v1/notes/{created.Id}", created);
        }).RequireAuthorization(AuthPolicies.NotesCreate);

        notes.MapPut(EntityIdRoute, async (Guid id, UpdateNoteRequest request, IValidator<UpdateNoteRequest> validator, INoteCommandService commands, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await commands.UpdateAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesUpdate);

        notes.MapPost("/{id:guid}/submit", async (Guid id, TransitionNoteRequest request, IValidator<TransitionNoteRequest> validator, INoteCommandService commands, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await commands.SubmitAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesUpdate);

        notes.MapPost("/{id:guid}/assign", async (Guid id, AssignNoteRequest request, IValidator<AssignNoteRequest> validator, INoteAssignmentService assignments, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await assignments.AssignAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesAssign);

        notes.MapPost("/{id:guid}/start-work", async (Guid id, WorkflowActionRequest request, IValidator<WorkflowActionRequest> validator, INoteWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.StartWorkAsync(id, ToTransition(request), ct));
        }).RequireAuthorization(AuthPolicies.NotesStartWork);

        notes.MapPost("/{id:guid}/submit-for-verification", async (Guid id, WorkflowActionRequest request, IValidator<WorkflowActionRequest> validator, INoteWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.SubmitForVerificationAsync(id, ToTransition(request), ct));
        }).RequireAuthorization(AuthPolicies.NotesSubmitForVerification);

        notes.MapPost("/{id:guid}/return-for-rework", async (Guid id, TransitionNoteRequest request, IValidator<TransitionNoteRequest> validator, INoteWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.ReturnForReworkAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesReturnForRework);

        notes.MapPost("/{id:guid}/verify-closure", async (Guid id, CloseNoteRequest request, IValidator<CloseNoteRequest> validator, INoteWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.VerifyClosureAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesVerifyClosure);

        notes.MapPost("/{id:guid}/reopen", async (Guid id, ReopenNoteRequest request, IValidator<ReopenNoteRequest> validator, INoteWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.ReopenAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesReopen);

        notes.MapPost("/{id:guid}/cancel", async (Guid id, TransitionNoteRequest request, IValidator<TransitionNoteRequest> validator, INoteWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.CancelAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.NotesCancel);

        notes.MapPost("/{id:guid}/archive", async (Guid id, TransitionNoteRequest request, IValidator<TransitionNoteRequest> validator, INoteCommandService commands, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            await commands.ArchiveAsync(id, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.NotesArchive);

        notes.MapPost("/{id:guid}/restore", async (Guid id, TransitionNoteRequest request, IValidator<TransitionNoteRequest> validator, INoteCommandService commands, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            await commands.RestoreAsync(id, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.NotesRestore);

        notes.MapGet("/{id:guid}/history", async (Guid id, INoteQueryService queries, CancellationToken ct) =>
            Results.Ok(await queries.GetHistoryAsync(id, ct))).RequireAuthorization(AuthPolicies.NotesView);

        notes.MapGet("/{id:guid}/assignments", async (Guid id, INoteQueryService queries, CancellationToken ct) =>
            Results.Ok(await queries.GetAssignmentsAsync(id, ct))).RequireAuthorization(AuthPolicies.NotesView);

        notes.MapGet("/{id:guid}/eligible-assignees", async (Guid id, INoteEligibilityService eligibility, CancellationToken ct) =>
            Results.Ok(await eligibility.GetEligibleAssigneesAsync(id, ct))).RequireAuthorization(AuthPolicies.NotesAssign);

        notes.MapGet("/{id:guid}/eligible-reviewers", async (Guid id, INoteEligibilityService eligibility, CancellationToken ct) =>
            Results.Ok(await eligibility.GetEligibleReviewersAsync(id, ct))).RequireAuthorization(AuthPolicies.NotesVerifyClosure);

        // Metadata-only (no content); out-of-scope/missing notes surface as 404 via the same
        // KeyNotFoundException path AttachmentService uses for single-attachment downloads.
        notes.MapGet("/{id:guid}/attachments", async (Guid id, IAttachmentAppService attachments, CancellationToken ct) =>
            Results.Ok(await attachments.ListForEntityAsync("OperationalNote", id, ct))).RequireAuthorization(AuthPolicies.NotesView);

        notes.MapGet("/{id:guid}/corrective-actions", async (Guid id, [AsParameters] CorrectiveActionListQueryParams query, ICorrectiveActionQueryService actions, CancellationToken ct) =>
            Results.Ok(await actions.ListForNoteAsync(id, query.ToQuery(), ct))).RequireAuthorization(AuthPolicies.CorrectiveActionsView);

        notes.MapPost("/{id:guid}/corrective-actions", async (Guid id, CreateCorrectiveActionRequest request, IValidator<CreateCorrectiveActionRequest> validator, ICorrectiveActionCommandService actions, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var created = await actions.CreateDraftAsync(id, request, ct);
            return Results.Created($"/api/v1/corrective-actions/{created.Id}", created);
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsCreate);
    }

    private static async Task<IResult> ListNotesAsync(
        [AsParameters] NoteListQueryParams query,
        INoteQueryService queries,
        CancellationToken cancellationToken)
    {
        var result = await queries.ListAsync(query.ToQuery(), cancellationToken);
        return Results.Ok(result);
    }

    private static TransitionNoteRequest ToTransition(WorkflowActionRequest request) =>
        new(string.IsNullOrWhiteSpace(request.Reason) ? "—" : request.Reason.Trim(), request.RowVersion);

    private static async Task<IResult> UploadAttachmentAsync(HttpRequest http, IAttachmentAppService attachments, CancellationToken ct)
    {
        if (!http.HasFormContentType)
        {
            return Results.BadRequest(new { detail = "يجب إرسال multipart/form-data." });
        }

        var form = await http.ReadFormAsync(ct);
        var file = form.Files.GetFile("file");
        if (file is null)
        {
            return Results.BadRequest(new { detail = "الملف مطلوب." });
        }

        if (!Guid.TryParse(form["entityId"], out var entityId))
        {
            return Results.BadRequest(new { detail = "entityId غير صالح." });
        }

        var entityType = form["entityType"].ToString();
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return Results.BadRequest(new { detail = "entityType مطلوب." });
        }

        Enum.TryParse<ClassificationLevel>(form["classification"], true, out var classification);
        await using var stream = file.OpenReadStream();
        var created = await attachments.UploadAsync(new UploadAttachmentRequest
        {
            EntityType = entityType,
            EntityId = entityId,
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            Content = stream,
            SizeBytes = file.Length,
            Classification = classification,
            UploadReason = form["reason"]
        }, ct);

        return Results.Created($"/api/v1/attachments/{created.Id}", created);
    }
}
