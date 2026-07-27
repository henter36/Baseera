namespace Baseera.Application.RiskManagement;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.RiskManagement;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Surfaces cross-source conflicts for human reconciliation — e.g. a risk that was reported both manually
/// and through import with different owners — and records how each was resolved. Mirrors
/// ISensitiveCustodyReconciliationService's shape; this phase detects the one conflict class that is cheap
/// and unambiguous to compute today (duplicate RecurrenceKey within a facility) and records resolutions.
/// </summary>
public sealed class RiskReconciliationService(IBaseeraDbContext db, ICurrentUser currentUser, IOrganizationalScopeService scope, IAuditService audit)
    : RiskServiceBase(db, currentUser, scope, audit), IRiskReconciliationService
{
    public async Task<IReadOnlyList<RiskReconciliationItemDto>> ListAsync(Guid facilityId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksImport);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);

        var resolvedKeys = await Db.RiskReconciliationRecords.AsNoTracking()
            .Where(r => r.FacilityId == facilityId)
            .Select(r => r.ItemKey)
            .ToListAsync(cancellationToken);

        var groups = await Db.RiskRecords.AsNoTracking()
            .Where(r => r.FacilityId == facilityId)
            .GroupBy(r => r.RecurrenceKey)
            .Where(g => g.Count() > 1)
            .Select(g => new { Key = g.Key, Codes = g.Select(r => r.RiskCode).ToList() })
            .ToListAsync(cancellationToken);

        return groups
            .Where(g => !resolvedKeys.Contains(g.Key))
            .Select(g => new RiskReconciliationItemDto(g.Key, $"سجلات متشابهة: {string.Join(", ", g.Codes)}", "متوسطة"))
            .ToList();
    }

    public async Task ResolveAsync(Guid facilityId, RiskReconciliationResolveRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksImport);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("سبب المصالحة مطلوب.");
        }

        var record = new RiskReconciliationRecord
        {
            OrganizationId = facility.Region.OrganizationId,
            FacilityId = facilityId,
            ItemKey = request.ItemKey,
            Action = request.Action,
            Reason = request.Reason,
            ResolvedBy = ActorReference(),
            ResolvedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = ActorReference()
        };

        Db.Add(record);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskReconciled, nameof(RiskReconciliationRecord), record.Id, new { request.Action }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }
}
