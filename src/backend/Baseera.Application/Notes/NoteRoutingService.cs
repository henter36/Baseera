namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

public interface INoteRoutingService
{
    Task<PagedResult<NoteRoutingRuleDto>> ListRulesAsync(NoteRoutingRuleQuery query, CancellationToken cancellationToken = default);
    Task<NoteRoutingRuleDto?> GetRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NoteRoutingRuleDto> CreateRuleAsync(CreateNoteRoutingRuleRequest request, CancellationToken cancellationToken = default);
    Task<NoteRoutingRuleDto> UpdateRuleAsync(Guid id, UpdateNoteRoutingRuleRequest request, CancellationToken cancellationToken = default);
    Task<NoteRoutingRuleDto> ActivateRuleAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default);
    Task<NoteRoutingRuleDto> DeactivateRuleAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default);
    Task ArchiveRuleAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default);
    Task<NoteRoutingRuleDto> RestoreRuleAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default);
    Task<NoteRoutingDecisionDto> RouteOnSubmitAsync(OperationalNote note, DateTimeOffset submittedAtUtc, string reason, CancellationToken cancellationToken = default);
    Task<NoteRoutingDecisionDto> RunManualAsync(Guid noteId, RunNoteRoutingRequest request, CancellationToken cancellationToken = default);
    Task<NoteRoutingPreviewDto> PreviewNoteAsync(Guid noteId, PreviewNoteRoutingRequest request, CancellationToken cancellationToken = default);
    Task<NoteRoutingEffectivenessDto> GetEffectivenessAsync(NoteRoutingEffectivenessQuery query, CancellationToken cancellationToken = default);
}

public sealed class NoteRoutingService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    INoteScopeService noteScope,
    INoteTypeAccessService typeAccess,
    IAuditService audit,
    TimeProvider timeProvider) : INoteRoutingService
{
    private const string RoutingRuleNotFoundMessage =
        "قاعدة التوجيه غير موجودة.";

    private const string ModuleName = "NoteRouting";
    private const string AutoRoutingReasonPrefix = "توجيه آلي بواسطة القاعدة";

    public async Task<PagedResult<NoteRoutingRuleDto>> ListRulesAsync(
        NoteRoutingRuleQuery query,
        CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesViewRouting);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var source = db.NoteRoutingRules.AsNoTracking();

        if (query.NoteTypeId.HasValue)
        {
            source = source.Where(rule => rule.NoteTypeId == query.NoteTypeId.Value);
        }

        if (query.ScopeType.HasValue)
        {
            source = source.Where(rule => rule.ScopeType == query.ScopeType.Value);
        }

        if (query.RegionId.HasValue)
        {
            source = source.Where(rule => rule.RegionId == query.RegionId.Value);
        }

        if (query.FacilityId.HasValue)
        {
            source = source.Where(rule => rule.FacilityId == query.FacilityId.Value);
        }

        if (query.FacilityUnitId.HasValue)
        {
            source = source.Where(rule => rule.FacilityUnitId == query.FacilityUnitId.Value);
        }

        if (query.IsActive.HasValue)
        {
            source = source.Where(rule => rule.IsActive == query.IsActive.Value);
        }

        if (query.ProcessingTargetType.HasValue)
        {
            source = source.Where(rule => rule.ProcessingTargetType == query.ProcessingTargetType.Value);
        }

        var total = await source.CountAsync(cancellationToken);
        var rules = await source
            .Include(rule => rule.NoteType)
            .Include(rule => rule.ProcessingDepartment)
            .Include(rule => rule.ProcessingRole)
            .Include(rule => rule.ReviewerRole)
            .OrderBy(rule => rule.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = rules.Select(ToRuleDto).ToList();
        return new PagedResult<NoteRoutingRuleDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<NoteRoutingRuleDto?> GetRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesViewRouting);
        var rule = await db.NoteRoutingRules.AsNoTracking()
            .Include(item => item.NoteType)
            .Include(item => item.ProcessingDepartment)
            .Include(item => item.ProcessingRole)
            .Include(item => item.ReviewerRole)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return rule is null ? null : ToRuleDto(rule);
    }

    public async Task<NoteRoutingRuleDto> CreateRuleAsync(
        CreateNoteRoutingRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesManageRoutingRules);
        await ValidateRuleShapeAsync(
            request.NoteTypeId,
            request.ScopeType,
            request.RegionId,
            request.FacilityId,
            request.FacilityUnitId,
            request.ProcessingTargetType,
            request.ProcessingDepartmentId,
            request.ProcessingRoleId,
            request.ReviewerRoleId,
            cancellationToken);
        await EnsureNoAmbiguousActiveRuleAsync(null, request, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var rule = new NoteRoutingRule
        {
            Code = request.Code.Trim(),
            NameAr = request.NameAr.Trim(),
            DescriptionAr = TrimOrNull(request.DescriptionAr),
            NoteTypeId = request.NoteTypeId,
            ScopeType = request.ScopeType,
            RegionId = request.RegionId,
            FacilityId = request.FacilityId,
            FacilityUnitId = request.FacilityUnitId,
            Priority = request.Priority,
            ProcessingTargetType = request.ProcessingTargetType,
            ProcessingDepartmentId = request.ProcessingDepartmentId,
            ProcessingRoleId = request.ProcessingRoleId,
            ReviewerRoleId = request.ReviewerRoleId,
            DefaultDueDays = request.DefaultDueDays,
            AutoAssignOnSubmit = request.AutoAssignOnSubmit,
            AutoReassignOnReopen = request.AutoReassignOnReopen,
            CreatedAtUtc = now,
            CreatedBy = currentUser.ExternalSubject,
            CreatedByUserId = currentUser.UserId
        };
        db.Add(rule);
        AppendRuleHistory(rule, NoteRoutingRuleChangeType.Created, request.Reason, now);
        await audit.WriteAsync(Audit("NoteRoutingRuleCreated", nameof(NoteRoutingRule), rule.Id, new { rule.Code, rule.NoteTypeId, rule.ScopeType }, request.Reason), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetRuleAsync(rule.Id, cancellationToken))!;
    }

    public async Task<NoteRoutingRuleDto> UpdateRuleAsync(
        Guid id,
        UpdateNoteRoutingRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesManageRoutingRules);
        var rule = await db.NoteRoutingRules.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException(RoutingRuleNotFoundMessage);
        if (rule.IsActive)
        {
            throw new InvalidOperationException("يجب تعطيل قاعدة التوجيه قبل تعديل حقولها الجوهرية.");
        }

        NoteAccessHelper.EnsureRowVersion(rule.RowVersion, request.RowVersion);
        await ValidateRuleShapeAsync(
            rule.NoteTypeId,
            request.ScopeType,
            request.RegionId,
            request.FacilityId,
            request.FacilityUnitId,
            request.ProcessingTargetType,
            request.ProcessingDepartmentId,
            request.ProcessingRoleId,
            request.ReviewerRoleId,
            cancellationToken);
        await EnsureNoAmbiguousActiveRuleAsync(rule.Id, request, rule.NoteTypeId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        rule.NameAr = request.NameAr.Trim();
        rule.DescriptionAr = TrimOrNull(request.DescriptionAr);
        rule.ScopeType = request.ScopeType;
        rule.RegionId = request.RegionId;
        rule.FacilityId = request.FacilityId;
        rule.FacilityUnitId = request.FacilityUnitId;
        rule.Priority = request.Priority;
        rule.ProcessingTargetType = request.ProcessingTargetType;
        rule.ProcessingDepartmentId = request.ProcessingDepartmentId;
        rule.ProcessingRoleId = request.ProcessingRoleId;
        rule.ReviewerRoleId = request.ReviewerRoleId;
        rule.DefaultDueDays = request.DefaultDueDays;
        rule.AutoAssignOnSubmit = request.AutoAssignOnSubmit;
        rule.AutoReassignOnReopen = request.AutoReassignOnReopen;
        rule.UpdatedAtUtc = now;
        rule.UpdatedBy = currentUser.ExternalSubject;
        rule.UpdatedByUserId = currentUser.UserId;
        db.Update(rule);
        AppendRuleHistory(rule, NoteRoutingRuleChangeType.Updated, request.Reason, now);
        await audit.WriteAsync(Audit("NoteRoutingRuleUpdated", nameof(NoteRoutingRule), rule.Id, new { rule.Code }, request.Reason), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetRuleAsync(rule.Id, cancellationToken))!;
    }

    public Task<NoteRoutingRuleDto> ActivateRuleAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default) =>
        SetRuleActiveAsync(id, request, true, cancellationToken);

    public Task<NoteRoutingRuleDto> DeactivateRuleAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default) =>
        SetRuleActiveAsync(id, request, false, cancellationToken);

    public async Task ArchiveRuleAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesManageRoutingRules);
        var rule = await db.NoteRoutingRules.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException(RoutingRuleNotFoundMessage);
        NoteAccessHelper.EnsureRowVersion(rule.RowVersion, request.RowVersion);
        var now = timeProvider.GetUtcNow();
        rule.IsDeleted = true;
        rule.DeletedAtUtc = now;
        rule.DeletedBy = currentUser.ExternalSubject;
        db.Update(rule);
        AppendRuleHistory(rule, NoteRoutingRuleChangeType.Archived, request.Reason, now);
        await audit.WriteAsync(Audit("NoteRoutingRuleArchived", nameof(NoteRoutingRule), rule.Id, new { rule.Code }, request.Reason), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<NoteRoutingRuleDto> RestoreRuleAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesManageRoutingRules);
        var rule = await db.NoteRoutingRulesIncludingDeleted.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException(RoutingRuleNotFoundMessage);
        NoteAccessHelper.EnsureRowVersion(rule.RowVersion, request.RowVersion);
        rule.IsDeleted = false;
        rule.DeletedAtUtc = null;
        rule.DeletedBy = null;
        var now = timeProvider.GetUtcNow();
        db.Update(rule);
        AppendRuleHistory(rule, NoteRoutingRuleChangeType.Restored, request.Reason, now);
        await audit.WriteAsync(Audit("NoteRoutingRuleRestored", nameof(NoteRoutingRule), rule.Id, new { rule.Code }, request.Reason), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetRuleAsync(rule.Id, cancellationToken))!;
    }

    public Task<NoteRoutingDecisionDto> RouteOnSubmitAsync(
        OperationalNote note,
        DateTimeOffset submittedAtUtc,
        string reason,
        CancellationToken cancellationToken = default) =>
        RouteAsync(note, NoteRoutingTrigger.Submit, submittedAtUtc, $"{note.Id}:Submit:{submittedAtUtc.UtcTicks}", reason, replaceCurrentAssignment: false, cancellationToken);

    public async Task<NoteRoutingDecisionDto> RunManualAsync(Guid noteId, RunNoteRoutingRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesRunRouting);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        await typeAccess.EnsureCanAsync(note.NoteTypeId, NoteTypeCapability.Assign, cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);

        var existing = await db.NoteAssignments.AnyAsync(a => a.OperationalNoteId == note.Id && a.IsCurrent, cancellationToken);
        if (existing && !request.ReplaceCurrentAssignment)
        {
            throw new InvalidOperationException("توجد جهة تكليف حالية. استخدم الاستبدال اليدوي عند الحاجة.");
        }

        return await db.ExecuteInTransactionAsync(
            async ct =>
            {
                if (existing)
                {
                    await EndCurrentAssignmentAsync(note.Id, timeProvider.GetUtcNow(), request.Reason.Trim(), ct);
                }

                var trigger = existing ? NoteRoutingTrigger.ManualOverride : NoteRoutingTrigger.ManualRun;
                return await RouteAsync(note, trigger, timeProvider.GetUtcNow(), $"{note.Id}:ManualRun:{request.IdempotencyKey.Trim()}", request.Reason, request.ReplaceCurrentAssignment, ct);
            },
            cancellationToken);
    }

    public async Task<NoteRoutingPreviewDto> PreviewNoteAsync(Guid noteId, PreviewNoteRoutingRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesViewRouting);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        await typeAccess.EnsureCanAsync(note.NoteTypeId, NoteTypeCapability.View, cancellationToken);
        var effectiveNote = new OperationalNote
        {
            Id = note.Id,
            NoteTypeId = request.NoteTypeId ?? note.NoteTypeId,
            ScopeType = request.ScopeType ?? note.ScopeType,
            RegionId = request.RegionId ?? note.RegionId,
            FacilityId = request.FacilityId ?? note.FacilityId,
            FacilityUnitId = request.FacilityUnitId ?? note.FacilityUnitId,
            DueAtUtc = note.DueAtUtc,
            SubmittedAtUtc = note.SubmittedAtUtc,
            Classification = note.Classification
        };
        var resolution = await ResolveRuleAsync(effectiveNote, NoteRoutingTrigger.ManualRun, cancellationToken);
        var warnings = new List<string>();
        if (resolution.Rule is null)
        {
            warnings.Add("لا توجد قاعدة مطابقة.");
        }

        var expectedUser = resolution.Rule?.ProcessingTargetType == NoteRoutingProcessingTargetType.Role
            ? await SelectEligibleUserAsync(effectiveNote, resolution.Rule.ProcessingRoleId!.Value, cancellationToken)
            : null;
        if (resolution.Rule?.ProcessingTargetType == NoteRoutingProcessingTargetType.Role && expectedUser is null)
        {
            warnings.Add("لا يوجد مستخدم مؤهل للدور المحدد.");
        }

        return new NoteRoutingPreviewDto(
            resolution.Rule is null ? null : ToRuleDto(resolution.Rule),
            resolution.Specificity,
            resolution.Reason,
            resolution.Rule?.ProcessingDepartmentId,
            expectedUser?.UserId,
            expectedUser is null ? 0 : 1,
            resolution.Rule?.ReviewerRoleId,
            ComputeDueAt(effectiveNote, resolution.Rule, timeProvider.GetUtcNow()).DueAt,
            ComputeDueAt(effectiveNote, resolution.Rule, timeProvider.GetUtcNow()).Source,
            warnings);
    }

    public async Task<NoteRoutingEffectivenessDto> GetEffectivenessAsync(
        NoteRoutingEffectivenessQuery query,
        CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesViewRoutingDiagnostics);
        var to = query.ToUtc ?? timeProvider.GetUtcNow();
        var from = query.FromUtc ?? to.AddDays(-30);
        if ((to - from).TotalDays > 90)
        {
            throw new InvalidOperationException("لا يمكن أن تتجاوز فترة قياس فاعلية التوجيه 90 يومًا.");
        }

        var source = db.NoteRoutingDecisions.AsNoTracking()
            .Where(decision => decision.DecidedAtUtc >= from && decision.DecidedAtUtc <= to);
        var total = await source.CountAsync(cancellationToken);
        var assignedDepartment = await source.CountAsync(d => d.ResultStatus == NoteRoutingResultStatus.AssignedToDepartment, cancellationToken);
        var assignedUser = await source.CountAsync(d => d.ResultStatus == NoteRoutingResultStatus.AssignedToUser, cancellationToken);
        var noMatch = await source.CountAsync(d => d.ResultStatus == NoteRoutingResultStatus.NoMatchingRule, cancellationToken);
        var noEligible = await source.CountAsync(d => d.ResultStatus == NoteRoutingResultStatus.NoEligibleUser, cancellationToken);
        var invalidTarget = await source.CountAsync(d => d.ResultStatus == NoteRoutingResultStatus.InvalidTarget, cancellationToken);
        var manualOverride = await source.CountAsync(d => d.Trigger == NoteRoutingTrigger.ManualOverride, cancellationToken);
        var requiresRouting = await db.OperationalNotes.AsNoTracking()
            .Where(n => n.Status == NoteStatus.Open || n.Status == NoteStatus.Reopened)
            .Where(n => !db.NoteAssignments.Any(a => a.OperationalNoteId == n.Id && a.IsCurrent))
            .CountAsync(cancellationToken);
        var successRate = total == 0 ? 0 : (assignedDepartment + assignedUser) * 100d / total;
        return new NoteRoutingEffectivenessDto(total, successRate, assignedDepartment, assignedUser, noMatch, noEligible, invalidTarget, manualOverride, null, null, requiresRouting);
    }

    private async Task<NoteRoutingRuleDto> SetRuleActiveAsync(
        Guid id,
        TransitionNoteRequest request,
        bool active,
        CancellationToken cancellationToken)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesActivateRoutingRules);
        var rule = await db.NoteRoutingRules.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException(RoutingRuleNotFoundMessage);
        NoteAccessHelper.EnsureRowVersion(rule.RowVersion, request.RowVersion);
        if (active)
        {
            await ValidateRuleShapeAsync(rule.NoteTypeId, rule.ScopeType, rule.RegionId, rule.FacilityId, rule.FacilityUnitId, rule.ProcessingTargetType, rule.ProcessingDepartmentId, rule.ProcessingRoleId, rule.ReviewerRoleId, cancellationToken);
            await EnsureNoAmbiguousActiveRuleAsync(rule.Id, rule, cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        rule.IsActive = active;
        if (active)
        {
            rule.ActivatedAtUtc = now;
            rule.ActivatedByUserId = currentUser.UserId;
            rule.DeactivatedAtUtc = null;
            rule.DeactivatedByUserId = null;
        }
        else
        {
            rule.DeactivatedAtUtc = now;
            rule.DeactivatedByUserId = currentUser.UserId;
        }

        rule.UpdatedAtUtc = now;
        rule.UpdatedBy = currentUser.ExternalSubject;
        rule.UpdatedByUserId = currentUser.UserId;
        db.Update(rule);
        AppendRuleHistory(rule, active ? NoteRoutingRuleChangeType.Activated : NoteRoutingRuleChangeType.Deactivated, request.Reason, now);
        await audit.WriteAsync(Audit(active ? "NoteRoutingRuleActivated" : "NoteRoutingRuleDeactivated", nameof(NoteRoutingRule), rule.Id, new { rule.Code }, request.Reason), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetRuleAsync(rule.Id, cancellationToken))!;
    }

    private async Task<NoteRoutingDecisionDto> RouteAsync(
        OperationalNote note,
        NoteRoutingTrigger trigger,
        DateTimeOffset now,
        string decisionKey,
        string reason,
        bool replaceCurrentAssignment,
        CancellationToken cancellationToken)
    {
        var existing = await db.NoteRoutingDecisions
            .AsNoTracking()
            .FirstOrDefaultAsync(decision => decision.DecisionKey == decisionKey, cancellationToken);
        if (existing is not null)
        {
            return await ToDecisionDtoAsync(existing.Id, cancellationToken);
        }

        var attemptNumber = await db.NoteRoutingDecisions
            .CountAsync(decision => decision.OperationalNoteId == note.Id, cancellationToken) + 1;
        var resolution = await ResolveRuleAsync(note, trigger, cancellationToken);
        var due = ComputeDueAt(note, resolution.Rule, now);
        var decision = NewDecision(note, trigger, attemptNumber, decisionKey, resolution.Rule, now, due.Source);
        decision.DueAtBeforeUtc = note.DueAtUtc;
        decision.DueAtAfterUtc = due.DueAt;

        if (resolution.Rule is null)
        {
            decision.ResultStatus = NoteRoutingResultStatus.NoMatchingRule;
            decision.FailureCode = "NoMatchingRule";
            decision.FailureMessageSafe = "لا توجد قاعدة توجيه مطابقة.";
            db.Add(decision);
            await audit.WriteAsync(Audit("NoteRoutingNoMatch", nameof(OperationalNote), note.Id, new { trigger }, reason), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return await ToDecisionDtoAsync(decision.Id, cancellationToken);
        }

        if (await HasCurrentAssignmentAsync(note.Id, cancellationToken) && !replaceCurrentAssignment)
        {
            decision.ResultStatus = NoteRoutingResultStatus.SkippedExistingAssignment;
            db.Add(decision);
            await db.SaveChangesAsync(cancellationToken);
            return await ToDecisionDtoAsync(decision.Id, cancellationToken);
        }

        if (note.DueAtUtc is null && due.DueAt is not null)
        {
            note.DueAtUtc = due.DueAt;
        }

        var assignment = await CreateAssignmentAsync(note, resolution.Rule, decision, now, cancellationToken);
        if (assignment is null)
        {
            db.Add(decision);
            await audit.WriteAsync(Audit("NoteRoutingNoEligibleUser", nameof(OperationalNote), note.Id, new { resolution.Rule.Code }, reason), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return await ToDecisionDtoAsync(decision.Id, cancellationToken);
        }

        decision.CreatedAssignmentId = assignment.Id;
        if (trigger == NoteRoutingTrigger.ManualOverride)
        {
            decision.ResultStatus = NoteRoutingResultStatus.ManuallyOverridden;
        }

        var fromStatus = note.Status;
        note.Status = NoteStatus.Assigned;
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);
        db.Add(decision);
        db.Add(assignment);
        db.Add(new NoteStatusHistory
        {
            OperationalNoteId = note.Id,
            FromStatus = trigger == NoteRoutingTrigger.Submit ? NoteStatus.Open : fromStatus,
            ToStatus = NoteStatus.Assigned,
            ChangedByUserId = currentUser.UserId ?? note.ReportedByUserId,
            ChangedAtUtc = now,
            Reason = assignment.Reason,
            AssignmentId = assignment.Id
        });
        await audit.WriteAsync(Audit("NoteAutoAssigned", nameof(OperationalNote), note.Id, new { decision.ResultStatus, assignment.AssignedToUserId, assignment.AssignedToDepartmentId }, reason), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDecisionDtoAsync(decision.Id, cancellationToken);
    }

    private async Task<NoteAssignment?> CreateAssignmentAsync(
        OperationalNote note,
        NoteRoutingRule rule,
        NoteRoutingDecision decision,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? note.ReportedByUserId;
        var reason = $"{AutoRoutingReasonPrefix} {rule.Code}";
        if (rule.ProcessingTargetType == NoteRoutingProcessingTargetType.Department)
        {
            if (!await db.Departments.AnyAsync(d => d.Id == rule.ProcessingDepartmentId && d.IsActive, cancellationToken))
            {
                decision.ResultStatus = NoteRoutingResultStatus.InvalidTarget;
                decision.FailureCode = "InvalidDepartment";
                decision.FailureMessageSafe = "الإدارة المحددة في قاعدة التوجيه غير صالحة.";
                return null;
            }

            decision.ResultStatus = NoteRoutingResultStatus.AssignedToDepartment;
            decision.ResolvedDepartmentId = rule.ProcessingDepartmentId;
            return NewAssignment(note, decision.Id, actorId, null, rule.ProcessingDepartmentId, now, reason);
        }

        var selected = await SelectEligibleUserAsync(note, rule.ProcessingRoleId!.Value, cancellationToken);
        if (selected is null)
        {
            decision.ResultStatus = NoteRoutingResultStatus.NoEligibleUser;
            decision.ResolvedProcessingRoleId = rule.ProcessingRoleId;
            decision.FailureCode = "NoEligibleUser";
            decision.FailureMessageSafe = "لا يوجد مستخدم مؤهل للدور المحدد.";
            return null;
        }

        decision.ResultStatus = NoteRoutingResultStatus.AssignedToUser;
        decision.ResolvedUserId = selected.UserId;
        decision.ResolvedProcessingRoleId = rule.ProcessingRoleId;
        return NewAssignment(note, decision.Id, actorId, selected.UserId, null, now, reason);
    }

    private async Task<SelectedRoutingUser?> SelectEligibleUserAsync(
        OperationalNote note,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var candidates = await db.Users
            .AsNoTracking()
            .Where(user => user.IsActive && user.ProvisioningStatus == UserProvisioningStatus.Active)
            .Where(user => db.UserRoles.Any(ur => ur.UserId == user.Id && ur.RoleId == roleId))
            .Where(user => db.UserRoles.Any(ur => ur.UserId == user.Id &&
                db.RolePermissions.Any(rp => rp.RoleId == ur.RoleId && rp.Permission.Code == PermissionCodes.NotesStartWork)))
            .Select(user => new { user.Id, user.DisplayNameAr })
            .ToListAsync(cancellationToken);
        var ids = candidates.Select(user => user.Id).ToList();
        if (ids.Count == 0)
        {
            return null;
        }

        var scopes = await db.UserScopes.AsNoTracking()
            .Where(scope => ids.Contains(scope.UserId) && scope.IsActive)
            .ToListAsync(cancellationToken);
        var scopesByUser = scopes.ToLookup(scope => scope.UserId);
        var accessByUser = await typeAccess.GetEffectiveAccessForUsersAsync(ids, note.NoteTypeId, cancellationToken);
        var workloads = await db.NoteAssignments
            .AsNoTracking()
            .Where(assignment => assignment.IsCurrent && assignment.AssignedToUserId.HasValue && ids.Contains(assignment.AssignedToUserId.Value))
            .Where(assignment => assignment.OperationalNote.Status == NoteStatus.Assigned ||
                                 assignment.OperationalNote.Status == NoteStatus.InProgress ||
                                 assignment.OperationalNote.Status == NoteStatus.Reopened)
            .GroupBy(assignment => assignment.AssignedToUserId!.Value)
            .Select(group => new
            {
                UserId = group.Key,
                Count = group.Count(),
                LastAssignedAtUtc = group.Max(item => item.AssignedAtUtc)
            })
            .ToListAsync(cancellationToken);
        var workloadByUser = workloads.ToDictionary(item => item.UserId);

        return candidates
            .Where(user => accessByUser.TryGetValue(user.Id, out var access) && access?.View.Allowed == true && access.Process.Allowed)
            .Where(user => IntersectsAny(scopesByUser[user.Id], note))
            .Select(user =>
            {
                workloadByUser.TryGetValue(user.Id, out var workload);
                return new SelectedRoutingUser(
                    user.Id,
                    user.DisplayNameAr,
                    workload?.Count ?? 0,
                    workload?.LastAssignedAtUtc);
            })
            .OrderBy(user => user.ActiveAssignmentCount)
            .ThenBy(user => user.LastAssignedAtUtc ?? DateTimeOffset.MinValue)
            .ThenBy(user => user.DisplayNameAr)
            .ThenBy(user => user.UserId)
            .FirstOrDefault();
    }

    private async Task<RuleResolution> ResolveRuleAsync(
        OperationalNote note,
        NoteRoutingTrigger trigger,
        CancellationToken cancellationToken)
    {
        var rules = await db.NoteRoutingRules
            .AsNoTracking()
            .Where(rule => rule.IsActive && rule.NoteTypeId == note.NoteTypeId)
            .Where(rule => trigger != NoteRoutingTrigger.Submit || rule.AutoAssignOnSubmit)
            .Where(rule => trigger != NoteRoutingTrigger.Reopen || rule.AutoReassignOnReopen)
            .ToListAsync(cancellationToken);
        var matches = rules
            .Where(rule => RuleCoversNote(rule, note))
            .Select(rule => new { Rule = rule, Specificity = Specificity(rule.ScopeType) })
            .OrderByDescending(item => item.Specificity)
            .ThenBy(item => item.Rule.Priority)
            .ThenBy(item => item.Rule.Code, StringComparer.Ordinal)
            .ThenBy(item => item.Rule.Id)
            .ToList();
        var selected = matches.FirstOrDefault();
        return selected is null
            ? new RuleResolution(null, "None", "لا توجد قاعدة مطابقة.")
            : new RuleResolution(selected.Rule, selected.Rule.ScopeType.ToString(), "فازت القاعدة حسب أعلى تخصص ثم أقل أولوية ثم Code.");
    }

    private static bool RuleCoversNote(NoteRoutingRule rule, OperationalNote note) => rule.ScopeType switch
    {
        ScopeType.Global => true,
        ScopeType.Headquarters => note.ScopeType == ScopeType.Headquarters,
        ScopeType.Region => note.RegionId == rule.RegionId,
        ScopeType.Facility => note.FacilityId == rule.FacilityId,
        ScopeType.FacilityUnit => note.FacilityUnitId == rule.FacilityUnitId,
        _ => false
    };

    private static int Specificity(ScopeType scopeType) => scopeType switch
    {
        ScopeType.FacilityUnit => 5,
        ScopeType.Facility => 4,
        ScopeType.Region => 3,
        ScopeType.Headquarters => 2,
        ScopeType.Global => 1,
        _ => 0
    };

    private async Task ValidateRuleShapeAsync(
        Guid noteTypeId,
        ScopeType scopeType,
        Guid? regionId,
        Guid? facilityId,
        Guid? facilityUnitId,
        NoteRoutingProcessingTargetType targetType,
        Guid? departmentId,
        Guid? roleId,
        Guid? reviewerRoleId,
        CancellationToken cancellationToken)
    {
        if (!await db.NoteTypes.AnyAsync(type => type.Id == noteTypeId, cancellationToken))
        {
            throw new KeyNotFoundException("نوع الملاحظة غير موجود.");
        }

        noteScope.ValidateScopeShape(scopeType, regionId, facilityId, facilityUnitId);
        await noteScope.EnsureOrgEntitiesActiveAsync(scopeType, regionId, facilityId, facilityUnitId, cancellationToken);
        if (targetType == NoteRoutingProcessingTargetType.Department)
        {
            if (!departmentId.HasValue || roleId.HasValue)
            {
                throw new InvalidOperationException("هدف الإدارة يتطلب DepartmentId فقط.");
            }

            if (!await db.Departments.AnyAsync(department => department.Id == departmentId.Value && department.IsActive, cancellationToken))
            {
                throw new KeyNotFoundException("الإدارة غير موجودة.");
            }
        }
        else if (!roleId.HasValue || departmentId.HasValue)
        {
            throw new InvalidOperationException("هدف الدور يتطلب ProcessingRoleId فقط.");
        }

        if (roleId.HasValue && !await db.Roles.AnyAsync(role => role.Id == roleId.Value, cancellationToken))
        {
            throw new KeyNotFoundException("دور المعالجة غير موجود.");
        }

        if (reviewerRoleId.HasValue && !await db.Roles.AnyAsync(role => role.Id == reviewerRoleId.Value, cancellationToken))
        {
            throw new KeyNotFoundException("دور المراجعة غير موجود.");
        }
    }

    private Task EnsureNoAmbiguousActiveRuleAsync(
        Guid? currentRuleId,
        CreateNoteRoutingRuleRequest request,
        CancellationToken cancellationToken) =>
        EnsureNoAmbiguousActiveRuleAsync(currentRuleId, request.NoteTypeId, request.ScopeType, request.RegionId, request.FacilityId, request.FacilityUnitId, request.Priority, cancellationToken);

    private Task EnsureNoAmbiguousActiveRuleAsync(
        Guid? currentRuleId,
        UpdateNoteRoutingRuleRequest request,
        Guid noteTypeId,
        CancellationToken cancellationToken) =>
        EnsureNoAmbiguousActiveRuleAsync(currentRuleId, noteTypeId, request.ScopeType, request.RegionId, request.FacilityId, request.FacilityUnitId, request.Priority, cancellationToken);

    private Task EnsureNoAmbiguousActiveRuleAsync(
        Guid? currentRuleId,
        NoteRoutingRule rule,
        CancellationToken cancellationToken) =>
        EnsureNoAmbiguousActiveRuleAsync(currentRuleId, rule.NoteTypeId, rule.ScopeType, rule.RegionId, rule.FacilityId, rule.FacilityUnitId, rule.Priority, cancellationToken);

    private async Task EnsureNoAmbiguousActiveRuleAsync(
        Guid? currentRuleId,
        Guid noteTypeId,
        ScopeType scopeType,
        Guid? regionId,
        Guid? facilityId,
        Guid? facilityUnitId,
        int priority,
        CancellationToken cancellationToken)
    {
        var exists = await db.NoteRoutingRules.AnyAsync(rule =>
            rule.IsActive &&
            (!currentRuleId.HasValue || rule.Id != currentRuleId.Value) &&
            rule.NoteTypeId == noteTypeId &&
            rule.ScopeType == scopeType &&
            rule.RegionId == regionId &&
            rule.FacilityId == facilityId &&
            rule.FacilityUnitId == facilityUnitId &&
            rule.Priority == priority,
            cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("توجد قاعدة توجيه فعالة بنفس النوع والنطاق والأولوية.");
        }
    }

    private static bool IntersectsAny(IEnumerable<UserScope> scopes, OperationalNote note) =>
        scopes.Any(scope => scope.ScopeType switch
        {
            ScopeType.Global => true,
            ScopeType.Headquarters => note.ScopeType == ScopeType.Headquarters,
            ScopeType.Region => note.RegionId == scope.RegionId,
            ScopeType.Facility => note.FacilityId == scope.FacilityId,
            ScopeType.FacilityUnit => note.FacilityUnitId == scope.FacilityUnitId ||
                                      (!note.FacilityUnitId.HasValue && note.FacilityId == scope.FacilityId),
            _ => false
        });

    private async Task<bool> HasCurrentAssignmentAsync(Guid noteId, CancellationToken cancellationToken) =>
        await db.NoteAssignments.AnyAsync(assignment => assignment.OperationalNoteId == noteId && assignment.IsCurrent, cancellationToken);

    private async Task EndCurrentAssignmentAsync(Guid noteId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        var current = await db.NoteAssignments.FirstOrDefaultAsync(a => a.OperationalNoteId == noteId && a.IsCurrent, cancellationToken);
        if (current is null)
        {
            return;
        }

        current.IsCurrent = false;
        current.EndedAtUtc = now;
        current.EndReason = reason;
        db.Update(current);
    }

    private static NoteAssignment NewAssignment(
        OperationalNote note,
        Guid decisionId,
        Guid actorId,
        Guid? userId,
        Guid? departmentId,
        DateTimeOffset now,
        string reason) =>
        new()
        {
            OperationalNoteId = note.Id,
            AssignedToUserId = userId,
            AssignedToDepartmentId = departmentId,
            AssignedByUserId = actorId,
            AssignedAtUtc = now,
            DueAtUtc = note.DueAtUtc,
            Reason = reason,
            IsCurrent = true,
            RoutingDecisionId = decisionId,
            CreatedBy = "routing"
        };

    private NoteRoutingDecision NewDecision(
        OperationalNote note,
        NoteRoutingTrigger trigger,
        int attemptNumber,
        string decisionKey,
        NoteRoutingRule? rule,
        DateTimeOffset now,
        string dueSource) =>
        new()
        {
            OperationalNoteId = note.Id,
            Trigger = trigger,
            AttemptNumber = attemptNumber,
            DecisionKey = decisionKey,
            RoutingRuleId = rule?.Id,
            ResolvedProcessingRoleId = rule?.ProcessingRoleId,
            ResolvedReviewerRoleId = rule?.ReviewerRoleId,
            DecidedAtUtc = now,
            DecidedByUserId = currentUser.UserId,
            CorrelationId = currentUser.CorrelationId,
            DueAtSource = dueSource,
            ResultStatus = NoteRoutingResultStatus.Failed
        };

    private (DateTimeOffset? DueAt, string Source) ComputeDueAt(OperationalNote note, NoteRoutingRule? rule, DateTimeOffset submittedAtUtc)
    {
        if (note.DueAtUtc.HasValue)
        {
            return (note.DueAtUtc.Value, "UserProvided");
        }

        if (rule?.DefaultDueDays is int ruleDays)
        {
            return (submittedAtUtc.AddDays(ruleDays), "RoutingRule");
        }

        var noteTypeDays = db.NoteTypes.AsNoTracking()
            .Where(type => type.Id == note.NoteTypeId)
            .Select(type => type.DefaultDueDays)
            .FirstOrDefault();
        return noteTypeDays.HasValue ? (submittedAtUtc.AddDays(noteTypeDays.Value), "NoteType") : (null, "None");
    }

    private void AppendRuleHistory(
        NoteRoutingRule rule,
        NoteRoutingRuleChangeType changeType,
        string reason,
        DateTimeOffset now) =>
        db.Add(new NoteRoutingRuleHistory
        {
            RoutingRuleId = rule.Id,
            ChangeType = changeType,
            SnapshotJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                rule.Code,
                rule.NoteTypeId,
                rule.ScopeType,
                rule.RegionId,
                rule.FacilityId,
                rule.FacilityUnitId,
                rule.Priority,
                rule.ProcessingTargetType,
                rule.ProcessingDepartmentId,
                rule.ProcessingRoleId,
                rule.ReviewerRoleId,
                rule.DefaultDueDays,
                rule.AutoAssignOnSubmit,
                rule.AutoReassignOnReopen,
                rule.IsActive
            }),
            ChangedAtUtc = now,
            ChangedByUserId = currentUser.UserId,
            Reason = reason.Trim(),
            CorrelationId = currentUser.CorrelationId
        });

    private async Task<NoteRoutingDecisionDto> ToDecisionDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var decision = await db.NoteRoutingDecisions.AsNoTracking()
            .Include(item => item.RoutingRule)
            .Include(item => item.ResolvedDepartment)
            .Include(item => item.ResolvedUser)
            .Include(item => item.ResolvedProcessingRole)
            .Include(item => item.ResolvedReviewerRole)
            .FirstAsync(decision => decision.Id == id, cancellationToken);
        return ToDecisionDto(decision);
    }

    private AuditEntry Audit(string action, string entityType, Guid entityId, object values, string? reason) =>
        new()
        {
            Action = action,
            Module = ModuleName,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            NewValues = values,
            Reason = reason
        };

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static NoteRoutingRuleDto ToRuleDto(NoteRoutingRule rule) =>
        new(
            rule.Id,
            rule.Code,
            rule.NameAr,
            rule.DescriptionAr,
            rule.NoteTypeId,
            rule.NoteType.NameAr,
            rule.ScopeType,
            rule.RegionId,
            rule.FacilityId,
            rule.FacilityUnitId,
            rule.Priority,
            rule.ProcessingTargetType,
            rule.ProcessingDepartmentId,
            rule.ProcessingDepartment == null ? null : rule.ProcessingDepartment.NameAr,
            rule.ProcessingRoleId,
            rule.ProcessingRole == null ? null : rule.ProcessingRole.NameAr,
            rule.ReviewerRoleId,
            rule.ReviewerRole == null ? null : rule.ReviewerRole.NameAr,
            rule.DefaultDueDays,
            rule.AutoAssignOnSubmit,
            rule.AutoReassignOnReopen,
            rule.IsActive,
            rule.ActivatedAtUtc,
            rule.DeactivatedAtUtc,
            rule.CreatedAtUtc,
            rule.UpdatedAtUtc,
            Convert.ToBase64String(rule.RowVersion));

    private static NoteRoutingDecisionDto ToDecisionDto(NoteRoutingDecision decision) =>
        new(
            decision.Id,
            decision.OperationalNoteId,
            decision.Trigger,
            decision.AttemptNumber,
            decision.RoutingRuleId,
            decision.RoutingRule == null ? null : decision.RoutingRule.Code,
            decision.ResultStatus,
            decision.ResolvedDepartmentId,
            decision.ResolvedDepartment == null ? null : decision.ResolvedDepartment.NameAr,
            decision.ResolvedUserId,
            decision.ResolvedUser == null ? null : decision.ResolvedUser.DisplayNameAr,
            decision.ResolvedProcessingRoleId,
            decision.ResolvedProcessingRole == null ? null : decision.ResolvedProcessingRole.NameAr,
            decision.ResolvedReviewerRoleId,
            decision.ResolvedReviewerRole == null ? null : decision.ResolvedReviewerRole.NameAr,
            decision.CreatedAssignmentId,
            decision.DueAtBeforeUtc,
            decision.DueAtAfterUtc,
            decision.DueAtSource,
            decision.DecidedAtUtc,
            decision.FailureCode,
            decision.FailureMessageSafe);

    private sealed record RuleResolution(NoteRoutingRule? Rule, string Specificity, string Reason);
    private sealed record SelectedRoutingUser(Guid UserId, string DisplayNameAr, int ActiveAssignmentCount, DateTimeOffset? LastAssignedAtUtc);
}
