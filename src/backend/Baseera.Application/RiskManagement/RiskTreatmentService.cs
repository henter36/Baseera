namespace Baseera.Application.RiskManagement;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.RiskManagement;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Treatment plans and their actions. Dependency cycles are structurally impossible: an action's
/// DependencyActionId may only reference an action that already exists in the same plan at creation time,
/// so the dependency graph is a DAG by construction — there is no "edit dependency" operation that could
/// introduce a cycle later. Completing all actions never auto-completes the plan (principle: closing an
/// action does not imply closing the plan) — Complete is always an explicit plan command.
/// </summary>
public sealed class RiskTreatmentService(IBaseeraDbContext db, ICurrentUser currentUser, IOrganizationalScopeService scope, IAuditService audit)
    : RiskServiceBase(db, currentUser, scope, audit), IRiskTreatmentService
{
    public async Task<IReadOnlyList<RiskTreatmentPlanDto>> ListPlansAsync(Guid facilityId, Guid riskId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksView);
        await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);

        var plans = await Db.RiskTreatmentPlans.AsNoTracking()
            .Include(p => p.OwnerWorkforceMember)
            .Include(p => p.Actions).ThenInclude(a => a.AssignedToWorkforceMember)
            .Where(p => p.RiskRecordId == riskId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        return plans.Select(p => MapPlan(p, now)).ToList();
    }

    private static RiskTreatmentPlanDto MapPlan(RiskTreatmentPlan p, DateTimeOffset now) => new(
        p.Id, p.Title, p.Objective, p.Strategy, RiskManagementDisplay.TreatmentStrategyAr(p.Strategy), p.Status,
        RiskManagementDisplay.TreatmentPlanStatusAr(p.Status), RiskTreatmentPlanStateMachine.IsOverdue(p.Status, p.DueAtUtc, now),
        p.Priority, p.DueAtUtc, p.CompletedAtUtc, p.TargetScore, p.OwnerWorkforceMemberId, p.OwnerWorkforceMember?.DisplayName,
        p.ApprovalStatus,
        p.Actions.OrderBy(a => a.DueAtUtc).Select(a => MapAction(a, now)).ToList(),
        Convert.ToBase64String(p.RowVersion));

    private static RiskTreatmentActionDto MapAction(RiskTreatmentAction a, DateTimeOffset now) => new(
        a.Id, a.Title, a.Description, a.Status, RiskManagementDisplay.TreatmentActionStatusAr(a.Status), a.Priority,
        a.DueAtUtc, a.CompletedAtUtc, RiskTreatmentActionStateMachine.IsOverdue(a.Status, a.DueAtUtc, now),
        a.CompletionEvidenceRequired, a.CompletionSummary, a.BlockedReason, a.AssignedToWorkforceMemberId,
        a.AssignedToWorkforceMember?.DisplayName, Convert.ToBase64String(a.RowVersion));

    public async Task<Guid> CreatePlanAsync(Guid facilityId, Guid riskId, RiskTreatmentPlanCreateRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksManageTreatments);
        var risk = await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);

        if (risk.Status is RiskStatus.Closed or RiskStatus.Archived)
        {
            throw new InvalidOperationException("لا يمكن إنشاء خطة معالجة لخطر مغلق أو مؤرشف.");
        }

        if (request.OwnerWorkforceMemberId is Guid ownerId && !await Db.WorkforceMembers.AnyAsync(w => w.Id == ownerId, cancellationToken))
        {
            throw new InvalidOperationException("عضو القوى البشرية المحدد كمالك للخطة غير موجود.");
        }

        var plan = new RiskTreatmentPlan
        {
            RiskRecordId = riskId,
            Strategy = request.Strategy,
            Title = request.Title.Trim(),
            Objective = request.Objective.Trim(),
            OwnerWorkforceMemberId = request.OwnerWorkforceMemberId,
            Priority = request.Priority,
            PlannedStartAtUtc = request.PlannedStartAtUtc,
            DueAtUtc = request.DueAtUtc,
            TargetLikelihoodLevelId = request.TargetLikelihoodLevelId,
            TargetImpactLevelId = request.TargetImpactLevelId,
            TargetScore = request.TargetScore,
            Status = TreatmentPlanStatus.Draft,
            ApprovalStatus = RiskApprovalStatus.Pending,
            CreatedBy = ActorReference()
        };

        Db.Add(plan);
        risk.TreatmentStrategy = request.Strategy;
        Db.Update(risk);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskTreatmentCreated, nameof(RiskTreatmentPlan), plan.Id, new { plan.Strategy }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return plan.Id;
    }

    public async Task ExecutePlanCommandAsync(Guid facilityId, Guid riskId, Guid planId, RiskTreatmentPlanCommandRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksManageTreatments);
        var risk = await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);
        var plan = await Db.RiskTreatmentPlans.Include(p => p.Actions)
            .FirstOrDefaultAsync(p => p.Id == planId && p.RiskRecordId == riskId, cancellationToken)
            ?? throw new KeyNotFoundException("خطة المعالجة غير موجودة.");
        EnsureCurrentRowVersion(plan, request.RowVersion);

        switch (request.Command)
        {
            case RiskTreatmentPlanCommandTypes.Submit:
                RiskTreatmentPlanStateMachine.EnsureAllowed(plan.Status, TreatmentPlanStatus.PendingApproval);
                plan.Status = TreatmentPlanStatus.PendingApproval;
                break;

            case RiskTreatmentPlanCommandTypes.Approve:
                RiskTreatmentPlanStateMachine.EnsureAllowed(plan.Status, TreatmentPlanStatus.Approved);
                EnforceFourEyes(plan.CreatedBy ?? string.Empty);
                plan.Status = TreatmentPlanStatus.Approved;
                plan.ApprovalStatus = RiskApprovalStatus.Approved;
                plan.ApprovedBy = ActorReference();
                plan.ApprovedAtUtc = DateTimeOffset.UtcNow;

                if (risk.Status == RiskStatus.Active)
                {
                    RiskLifecycleStateMachine.EnsureAllowed(RiskStatus.Active, RiskStatus.UnderTreatment);
                    risk.Status = RiskStatus.UnderTreatment;
                    Db.Add(new RiskStatusHistory { RiskRecordId = risk.Id, FromStatus = RiskStatus.Active, ToStatus = RiskStatus.UnderTreatment, ChangedBy = ActorReference(), Reason = "اعتماد خطة معالجة." });
                    Db.Update(risk);
                }

                await AuditAsync(RiskAuditActions.RiskTreatmentApproved, nameof(RiskTreatmentPlan), plan.Id, null, cancellationToken);
                break;

            case RiskTreatmentPlanCommandTypes.Reject:
                RequireReason(request.Reason);
                RiskTreatmentPlanStateMachine.EnsureAllowed(plan.Status, TreatmentPlanStatus.Rejected);
                plan.Status = TreatmentPlanStatus.Rejected;
                plan.ApprovalStatus = RiskApprovalStatus.Rejected;
                plan.CancellationReason = request.Reason;
                break;

            case RiskTreatmentPlanCommandTypes.Start:
                RiskTreatmentPlanStateMachine.EnsureAllowed(plan.Status, TreatmentPlanStatus.InProgress);
                plan.Status = TreatmentPlanStatus.InProgress;
                break;

            case RiskTreatmentPlanCommandTypes.Block:
                RequireReason(request.Reason);
                RiskTreatmentPlanStateMachine.EnsureAllowed(plan.Status, TreatmentPlanStatus.Blocked);
                plan.Status = TreatmentPlanStatus.Blocked;
                break;

            case RiskTreatmentPlanCommandTypes.Unblock:
                RiskTreatmentPlanStateMachine.EnsureAllowed(plan.Status, TreatmentPlanStatus.InProgress);
                plan.Status = TreatmentPlanStatus.InProgress;
                break;

            case RiskTreatmentPlanCommandTypes.Complete:
                if (plan.Actions.Any(a => a.Status != RiskTreatmentActionStatus.Completed && a.Status != RiskTreatmentActionStatus.Cancelled))
                {
                    throw new InvalidOperationException("لا يمكن إكمال خطة المعالجة قبل إكمال أو إلغاء جميع إجراءاتها.");
                }

                RiskTreatmentPlanStateMachine.EnsureAllowed(plan.Status, TreatmentPlanStatus.Completed);
                plan.Status = TreatmentPlanStatus.Completed;
                plan.CompletedAtUtc = DateTimeOffset.UtcNow;
                break;

            case RiskTreatmentPlanCommandTypes.Cancel:
                RequireReason(request.Reason);
                RiskTreatmentPlanStateMachine.EnsureAllowed(plan.Status, TreatmentPlanStatus.Cancelled);
                plan.Status = TreatmentPlanStatus.Cancelled;
                plan.CancellationReason = request.Reason;
                break;

            default:
                throw new InvalidOperationException($"أمر غير معروف: {request.Command}.");
        }

        plan.UpdatedBy = ActorReference();
        plan.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Db.Update(plan);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskTreatmentActionChanged, nameof(RiskTreatmentPlan), plan.Id, new { request.Command }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> CreateActionAsync(Guid facilityId, Guid riskId, Guid planId, RiskTreatmentActionCreateRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksManageTreatments);
        await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);
        var plan = await Db.RiskTreatmentPlans.FirstOrDefaultAsync(p => p.Id == planId && p.RiskRecordId == riskId, cancellationToken)
            ?? throw new KeyNotFoundException("خطة المعالجة غير موجودة.");

        if (plan.Status is not (TreatmentPlanStatus.Approved or TreatmentPlanStatus.InProgress or TreatmentPlanStatus.Blocked))
        {
            throw new InvalidOperationException("لا يمكن إضافة إجراءات إلا لخطة معتمدة أو قيد التنفيذ.");
        }

        if (request.DependencyActionId is Guid dependencyId && !await Db.RiskTreatmentActions.AnyAsync(a => a.Id == dependencyId && a.TreatmentPlanId == planId, cancellationToken))
        {
            throw new InvalidOperationException("إجراء الاعتمادية المحدد غير موجود ضمن نفس الخطة.");
        }

        if (request.AssignedToWorkforceMemberId is Guid memberId && !await Db.WorkforceMembers.AnyAsync(w => w.Id == memberId, cancellationToken))
        {
            throw new InvalidOperationException("عضو القوى البشرية المسند إليه غير موجود.");
        }

        var action = new RiskTreatmentAction
        {
            TreatmentPlanId = planId,
            Title = request.Title.Trim(),
            Description = request.Description,
            AssignedToWorkforceMemberId = request.AssignedToWorkforceMemberId,
            AssignedToUserId = request.AssignedToUserId,
            Priority = request.Priority,
            StartAtUtc = request.StartAtUtc,
            DueAtUtc = request.DueAtUtc,
            CompletionEvidenceRequired = request.CompletionEvidenceRequired,
            DependencyActionId = request.DependencyActionId,
            Status = (request.AssignedToWorkforceMemberId ?? request.AssignedToUserId) is null
                ? RiskTreatmentActionStatus.Draft
                : RiskTreatmentActionStatus.Assigned,
            CreatedBy = ActorReference()
        };

        Db.Add(action);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskTreatmentActionChanged, nameof(RiskTreatmentAction), action.Id, new { Command = "Created" }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return action.Id;
    }

    public async Task ExecuteActionCommandAsync(Guid facilityId, Guid riskId, Guid planId, Guid actionId, RiskTreatmentActionCommandRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);
        var action = await Db.RiskTreatmentActions.Include(a => a.DependencyAction)
            .FirstOrDefaultAsync(a => a.Id == actionId && a.TreatmentPlanId == planId, cancellationToken)
            ?? throw new KeyNotFoundException("إجراء المعالجة غير موجود.");
        EnsureCurrentRowVersion(action, request.RowVersion);

        switch (request.Command)
        {
            case RiskTreatmentActionCommandTypes.Assign:
                Require(PermissionCodes.RisksManageTreatments);
                RiskTreatmentActionStateMachine.EnsureAllowed(action.Status, RiskTreatmentActionStatus.Assigned);
                action.Status = RiskTreatmentActionStatus.Assigned;
                break;

            case RiskTreatmentActionCommandTypes.Start:
                Require(PermissionCodes.RisksManageTreatments);
                if (action.DependencyAction is not null && action.DependencyAction.Status != RiskTreatmentActionStatus.Completed)
                {
                    throw new InvalidOperationException("لا يمكن بدء هذا الإجراء قبل إكمال الإجراء المرتبط به.");
                }

                RiskTreatmentActionStateMachine.EnsureAllowed(action.Status, RiskTreatmentActionStatus.InProgress);
                action.Status = RiskTreatmentActionStatus.InProgress;
                break;

            case RiskTreatmentActionCommandTypes.Block:
                Require(PermissionCodes.RisksManageTreatments);
                RequireReason(request.Reason);
                RiskTreatmentActionStateMachine.EnsureAllowed(action.Status, RiskTreatmentActionStatus.Blocked);
                action.Status = RiskTreatmentActionStatus.Blocked;
                action.BlockedReason = request.Reason;
                break;

            case RiskTreatmentActionCommandTypes.Unblock:
                Require(PermissionCodes.RisksManageTreatments);
                RiskTreatmentActionStateMachine.EnsureAllowed(action.Status, RiskTreatmentActionStatus.InProgress);
                action.Status = RiskTreatmentActionStatus.InProgress;
                action.BlockedReason = null;
                break;

            case RiskTreatmentActionCommandTypes.SubmitForVerification:
                Require(PermissionCodes.RisksCompleteTreatmentActions);
                if (action.CompletionEvidenceRequired && string.IsNullOrWhiteSpace(request.CompletionSummary))
                {
                    throw new InvalidOperationException("يلزم توثيق ملخص الإكمال والدليل قبل الإرسال للتحقق.");
                }

                RiskTreatmentActionStateMachine.EnsureAllowed(action.Status, RiskTreatmentActionStatus.PendingVerification);
                action.Status = RiskTreatmentActionStatus.PendingVerification;
                action.CompletionSummary = request.CompletionSummary;
                action.UpdatedBy = ActorReference();
                break;

            case RiskTreatmentActionCommandTypes.Verify:
                Require(PermissionCodes.RisksVerifyTreatmentActions);
                EnforceFourEyes(action.UpdatedBy ?? action.CreatedBy ?? string.Empty);
                RiskTreatmentActionStateMachine.EnsureAllowed(action.Status, RiskTreatmentActionStatus.Completed);
                action.Status = RiskTreatmentActionStatus.Completed;
                action.CompletedAtUtc = DateTimeOffset.UtcNow;
                action.VerifiedAtUtc = DateTimeOffset.UtcNow;
                action.VerifiedBy = ActorReference();
                break;

            case RiskTreatmentActionCommandTypes.ReturnForRework:
                Require(PermissionCodes.RisksVerifyTreatmentActions);
                RiskTreatmentActionStateMachine.EnsureAllowed(action.Status, RiskTreatmentActionStatus.InProgress);
                action.Status = RiskTreatmentActionStatus.InProgress;
                break;

            case RiskTreatmentActionCommandTypes.Cancel:
                Require(PermissionCodes.RisksManageTreatments);
                RequireReason(request.Reason);
                RiskTreatmentActionStateMachine.EnsureAllowed(action.Status, RiskTreatmentActionStatus.Cancelled);
                action.Status = RiskTreatmentActionStatus.Cancelled;
                action.CancellationReason = request.Reason;
                break;

            default:
                throw new InvalidOperationException($"أمر غير معروف: {request.Command}.");
        }

        action.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Db.Update(action);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskTreatmentActionChanged, nameof(RiskTreatmentAction), action.Id, new { request.Command }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }

    private static void RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("السبب مطلوب لتنفيذ هذا الأمر.");
        }
    }
}
