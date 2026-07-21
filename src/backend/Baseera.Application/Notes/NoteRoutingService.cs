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
        var source = await noteScope.FilterRoutingRulesQueryableAsync(
            db.NoteRoutingRules.AsNoTracking(),
            cancellationToken);

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
        if (rule is null || !noteScope.CanAccessRoutingRule(rule))
        {
            return null;
        }

        return ToRuleDto(rule);
    }

    public async Task<NoteRoutingRuleDto> CreateRuleAsync(
        CreateNoteRoutingRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesManageRoutingRules);
        await ValidateRuleShapeAsync(ToRuleShape(request), cancellationToken);
        await EnsureNoAmbiguousActiveRuleAsync(null, ToRuleIdentity(request), cancellationToken);

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
        var rule = await LoadRuleInScopeOrNotFoundAsync(id, cancellationToken: cancellationToken);
        if (rule.IsActive)
        {
            throw new InvalidOperationException("يجب تعطيل قاعدة التوجيه قبل تعديل حقولها الجوهرية.");
        }

        NoteAccessHelper.EnsureRowVersion(rule.RowVersion, request.RowVersion);
        await ValidateRuleShapeAsync(ToRuleShape(request, rule.NoteTypeId), cancellationToken);
        await EnsureNoAmbiguousActiveRuleAsync(rule.Id, ToRuleIdentity(request, rule.NoteTypeId), cancellationToken);

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
        var rule = await LoadRuleInScopeOrNotFoundAsync(id, cancellationToken: cancellationToken);
        NoteAccessHelper.EnsureRowVersion(rule.RowVersion, request.RowVersion);
        var now = timeProvider.GetUtcNow();
        rule.IsActive = false;
        rule.ActivatedAtUtc = null;
        rule.ActivatedByUserId = null;
        rule.DeactivatedAtUtc = now;
        rule.DeactivatedByUserId = currentUser.UserId;
        rule.IsDeleted = true;
        rule.DeletedAtUtc = now;
        rule.DeletedBy = currentUser.ExternalSubject;
        rule.UpdatedAtUtc = now;
        rule.UpdatedBy = currentUser.ExternalSubject;
        rule.UpdatedByUserId = currentUser.UserId;
        db.Update(rule);
        AppendRuleHistory(rule, NoteRoutingRuleChangeType.Archived, request.Reason, now);
        await audit.WriteAsync(Audit("NoteRoutingRuleArchived", nameof(NoteRoutingRule), rule.Id, new { rule.Code }, request.Reason), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<NoteRoutingRuleDto> RestoreRuleAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesManageRoutingRules);
        var rule = await LoadRuleInScopeOrNotFoundAsync(id, includeDeleted: true, cancellationToken: cancellationToken);
        NoteAccessHelper.EnsureRowVersion(rule.RowVersion, request.RowVersion);
        var now = timeProvider.GetUtcNow();
        rule.IsDeleted = false;
        rule.DeletedAtUtc = null;
        rule.DeletedBy = null;
        rule.IsActive = false;
        rule.ActivatedAtUtc = null;
        rule.ActivatedByUserId = null;
        rule.DeactivatedAtUtc = now;
        rule.DeactivatedByUserId = currentUser.UserId;
        rule.UpdatedAtUtc = now;
        rule.UpdatedBy = currentUser.ExternalSubject;
        rule.UpdatedByUserId = currentUser.UserId;
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
        if (resolution.MatchedRule is null)
        {
            warnings.Add("لا توجد قاعدة مطابقة.");
        }

        var expectedUser = resolution.MatchedRule?.ProcessingTargetType == NoteRoutingProcessingTargetType.Role
            ? await SelectEligibleUserAsync(effectiveNote, resolution.MatchedRule.ProcessingRoleId!.Value, cancellationToken)
            : null;
        if (resolution.MatchedRule?.ProcessingTargetType == NoteRoutingProcessingTargetType.Role && expectedUser is null)
        {
            warnings.Add("لا يوجد مستخدم مؤهل للدور المحدد.");
        }

        var due = await ComputeDueAtAsync(
            effectiveNote,
            resolution.MatchedRule,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return new NoteRoutingPreviewDto(
            resolution.MatchedRule is null ? null : ToRuleDto(resolution.MatchedRule),
            resolution.Specificity,
            resolution.Reason,
            resolution.MatchedRule?.ProcessingDepartmentId,
            expectedUser?.UserId,
            expectedUser is null ? 0 : 1,
            resolution.MatchedRule?.ReviewerRoleId,
            due.DueAt,
            due.Source,
            warnings);
    }

    public async Task<NoteRoutingEffectivenessDto> GetEffectivenessAsync(
        NoteRoutingEffectivenessQuery query,
        CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesViewRoutingDiagnostics);
        var to = query.ToUtc ?? timeProvider.GetUtcNow();
        var from = query.FromUtc ?? to.AddDays(-30);
        if (from > to)
        {
            throw new InvalidOperationException(
                "تاريخ البدء لا يمكن أن يكون بعد تاريخ الانتهاء.");
        }

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
        var rule = await LoadRuleInScopeOrNotFoundAsync(id, cancellationToken: cancellationToken);
        NoteAccessHelper.EnsureRowVersion(rule.RowVersion, request.RowVersion);
        if (active)
        {
            await ValidateRuleShapeAsync(ToRuleShape(rule), cancellationToken);
            await EnsureNoAmbiguousActiveRuleAsync(rule.Id, ToRuleIdentity(rule), cancellationToken);
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
        var resolution = await ResolveRuleAsync(
            note,
            trigger,
            cancellationToken);
        var due = await ComputeDueAtAsync(
            note,
            resolution.MatchedRule,
            now,
            cancellationToken);
        var decision = NewDecision(
            note,
            trigger,
            attemptNumber,
            decisionKey,
            resolution.MatchedRule,
            now,
            due.Source);
        decision.DueAtBeforeUtc = note.DueAtUtc;
        decision.DueAtAfterUtc = due.DueAt;

        if (resolution.MatchedRule is null)
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

        var assignment = await CreateAssignmentAsync(note, resolution.MatchedRule, decision, now, cancellationToken);
        if (assignment is null)
        {
            db.Add(decision);
            await audit.WriteAsync(Audit("NoteRoutingNoEligibleUser", nameof(OperationalNote), note.Id, new { resolution.MatchedRule.Code }, reason), cancellationToken);
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
        HashSet<Guid>? sensitiveViewerIds = null;
        if (NoteAccessHelper.RequiresSensitive(note.Classification))
        {
            sensitiveViewerIds = await db.UserRoles
                .AsNoTracking()
                .Where(userRole => ids.Contains(userRole.UserId))
                .Where(userRole => db.RolePermissions.Any(permission =>
                    permission.RoleId == userRole.RoleId &&
                    permission.Permission.Code == PermissionCodes.NotesViewSensitive))
                .Select(userRole => userRole.UserId)
                .Distinct()
                .ToHashSetAsync(cancellationToken);
        }

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
            .Where(user => !NoteAccessHelper.RequiresSensitive(note.Classification) || sensitiveViewerIds!.Contains(user.Id))
            .Where(user => NoteAssigneeScopeIntersection.IntersectsAnyUserScopeForRouting(scopesByUser[user.Id], note))
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
        RoutingRuleShape shape,
        CancellationToken cancellationToken)
    {
        if (!await db.NoteTypes.AnyAsync(type => type.Id == shape.NoteTypeId, cancellationToken))
        {
            throw new KeyNotFoundException("نوع الملاحظة غير موجود.");
        }

        noteScope.ValidateScopeShape(shape.RuleScopeType, shape.RegionId, shape.FacilityId, shape.FacilityUnitId);
        await noteScope.EnsureOrgEntitiesActiveAsync(shape.RuleScopeType, shape.RegionId, shape.FacilityId, shape.FacilityUnitId, cancellationToken);
        if (shape.ProcessingTargetType == NoteRoutingProcessingTargetType.Department)
        {
            if (!shape.ProcessingDepartmentId.HasValue || shape.ProcessingRoleId.HasValue)
            {
                throw new InvalidOperationException("هدف الإدارة يتطلب DepartmentId فقط.");
            }

            if (!await db.Departments.AnyAsync(department => department.Id == shape.ProcessingDepartmentId.Value && department.IsActive, cancellationToken))
            {
                throw new KeyNotFoundException("الإدارة غير موجودة.");
            }
        }
        else if (!shape.ProcessingRoleId.HasValue || shape.ProcessingDepartmentId.HasValue)
        {
            throw new InvalidOperationException("هدف الدور يتطلب ProcessingRoleId فقط.");
        }

        if (shape.ProcessingRoleId.HasValue && !await db.Roles.AnyAsync(role => role.Id == shape.ProcessingRoleId.Value, cancellationToken))
        {
            throw new KeyNotFoundException("دور المعالجة غير موجود.");
        }

        if (shape.ReviewerRoleId.HasValue && !await db.Roles.AnyAsync(role => role.Id == shape.ReviewerRoleId.Value, cancellationToken))
        {
            throw new KeyNotFoundException("دور المراجعة غير موجود.");
        }
    }

    private async Task EnsureNoAmbiguousActiveRuleAsync(
        Guid? currentRuleId,
        RoutingRuleIdentity identity,
        CancellationToken cancellationToken)
    {
        var exists = await db.NoteRoutingRules.AnyAsync(rule =>
            rule.IsActive &&
            (!currentRuleId.HasValue || rule.Id != currentRuleId.Value) &&
            rule.NoteTypeId == identity.NoteTypeId &&
            rule.ScopeType == identity.RuleScopeType &&
            rule.RegionId == identity.RegionId &&
            rule.FacilityId == identity.FacilityId &&
            rule.FacilityUnitId == identity.FacilityUnitId &&
            rule.Priority == identity.Priority,
            cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("توجد قاعدة توجيه فعالة بنفس النوع والنطاق والأولوية.");
        }
    }

    private async Task<NoteRoutingRule> LoadRuleInScopeOrNotFoundAsync(
        Guid id,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var query = includeDeleted ? db.NoteRoutingRulesIncludingDeleted : db.NoteRoutingRules;
        var rule = await query.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (rule is null || !noteScope.CanAccessRoutingRule(rule))
        {
            throw new KeyNotFoundException(RoutingRuleNotFoundMessage);
        }

        return rule;
    }

    private static RoutingRuleShape ToRuleShape(CreateNoteRoutingRuleRequest request) =>
        new(
            request.NoteTypeId,
            request.ScopeType,
            request.RegionId,
            request.FacilityId,
            request.FacilityUnitId,
            request.ProcessingTargetType,
            request.ProcessingDepartmentId,
            request.ProcessingRoleId,
            request.ReviewerRoleId);

    private static RoutingRuleShape ToRuleShape(UpdateNoteRoutingRuleRequest request, Guid noteTypeId) =>
        new(
            noteTypeId,
            request.ScopeType,
            request.RegionId,
            request.FacilityId,
            request.FacilityUnitId,
            request.ProcessingTargetType,
            request.ProcessingDepartmentId,
            request.ProcessingRoleId,
            request.ReviewerRoleId);

    private static RoutingRuleShape ToRuleShape(NoteRoutingRule rule) =>
        new(
            rule.NoteTypeId,
            rule.ScopeType,
            rule.RegionId,
            rule.FacilityId,
            rule.FacilityUnitId,
            rule.ProcessingTargetType,
            rule.ProcessingDepartmentId,
            rule.ProcessingRoleId,
            rule.ReviewerRoleId);

    private static RoutingRuleIdentity ToRuleIdentity(CreateNoteRoutingRuleRequest request) =>
        new(
            request.NoteTypeId,
            request.ScopeType,
            request.RegionId,
            request.FacilityId,
            request.FacilityUnitId,
            request.Priority);

    private static RoutingRuleIdentity ToRuleIdentity(UpdateNoteRoutingRuleRequest request, Guid noteTypeId) =>
        new(
            noteTypeId,
            request.ScopeType,
            request.RegionId,
            request.FacilityId,
            request.FacilityUnitId,
            request.Priority);

    private static RoutingRuleIdentity ToRuleIdentity(NoteRoutingRule rule) =>
        new(
            rule.NoteTypeId,
            rule.ScopeType,
            rule.RegionId,
            rule.FacilityId,
            rule.FacilityUnitId,
            rule.Priority);

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

    private async Task<(DateTimeOffset? DueAt, string Source)> ComputeDueAtAsync(
        OperationalNote note,
        NoteRoutingRule? rule,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken)
    {
        if (note.DueAtUtc.HasValue)
        {
            return (note.DueAtUtc.Value, "UserProvided");
        }

        if (rule?.DefaultDueDays is int ruleDays)
        {
            return (
                submittedAtUtc.AddDays(ruleDays),
                "RoutingRule");
        }

        var noteTypeDays = await db.NoteTypes
            .AsNoTracking()
            .Where(type => type.Id == note.NoteTypeId)
            .Select(type => type.DefaultDueDays)
            .FirstOrDefaultAsync(cancellationToken);

        return noteTypeDays.HasValue
            ? (
                submittedAtUtc.AddDays(noteTypeDays.Value),
                "NoteType")
            : (null, "None");
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

    private sealed record RuleResolution(NoteRoutingRule? MatchedRule, string Specificity, string Reason);
    private sealed record SelectedRoutingUser(Guid UserId, string DisplayNameAr, int ActiveAssignmentCount, DateTimeOffset? LastAssignedAtUtc);
    private sealed record RoutingRuleShape(
        Guid NoteTypeId,
        ScopeType RuleScopeType,
        Guid? RegionId,
        Guid? FacilityId,
        Guid? FacilityUnitId,
        NoteRoutingProcessingTargetType ProcessingTargetType,
        Guid? ProcessingDepartmentId,
        Guid? ProcessingRoleId,
        Guid? ReviewerRoleId);
    private sealed record RoutingRuleIdentity(
        Guid NoteTypeId,
        ScopeType RuleScopeType,
        Guid? RegionId,
        Guid? FacilityId,
        Guid? FacilityUnitId,
        int Priority);
}
