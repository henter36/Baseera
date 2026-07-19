namespace Baseera.Application.Audit;

using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Domain.Audit;
using Baseera.Domain.Identity;

public sealed record AuditLogDto(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset OccurredAtSaudi,
    string? UserDisplayName,
    string Action,
    string Module,
    string EntityType,
    string? EntityId,
    string Outcome,
    bool IsSensitiveView);

public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> ListAsync(PagedQuery query, string? module, CancellationToken cancellationToken = default);
}

/// <summary>
/// Phase A.1: national audit listing is limited to Audit.View holders with Global or Headquarters scope.
/// Regional/facility users cannot browse the national audit stream. Structured scope fields are deferred.
/// </summary>
public sealed class AuditQueryService(IBaseeraDbContext db, ICurrentUser currentUser) : IAuditQueryService
{
    public Task<PagedResult<AuditLogDto>> ListAsync(PagedQuery query, string? module, CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(PermissionCodes.AuditView))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية عرض سجل التدقيق.");
        }

        if (!currentUser.IsGlobalScope && !currentUser.HasHeadquartersScope)
        {
            throw new UnauthorizedAccessException("عرض سجل التدقيق الوطني مقصور على نطاق Global أو Headquarters.");
        }

        var q = db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(module))
        {
            q = q.Where(a => a.Module == module);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(a =>
                (a.EntityId != null && a.EntityId.Contains(term)) ||
                a.Action.Contains(term) ||
                a.EntityType.Contains(term));
        }

        var total = q.Count();
        var items = q.OrderByDescending(a => a.OccurredAtUtc)
            .Skip(query.Skip)
            .Take(query.Take)
            .AsEnumerable()
            .Select(a => new AuditLogDto(
                a.Id,
                a.OccurredAtUtc,
                TimeZones.ToSaudi(a.OccurredAtUtc),
                a.UserDisplayName,
                a.Action,
                a.Module,
                a.EntityType,
                a.EntityId,
                a.Outcome,
                a.IsSensitiveView))
            .ToList();

        return Task.FromResult(new PagedResult<AuditLogDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.Take,
            TotalCount = total
        });
    }
}
