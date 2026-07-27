namespace Baseera.Application.RiskManagement;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.RiskManagement;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Typed evidence/source links — never a generic JSON blob. Scope is verified before linking for the
/// entity types this codebase already models (Note, CorrectiveAction, ResourceAsset, WorkforceMember,
/// RiskRecord). Types belonging to domains not yet built in Baseera (Project, EmergencyPlan, Decision,
/// Occurrence, WorkforceCoverageGap/QualificationIssue, SensitiveCustodyDiscrepancy, DataQualityIssue) are
/// accepted with existence left unverified — this is a documented, honest gap (see
/// docs/phase-d6-risk-source-linking.md), not silently pretended away.
/// </summary>
public sealed class RiskSourceLinkService(IBaseeraDbContext db, ICurrentUser currentUser, IOrganizationalScopeService scope, IAuditService audit)
    : RiskServiceBase(db, currentUser, scope, audit), IRiskSourceLinkService
{
    private static readonly HashSet<RiskSourceEntityType> ScopeCheckedTypes =
    [
        RiskSourceEntityType.Note, RiskSourceEntityType.CorrectiveAction, RiskSourceEntityType.ResourceAsset,
        RiskSourceEntityType.WorkforceCoverageGap, RiskSourceEntityType.WorkforceQualificationIssue, RiskSourceEntityType.RiskRecord
    ];

    public async Task<IReadOnlyList<RiskSourceLinkDto>> ListAsync(Guid facilityId, Guid riskId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksView);
        await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);

        var links = await Db.RiskSourceLinks.AsNoTracking()
            .Where(l => l.RiskRecordId == riskId)
            .OrderByDescending(l => l.AddedAtUtc)
            .ToListAsync(cancellationToken);

        return links.Select(l => new RiskSourceLinkDto(l.Id, l.SourceEntityType, l.SourceEntityId, l.RelationshipType, l.AddedAtUtc, l.AddedBy, l.Rationale)).ToList();
    }

    public async Task<Guid> AddAsync(Guid facilityId, Guid riskId, RiskSourceLinkCreateRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksLinkSources);
        await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);

        if (request.SourceEntityId == Guid.Empty)
        {
            throw new InvalidOperationException("معرّف الكيان المصدر مطلوب.");
        }

        await EnsureSourceInScopeAsync(request.SourceEntityType, request.SourceEntityId, facilityId, cancellationToken);

        var duplicate = await Db.RiskSourceLinks.AnyAsync(l => l.RiskRecordId == riskId
            && l.SourceEntityType == request.SourceEntityType
            && l.SourceEntityId == request.SourceEntityId
            && l.RelationshipType == request.RelationshipType, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("هذا الرابط موجود بالفعل بنفس نوع العلاقة.");
        }

        var link = new RiskSourceLink
        {
            RiskRecordId = riskId,
            SourceEntityType = request.SourceEntityType,
            SourceEntityId = request.SourceEntityId,
            RelationshipType = request.RelationshipType,
            AddedAtUtc = DateTimeOffset.UtcNow,
            AddedBy = ActorReference(),
            Rationale = request.Rationale,
            CreatedBy = ActorReference()
        };

        Db.Add(link);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskSourceLinked, nameof(RiskSourceLink), link.Id, new { link.SourceEntityType, link.RelationshipType }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return link.Id;
    }

    public async Task RemoveAsync(Guid facilityId, Guid riskId, Guid linkId, RiskSourceLinkRemoveRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksLinkSources);
        await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.RemovalReason))
        {
            throw new InvalidOperationException("سبب إزالة الرابط مطلوب.");
        }

        var link = await Db.RiskSourceLinks.FirstOrDefaultAsync(l => l.Id == linkId && l.RiskRecordId == riskId, cancellationToken)
            ?? throw new KeyNotFoundException("الرابط غير موجود.");

        // Soft-delete only — evidence/source links are never historically erased.
        link.IsDeleted = true;
        link.DeletedAtUtc = DateTimeOffset.UtcNow;
        link.DeletedBy = ActorReference();
        link.RemovalReason = request.RemovalReason;
        Db.Update(link);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskSourceUnlinked, nameof(RiskSourceLink), link.Id, new { request.RemovalReason }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSourceInScopeAsync(RiskSourceEntityType type, Guid entityId, Guid facilityId, CancellationToken cancellationToken)
    {
        if (!ScopeCheckedTypes.Contains(type))
        {
            return;
        }

        var inScope = type switch
        {
            RiskSourceEntityType.Note => await Db.OperationalNotes.AnyAsync(n => n.Id == entityId && n.FacilityId == facilityId, cancellationToken),
            RiskSourceEntityType.CorrectiveAction => await Db.CorrectiveActions
                .Join(Db.OperationalNotes, ca => ca.OperationalNoteId, n => n.Id, (ca, n) => new { ca.Id, n.FacilityId })
                .AnyAsync(x => x.Id == entityId && x.FacilityId == facilityId, cancellationToken),
            RiskSourceEntityType.ResourceAsset => await Db.ResourceAssets.AnyAsync(r => r.Id == entityId && r.OperationalFacilityId == facilityId, cancellationToken),
            RiskSourceEntityType.WorkforceCoverageGap or RiskSourceEntityType.WorkforceQualificationIssue =>
                await Db.WorkforceMembers.AnyAsync(w => w.Id == entityId && (w.CurrentOperationalFacilityId == facilityId || w.HomeFacilityId == facilityId), cancellationToken),
            RiskSourceEntityType.RiskRecord => await Db.RiskRecords.AnyAsync(r => r.Id == entityId && r.FacilityId == facilityId, cancellationToken),
            _ => true
        };

        if (!inScope)
        {
            throw new InvalidOperationException("لا يمكن ربط كيان خارج نطاق هذا السجن.");
        }
    }
}
