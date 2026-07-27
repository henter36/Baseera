namespace Baseera.Application.RiskManagement;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.RiskManagement;
using Microsoft.EntityFrameworkCore;

/// <summary>Existing controls, kept explicitly separate from future RiskTreatmentAction items. A control's mere existence is never treated as proof of effectiveness — ControlEffectiveness defaults to NotTested.</summary>
public sealed class RiskControlService(IBaseeraDbContext db, ICurrentUser currentUser, IOrganizationalScopeService scope, IAuditService audit)
    : RiskServiceBase(db, currentUser, scope, audit), IRiskControlService
{
    public async Task<IReadOnlyList<RiskControlDto>> ListAsync(Guid facilityId, Guid riskId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksView);
        await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);

        var controls = await Db.RiskControls.AsNoTracking()
            .Include(c => c.OwnerWorkforceMember)
            .Where(c => c.RiskRecordId == riskId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return controls.Select(Map).ToList();
    }

    private static RiskControlDto Map(RiskControl c) => new(
        c.Id, c.ControlType, RiskManagementDisplay.ControlTypeAr(c.ControlType), c.Title, c.Description,
        c.OwnerWorkforceMemberId, c.OwnerWorkforceMember?.DisplayName, c.ControlStatus, c.ControlEffectiveness,
        RiskManagementDisplay.ControlEffectivenessAr(c.ControlEffectiveness), c.ImplementedAtUtc, c.LastTestedAtUtc,
        c.NextTestDueAtUtc, c.EvidenceRequired, Convert.ToBase64String(c.RowVersion));

    public async Task<Guid> CreateAsync(Guid facilityId, Guid riskId, RiskControlCreateRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksManageControls);
        var risk = await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);

        if (risk.Status is RiskStatus.Closed or RiskStatus.Archived)
        {
            throw new InvalidOperationException("لا يمكن إضافة ضابط لخطر مغلق أو مؤرشف.");
        }

        if (request.OwnerWorkforceMemberId is Guid ownerId && !await Db.WorkforceMembers.AnyAsync(w => w.Id == ownerId, cancellationToken))
        {
            throw new InvalidOperationException("عضو القوى البشرية المحدد كمالك للضابط غير موجود.");
        }

        var control = new RiskControl
        {
            RiskRecordId = riskId,
            ControlType = request.ControlType,
            Title = request.Title.Trim(),
            Description = request.Description,
            OwnerWorkforceMemberId = request.OwnerWorkforceMemberId,
            ControlStatus = RiskControlStatus.Proposed,
            ControlEffectiveness = ControlEffectiveness.NotTested,
            EvidenceRequired = request.EvidenceRequired,
            SourceReference = request.SourceReference,
            CreatedBy = ActorReference()
        };

        Db.Add(control);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskControlCreated, nameof(RiskControl), control.Id, new { control.ControlType }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return control.Id;
    }

    public async Task RecordTestAsync(Guid facilityId, Guid riskId, Guid controlId, RiskControlTestRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksManageControls);
        await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);

        var control = await Db.RiskControls.FirstOrDefaultAsync(c => c.Id == controlId && c.RiskRecordId == riskId, cancellationToken)
            ?? throw new KeyNotFoundException("الضابط غير موجود.");
        EnsureCurrentRowVersion(control, request.RowVersion);

        var now = DateTimeOffset.UtcNow;
        control.ControlEffectiveness = request.ControlEffectiveness;
        control.LastTestedAtUtc = now;
        control.NextTestDueAtUtc = request.NextTestDueAtUtc;
        if (control.ControlStatus == RiskControlStatus.Proposed)
        {
            control.ControlStatus = RiskControlStatus.Implemented;
            control.ImplementedAtUtc = now;
        }

        control.UpdatedBy = ActorReference();
        control.UpdatedAtUtc = now;
        Db.Update(control);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskControlTested, nameof(RiskControl), control.Id, new { control.ControlEffectiveness }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }
}
