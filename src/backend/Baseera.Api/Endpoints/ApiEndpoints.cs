namespace Baseera.Api.Endpoints;

using Baseera.Application.Abstractions;
using Baseera.Application.Attachments;
using Baseera.Application.Audit;
using Baseera.Application.Common;
using Baseera.Application.Identity;
using Baseera.Application.Organization;
using Baseera.Domain.Attachments;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

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
            }, ct)));

        api.MapGet("/regions/{id:guid}", async (Guid id, IOrganizationService org, CancellationToken ct) =>
        {
            var item = await org.GetRegionAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        api.MapPut("/regions/{id:guid}", async (Guid id, UpdateRegionRequest request, IValidator<UpdateRegionRequest> validator, IOrganizationService org, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await org.UpdateRegionAsync(id, request, ct));
        });

        api.MapGet("/facilities", async (int? page, int? pageSize, string? search, Guid? regionId, IOrganizationService org, CancellationToken ct) =>
            Results.Ok(await org.ListFacilitiesAsync(new PagedQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 20,
                Search = search
            }, regionId, ct)));

        api.MapGet("/facilities/{id:guid}", async (Guid id, IOrganizationService org, CancellationToken ct) =>
        {
            var item = await org.GetFacilityAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        api.MapPost("/facilities", async (CreateFacilityRequest request, IValidator<CreateFacilityRequest> validator, IOrganizationService org, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var created = await org.CreateFacilityAsync(request, ct);
            return Results.Created($"/api/v1/facilities/{created.Id}", created);
        });

        api.MapGet("/users", async (int? page, int? pageSize, string? search, IUserAdminService users, CancellationToken ct) =>
            Results.Ok(await users.ListUsersAsync(new PagedQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 20,
                Search = search
            }, ct)));

        api.MapGet("/users/{id:guid}", async (Guid id, IUserAdminService users, CancellationToken ct) =>
        {
            var item = await users.GetUserAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        api.MapPost("/users/{id:guid}/roles", async (Guid id, AssignRoleRequest request, IUserAdminService users, CancellationToken ct) =>
        {
            await users.AssignRoleAsync(id, request, ct);
            return Results.NoContent();
        });

        api.MapGet("/users/{id:guid}/scopes", async (Guid id, IUserAdminService users, CancellationToken ct) =>
            Results.Ok(await users.ListScopesAsync(id, ct)));

        api.MapPost("/users/{id:guid}/scopes", async (Guid id, AssignScopeRequest request, IValidator<AssignScopeRequest> validator, IUserAdminService users, CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            return Results.Ok(await users.AssignScopeAsync(id, request, ct));
        });

        api.MapGet("/roles", async (IUserAdminService users, CancellationToken ct) =>
            Results.Ok(await users.ListRolesAsync(ct)));

        api.MapGet("/audit-logs", async (int? page, int? pageSize, string? search, string? module, IAuditQueryService audit, CancellationToken ct) =>
            Results.Ok(await audit.ListAsync(new PagedQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 20,
                Search = search
            }, module, ct)));

        api.MapPost("/attachments", async (HttpRequest http, IAttachmentAppService attachments, CancellationToken ct) =>
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
        }).DisableAntiforgery();

        api.MapGet("/attachments/{id:guid}/download", async (Guid id, IAttachmentAppService attachments, CancellationToken ct) =>
        {
            var (meta, content) = await attachments.DownloadAsync(id, ct);
            return Results.File(content, meta.ContentType, meta.OriginalFileName);
        });

        return api;
    }
}
