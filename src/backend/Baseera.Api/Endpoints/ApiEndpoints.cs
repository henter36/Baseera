namespace Baseera.Api.Endpoints;

using Baseera.Api.Authorization;
using Baseera.Application.Abstractions;
using Baseera.Application.Attachments;
using Baseera.Application.Audit;
using Baseera.Application.Common;
using Baseera.Application.CorrectiveActions;
using Baseera.Application.Identity;
using Baseera.Application.Notes;
using Baseera.Application.Organization;
using Baseera.Domain.Attachments;
using Baseera.Domain.Notes;
using FluentValidation;

public static class ApiEndpoints
{
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
        MapCorrectiveActionEndpoints(api);

        return api;
    }

    private static void MapCorrectiveActionEndpoints(RouteGroupBuilder api)
    {
        var actions = api.MapGroup("/corrective-actions");

        actions.MapGet("/", async ([AsParameters] CorrectiveActionListQueryParams query, ICorrectiveActionQueryService queries, CancellationToken ct) =>
            Results.Ok(await queries.ListAsync(query.ToQuery(), ct))).RequireAuthorization(AuthPolicies.CorrectiveActionsView);

        actions.MapGet("/{id:guid}", async (Guid id, ICorrectiveActionQueryService queries, CancellationToken ct) =>
        {
            var item = await queries.GetDetailAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsView);

        actions.MapPut("/{id:guid}", async (Guid id, UpdateCorrectiveActionRequest request, IValidator<UpdateCorrectiveActionRequest> validator, ICorrectiveActionCommandService commands, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await commands.UpdateAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsUpdate);

        actions.MapPost("/{id:guid}/submit", async (Guid id, TransitionCorrectiveActionRequest request, IValidator<TransitionCorrectiveActionRequest> validator, ICorrectiveActionCommandService commands, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await commands.SubmitAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsUpdate);

        actions.MapPost("/{id:guid}/assign", async (Guid id, AssignCorrectiveActionRequest request, IValidator<AssignCorrectiveActionRequest> validator, ICorrectiveActionAssignmentService assignments, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await assignments.AssignAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsAssign);

        actions.MapPost("/{id:guid}/start-work", async (Guid id, TransitionCorrectiveActionRequest request, IValidator<TransitionCorrectiveActionRequest> validator, ICorrectiveActionWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.StartWorkAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsStartWork);

        actions.MapPost("/{id:guid}/submit-for-verification", async (Guid id, CompleteCorrectiveActionRequest request, IValidator<CompleteCorrectiveActionRequest> validator, ICorrectiveActionWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.SubmitForVerificationAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsSubmitForVerification);

        actions.MapPost("/{id:guid}/return-for-rework", async (Guid id, TransitionCorrectiveActionRequest request, IValidator<TransitionCorrectiveActionRequest> validator, ICorrectiveActionWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.ReturnForReworkAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsReturnForRework);

        actions.MapPost("/{id:guid}/verify-completion", async (Guid id, CompleteCorrectiveActionRequest request, IValidator<CompleteCorrectiveActionRequest> validator, ICorrectiveActionWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.VerifyCompletionAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsVerifyCompletion);

        actions.MapPost("/{id:guid}/reopen", async (Guid id, ReopenCorrectiveActionRequest request, IValidator<ReopenCorrectiveActionRequest> validator, ICorrectiveActionWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.ReopenAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsReopen);

        actions.MapPost("/{id:guid}/cancel", async (Guid id, TransitionCorrectiveActionRequest request, IValidator<TransitionCorrectiveActionRequest> validator, ICorrectiveActionWorkflowService workflow, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await workflow.CancelAsync(id, request, ct));
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsCancel);

        actions.MapPost("/{id:guid}/archive", async (Guid id, TransitionCorrectiveActionRequest request, IValidator<TransitionCorrectiveActionRequest> validator, ICorrectiveActionCommandService commands, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            await commands.ArchiveAsync(id, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsArchive);

        actions.MapPost("/{id:guid}/restore", async (Guid id, TransitionCorrectiveActionRequest request, IValidator<TransitionCorrectiveActionRequest> validator, ICorrectiveActionCommandService commands, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            await commands.RestoreAsync(id, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthPolicies.CorrectiveActionsRestore);

        actions.MapGet("/{id:guid}/history", async (Guid id, ICorrectiveActionQueryService queries, CancellationToken ct) =>
            Results.Ok(await queries.GetHistoryAsync(id, ct))).RequireAuthorization(AuthPolicies.CorrectiveActionsView);

        actions.MapGet("/{id:guid}/assignments", async (Guid id, ICorrectiveActionQueryService queries, CancellationToken ct) =>
            Results.Ok(await queries.GetAssignmentsAsync(id, ct))).RequireAuthorization(AuthPolicies.CorrectiveActionsView);

        actions.MapGet("/{id:guid}/attachments", async (Guid id, IAttachmentAppService attachments, CancellationToken ct) =>
            Results.Ok(await attachments.ListForEntityAsync("CorrectiveAction", id, ct))).RequireAuthorization(AuthPolicies.CorrectiveActionsView);
    }

    private static void MapNotesEndpoints(RouteGroupBuilder api)
    {
        var notes = api.MapGroup("/notes");

        notes.MapGet("/", ListNotesAsync).RequireAuthorization(AuthPolicies.NotesView);

        notes.MapGet("/{id:guid}", async (Guid id, INoteQueryService queries, CancellationToken ct) =>
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

        notes.MapPut("/{id:guid}", async (Guid id, UpdateNoteRequest request, IValidator<UpdateNoteRequest> validator, INoteCommandService commands, CancellationToken ct) =>
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
