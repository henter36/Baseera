namespace Baseera.Application.Audit;

using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Domain.Audit;

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

public sealed class AuditQueryService(IBaseeraDbContext db, ICurrentUser currentUser) : IAuditQueryService
{
    public Task<PagedResult<AuditLogDto>> ListAsync(PagedQuery query, string? module, CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission("Audit.View"))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية عرض سجل التدقيق.");
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
