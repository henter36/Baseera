namespace Baseera.Application.Escalations;

using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Escalations;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

public interface IEscalationPolicyService
{
    Task<PagedResult<EscalationPolicyDto>> ListAsync(EscalationPolicyQuery query, CancellationToken cancellationToken = default);
    Task<EscalationPolicyDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EscalationPolicyDto> CreateAsync(CreateEscalationPolicyRequest request, CancellationToken cancellationToken = default);
    Task<EscalationPolicyDto> UpdateAsync(Guid id, UpdateEscalationPolicyRequest request, CancellationToken cancellationToken = default);
    Task<EscalationPolicyDto> ActivateAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default);
    Task<EscalationPolicyDto> DeactivateAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default);
}

public interface IEscalationRuleService
{
    Task<IReadOnlyList<EscalationRuleDto>> ListAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<EscalationRuleDto> CreateAsync(Guid policyId, CreateEscalationRuleRequest request, CancellationToken cancellationToken = default);
    Task<EscalationRuleDto> UpdateAsync(Guid id, UpdateEscalationRuleRequest request, CancellationToken cancellationToken = default);
    Task<EscalationRuleDto> EnableAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default);
    Task<EscalationRuleDto> DisableAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default);
}

public interface IEscalationProcessor
{
    Task<EscalationRunResult> RunAsync(string leaseOwner, CancellationToken cancellationToken = default);
}

public interface IBackgroundJobLeaseService
{
    Task<bool> TryAcquireAsync(string jobName, string owner, TimeSpan duration, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> ListAsync(NotificationQuery query, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);
    Task<NotificationDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NotificationDto> MarkReadAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default);
    Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default);
    Task<NotificationDto> ArchiveAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default);
}

public interface IEscalationOccurrenceService
{
    Task<PagedResult<EscalationOccurrenceDto>> ListAsync(EscalationOccurrenceQuery query, CancellationToken cancellationToken = default);
    Task<EscalationOccurrenceDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task RetryAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class EscalationPolicyService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IAuditService audit,
    TimeProvider timeProvider) : IEscalationPolicyService, IEscalationRuleService
{
    public async Task<PagedResult<EscalationPolicyDto>> ListAsync(EscalationPolicyQuery query, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.EscalationsView);
        var source = db.EscalationPolicies.AsNoTracking();
        if (query.TargetType.HasValue)
        {
            source = source.Where(p => p.TargetType == query.TargetType);
        }

        if (query.IsEnabled.HasValue)
        {
            source = source.Where(p => p.IsEnabled == query.IsEnabled);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            source = source.Where(p => p.Code.Contains(term) || p.NameAr.Contains(term));
        }

        source = query.SortBy?.ToLowerInvariant() switch
        {
            "code" => query.SortDesc ? source.OrderByDescending(p => p.Code) : source.OrderBy(p => p.Code),
            "name" => query.SortDesc ? source.OrderByDescending(p => p.NameAr) : source.OrderBy(p => p.NameAr),
            _ => query.SortDesc ? source.OrderByDescending(p => p.CreatedAtUtc) : source.OrderBy(p => p.CreatedAtUtc)
        };

        var total = await source.CountAsync(cancellationToken);
        var items = await source.Skip(query.Skip).Take(query.Take).Select(p => ToPolicyDto(p)).ToListAsync(cancellationToken);
        return new PagedResult<EscalationPolicyDto> { Items = items, Page = query.Page, PageSize = query.Take, TotalCount = total };
    }

    public async Task<EscalationPolicyDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.EscalationsView);
        var policy = await db.EscalationPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return policy is null ? null : ToPolicyDto(policy);
    }

    public async Task<EscalationPolicyDto> CreateAsync(CreateEscalationPolicyRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.EscalationsManage);
        var now = timeProvider.GetUtcNow();
        var actor = RequireUser();
        var policy = new EscalationPolicy
        {
            Code = request.Code.Trim(),
            NameAr = request.NameAr.Trim(),
            Description = request.Description?.Trim(),
            TargetType = request.TargetType,
            ScopeType = request.ScopeType,
            RegionId = request.RegionId,
            FacilityId = request.FacilityId,
            FacilityUnitId = request.FacilityUnitId,
            CreatedByUserId = actor,
            CreatedAtUtc = now
        };
        db.Add(policy);
        await audit.WriteAsync(new AuditEntry { Action = "EscalationPolicyCreated", Module = "Escalations", EntityType = nameof(EscalationPolicy), EntityId = policy.Id.ToString(), NewValues = new { policy.Code, policy.NameAr } }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToPolicyDto(policy);
    }

    public async Task<EscalationPolicyDto> UpdateAsync(Guid id, UpdateEscalationPolicyRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.EscalationsManage);
        var policy = await LoadPolicyAsync(id, cancellationToken);
        EnsureRowVersion(policy.RowVersion, request.RowVersion);
        var old = new { policy.NameAr, policy.Description, policy.ScopeType, policy.RegionId, policy.FacilityId, policy.FacilityUnitId };
        policy.NameAr = request.NameAr.Trim();
        policy.Description = request.Description?.Trim();
        policy.ScopeType = request.ScopeType;
        policy.RegionId = request.RegionId;
        policy.FacilityId = request.FacilityId;
        policy.FacilityUnitId = request.FacilityUnitId;
        policy.UpdatedAtUtc = timeProvider.GetUtcNow();
        await audit.WriteAsync(new AuditEntry { Action = "EscalationPolicyUpdated", Module = "Escalations", EntityType = nameof(EscalationPolicy), EntityId = policy.Id.ToString(), OldValues = old, NewValues = new { policy.NameAr, policy.Description, policy.ScopeType, policy.RegionId, policy.FacilityId, policy.FacilityUnitId } }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToPolicyDto(policy);
    }

    public async Task<EscalationPolicyDto> ActivateAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.EscalationsActivate);
        var policy = await LoadPolicyAsync(id, cancellationToken);
        EnsureRowVersion(policy.RowVersion, request.RowVersion);
        if (!await db.EscalationRules.AnyAsync(r => r.EscalationPolicyId == id && r.IsEnabled, cancellationToken))
        {
            throw new InvalidOperationException("لا يمكن تفعيل سياسة دون قاعدة مفعلة واحدة على الأقل.");
        }

        policy.IsEnabled = true;
        policy.ActivatedAtUtc = timeProvider.GetUtcNow();
        policy.ActivatedByUserId = RequireUser();
        await audit.WriteAsync(new AuditEntry { Action = "EscalationPolicyActivated", Module = "Escalations", EntityType = nameof(EscalationPolicy), EntityId = policy.Id.ToString() }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToPolicyDto(policy);
    }

    public async Task<EscalationPolicyDto> DeactivateAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.EscalationsActivate);
        var policy = await LoadPolicyAsync(id, cancellationToken);
        EnsureRowVersion(policy.RowVersion, request.RowVersion);
        policy.IsEnabled = false;
        policy.DeactivatedAtUtc = timeProvider.GetUtcNow();
        policy.DeactivatedByUserId = RequireUser();
        await audit.WriteAsync(new AuditEntry { Action = "EscalationPolicyDeactivated", Module = "Escalations", EntityType = nameof(EscalationPolicy), EntityId = policy.Id.ToString() }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToPolicyDto(policy);
    }

    public async Task ArchiveAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.EscalationsManage);
        var policy = await LoadPolicyAsync(id, cancellationToken);
        EnsureRowVersion(policy.RowVersion, request.RowVersion);
        policy.IsDeleted = true;
        policy.DeletedAtUtc = timeProvider.GetUtcNow();
        policy.IsEnabled = false;
        await audit.WriteAsync(new AuditEntry { Action = "EscalationPolicyArchived", Module = "Escalations", EntityType = nameof(EscalationPolicy), EntityId = policy.Id.ToString() }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.EscalationsManage);
        var policy = await db.EscalationPoliciesIncludingDeleted.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("سياسة التصعيد غير موجودة.");
        EnsureRowVersion(policy.RowVersion, request.RowVersion);
        policy.IsDeleted = false;
        policy.DeletedAtUtc = null;
        policy.DeletedBy = null;
        await audit.WriteAsync(new AuditEntry { Action = "EscalationPolicyRestored", Module = "Escalations", EntityType = nameof(EscalationPolicy), EntityId = policy.Id.ToString() }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EscalationRuleDto>> ListAsync(Guid policyId, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.EscalationsView);
        if (!await db.EscalationPolicies.AnyAsync(p => p.Id == policyId, cancellationToken))
        {
            throw new KeyNotFoundException("سياسة التصعيد غير موجودة.");
        }

        return await db.EscalationRules.AsNoTracking()
            .Where(r => r.EscalationPolicyId == policyId)
            .OrderBy(r => r.Level)
            .Select(r => ToRuleDto(r))
            .ToListAsync(cancellationToken);
    }

    public async Task<EscalationRuleDto> CreateAsync(Guid policyId, CreateEscalationRuleRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.EscalationsManage);
        _ = await LoadPolicyAsync(policyId, cancellationToken);
        await ValidateSpecificUserAsync(request.SpecificRecipientUserId, cancellationToken);
        var rule = new EscalationRule
        {
            EscalationPolicyId = policyId,
            Level = request.Level,
            Priority = request.Priority,
            TriggerType = request.TriggerType,
            ThresholdDays = request.ThresholdDays,
            RepeatEveryDays = request.RepeatEveryDays,
            MaximumOccurrences = request.MaximumOccurrences,
            RecipientStrategy = request.RecipientStrategy,
            RecipientRoleCode = request.RecipientRoleCode?.Trim(),
            SpecificRecipientUserId = request.SpecificRecipientUserId,
            TitleTemplateAr = request.TitleTemplateAr.Trim(),
            MessageTemplateAr = request.MessageTemplateAr.Trim(),
            IsEnabled = true,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        db.Add(rule);
        await audit.WriteAsync(new AuditEntry { Action = "EscalationRuleCreated", Module = "Escalations", EntityType = nameof(EscalationRule), EntityId = rule.Id.ToString(), NewValues = new { policyId, rule.Level } }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToRuleDto(rule);
    }

    public async Task<EscalationRuleDto> UpdateAsync(Guid id, UpdateEscalationRuleRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.EscalationsManage);
        var rule = await LoadRuleAsync(id, cancellationToken);
        if (await db.EscalationOccurrences.AnyAsync(o => o.RuleId == id, cancellationToken))
        {
            throw new InvalidOperationException("لا يمكن تعديل قاعدة نتج عنها تصعيد؛ عطّلها وأنشئ قاعدة جديدة.");
        }

        EnsureRowVersion(rule.RowVersion, request.RowVersion);
        await ValidateSpecificUserAsync(request.SpecificRecipientUserId, cancellationToken);
        rule.Priority = request.Priority;
        rule.TriggerType = request.TriggerType;
        rule.ThresholdDays = request.ThresholdDays;
        rule.RepeatEveryDays = request.RepeatEveryDays;
        rule.MaximumOccurrences = request.MaximumOccurrences;
        rule.RecipientStrategy = request.RecipientStrategy;
        rule.RecipientRoleCode = request.RecipientRoleCode?.Trim();
        rule.SpecificRecipientUserId = request.SpecificRecipientUserId;
        rule.TitleTemplateAr = request.TitleTemplateAr.Trim();
        rule.MessageTemplateAr = request.MessageTemplateAr.Trim();
        rule.UpdatedAtUtc = timeProvider.GetUtcNow();
        await audit.WriteAsync(new AuditEntry { Action = "EscalationRuleUpdated", Module = "Escalations", EntityType = nameof(EscalationRule), EntityId = rule.Id.ToString() }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToRuleDto(rule);
    }

    public Task<EscalationRuleDto> EnableAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default) =>
        SetRuleEnabledAsync(id, request, true, "EscalationRuleEnabled", cancellationToken);

    public Task<EscalationRuleDto> DisableAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default) =>
        SetRuleEnabledAsync(id, request, false, "EscalationRuleDisabled", cancellationToken);

    private async Task<EscalationRuleDto> SetRuleEnabledAsync(Guid id, RowVersionRequest request, bool enabled, string auditAction, CancellationToken cancellationToken)
    {
        Ensure(PermissionCodes.EscalationsManage);
        var rule = await LoadRuleAsync(id, cancellationToken);
        EnsureRowVersion(rule.RowVersion, request.RowVersion);
        rule.IsEnabled = enabled;
        await audit.WriteAsync(new AuditEntry { Action = auditAction, Module = "Escalations", EntityType = nameof(EscalationRule), EntityId = rule.Id.ToString() }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToRuleDto(rule);
    }

    private async Task ValidateSpecificUserAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is null)
        {
            return;
        }

        var valid = await db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.ProvisioningStatus == UserProvisioningStatus.Active, cancellationToken);
        if (!valid)
        {
            throw new InvalidOperationException("المستخدم المحدد غير صالح لاستقبال التصعيد.");
        }
    }

    private async Task<EscalationPolicy> LoadPolicyAsync(Guid id, CancellationToken cancellationToken) =>
        await db.EscalationPolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("سياسة التصعيد غير موجودة.");

    private async Task<EscalationRule> LoadRuleAsync(Guid id, CancellationToken cancellationToken) =>
        await db.EscalationRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("قاعدة التصعيد غير موجودة.");

    private void Ensure(string permission)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تنفيذ هذه العملية.");
        }
    }

    private Guid RequireUser() => currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");

    private static void EnsureRowVersion(byte[] current, string incoming)
    {
        try
        {
            if (!current.SequenceEqual(Convert.FromBase64String(incoming)))
            {
                throw new InvalidOperationException("تم تعديل السجل بواسطة مستخدم آخر. أعد التحميل ثم حاول مجددًا.");
            }
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("إصدار السجل غير صالح.");
        }
    }

    private static EscalationPolicyDto ToPolicyDto(EscalationPolicy p) =>
        new(p.Id, p.Code, p.NameAr, p.Description, p.TargetType, p.IsEnabled, p.ScopeType, p.RegionId, p.FacilityId, p.FacilityUnitId, p.Rules.Count, Convert.ToBase64String(p.RowVersion));

    private static EscalationRuleDto ToRuleDto(EscalationRule r) =>
        new(r.Id, r.EscalationPolicyId, r.Level, r.Priority, r.TriggerType, r.ThresholdDays, r.RepeatEveryDays, r.MaximumOccurrences, r.RecipientStrategy, r.RecipientRoleCode, r.SpecificRecipientUserId, r.TitleTemplateAr, r.MessageTemplateAr, r.IsEnabled, Convert.ToBase64String(r.RowVersion));
}

internal sealed record EscalationCandidate(
    EscalationTargetType TargetType,
    Guid TargetId,
    string ReferenceNumber,
    DateTimeOffset DueAtUtc,
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId,
    ClassificationLevel Classification,
    Guid? CurrentAssignedUserId,
    string TargetCycleKey);

public sealed class EscalationProcessor(
    IBaseeraDbContext db,
    IBackgroundJobLeaseService leases,
    IAuditService audit,
    TimeProvider timeProvider) : IEscalationProcessor
{
    private const string JobName = "EscalationProcessing";

    public async Task<EscalationRunResult> RunAsync(string leaseOwner, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        if (!await leases.TryAcquireAsync(JobName, leaseOwner, TimeSpan.FromSeconds(60), cancellationToken))
        {
            return new EscalationRunResult(0, 0, 0, 0, 0, 0);
        }

        await audit.WriteAsync(new AuditEntry { Action = "EscalationRunStarted", Module = "Escalations", EntityType = JobName, NewValues = new { leaseOwner } }, cancellationToken);
        var policies = await db.EscalationPolicies.AsNoTracking()
            .Where(p => p.IsEnabled)
            .Include(p => p.Rules)
            .ToListAsync(cancellationToken);

        var result = new MutableRunResult { PoliciesEvaluated = policies.Count };
        foreach (var policy in policies)
        {
            foreach (var rule in policy.Rules.Where(r => r.IsEnabled).OrderBy(r => r.Level))
            {
                var candidates = await LoadCandidatesAsync(policy, rule, now, cancellationToken);
                result.CandidatesEvaluated += candidates.Count;
                foreach (var candidate in candidates)
                {
                    await ProcessCandidateAsync(policy, rule, candidate, now, result, cancellationToken);
                }
            }
        }

        var final = result.ToResult();
        await audit.WriteAsync(new AuditEntry { Action = "EscalationRunCompleted", Module = "Escalations", EntityType = JobName, NewValues = final }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return final;
    }

    private async Task<List<EscalationCandidate>> LoadCandidatesAsync(EscalationPolicy policy, EscalationRule rule, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (policy.TargetType == EscalationTargetType.OperationalNote)
        {
            var query = db.OperationalNotes.AsNoTracking()
                .Where(n => n.DueAtUtc.HasValue && n.Status != NoteStatus.Closed && n.Status != NoteStatus.Cancelled);
            query = ApplyPolicyScope(query, policy);
            query = ApplyTrigger(query, rule, now);
            var candidates = await query.Take(100).Select(n => new EscalationCandidate(
                EscalationTargetType.OperationalNote,
                n.Id,
                n.ReferenceNumber,
                n.DueAtUtc!.Value,
                n.ScopeType,
                n.RegionId,
                n.FacilityId,
                n.FacilityUnitId,
                n.Classification,
                db.NoteAssignments.Where(a => a.OperationalNoteId == n.Id && a.IsCurrent).Select(a => a.AssignedToUserId).FirstOrDefault(),
                EscalationRuleLogic.TargetCycleKey(EscalationTargetType.OperationalNote, n.Id, n.ReopenedAtUtc ?? n.SubmittedAtUtc ?? n.CreatedAtUtc)))
                .ToListAsync(cancellationToken);
            return candidates.Where(c => MatchesTrigger(c, rule, now)).ToList();
        }

        var caQuery = db.CorrectiveActions.AsNoTracking()
            .Where(a => a.DueAtUtc.HasValue && a.Status != CorrectiveActionStatus.Completed && a.Status != CorrectiveActionStatus.Cancelled)
            .Join(db.OperationalNotes, a => a.OperationalNoteId, n => n.Id, (a, n) => new { Action = a, Note = n });
        caQuery = policy.ScopeType switch
        {
            ScopeType.Global => caQuery,
            ScopeType.Headquarters => caQuery.Where(x => x.Note.ScopeType == ScopeType.Headquarters),
            ScopeType.Region => caQuery.Where(x => x.Note.RegionId == policy.RegionId),
            ScopeType.Facility => caQuery.Where(x => x.Note.FacilityId == policy.FacilityId),
            ScopeType.FacilityUnit => caQuery.Where(x => x.Note.FacilityUnitId == policy.FacilityUnitId),
            _ => caQuery.Where(_ => false)
        };
        caQuery = rule.TriggerType == EscalationTriggerType.DueSoon
            ? caQuery.Where(x => x.Action.DueAtUtc >= now && x.Action.DueAtUtc <= now.AddDays(rule.ThresholdDays))
            : caQuery.Where(x => x.Action.DueAtUtc < now);
        var actionCandidates = await caQuery.Take(100).Select(x => new EscalationCandidate(
            EscalationTargetType.CorrectiveAction,
            x.Action.Id,
            x.Action.ReferenceNumber,
            x.Action.DueAtUtc!.Value,
            x.Note.ScopeType,
            x.Note.RegionId,
            x.Note.FacilityId,
            x.Note.FacilityUnitId,
            x.Action.Classification,
            db.CorrectiveActionAssignments.Where(a => a.CorrectiveActionId == x.Action.Id && a.IsCurrent).Select(a => a.AssignedToUserId).FirstOrDefault(),
            EscalationRuleLogic.TargetCycleKey(EscalationTargetType.CorrectiveAction, x.Action.Id, x.Action.ReopenedAtUtc ?? x.Action.SubmittedAtUtc ?? x.Action.CreatedAtUtc)))
            .ToListAsync(cancellationToken);
        return actionCandidates.Where(c => MatchesTrigger(c, rule, now)).ToList();
    }

    private async Task ProcessCandidateAsync(
        EscalationPolicy policy,
        EscalationRule rule,
        EscalationCandidate candidate,
        DateTimeOffset now,
        MutableRunResult result,
        CancellationToken cancellationToken)
    {
        var occurrenceNumber = await NextOccurrenceNumberOrZeroAsync(rule, candidate, now, cancellationToken);
        if (occurrenceNumber == 0)
        {
            return;
        }

        var recipients = await ResolveRecipientsAsync(rule, candidate, cancellationToken);
        var occurrenceKey = BuildOccurrenceKey(rule.Id, candidate, occurrenceNumber);
        if (await db.EscalationOccurrences.AnyAsync(o => o.OccurrenceKey == occurrenceKey, cancellationToken))
        {
            return;
        }

        var occurrence = new EscalationOccurrence
        {
            PolicyId = policy.Id,
            RuleId = rule.Id,
            TargetType = candidate.TargetType,
            TargetId = candidate.TargetId,
            TargetReferenceNumber = candidate.ReferenceNumber,
            EscalationLevel = rule.Level,
            TriggerType = rule.TriggerType,
            OccurrenceNumber = occurrenceNumber,
            OccurrenceKey = occurrenceKey,
            DueAtUtc = candidate.DueAtUtc,
            DetectedAtUtc = now,
            RecipientCount = recipients.Count,
            Status = recipients.Count == 0 ? EscalationOccurrenceStatus.Suppressed : EscalationOccurrenceStatus.NotificationsCreated,
            SuppressionReason = recipients.Count == 0 ? "لا يوجد مستلم صالح داخل نطاق الهدف." : null,
            MetadataJson = $"{{\"targetCycleKey\":\"{candidate.TargetCycleKey}\"}}"
        };
        db.Add(occurrence);
        result.OccurrencesCreated++;
        if (recipients.Count == 0)
        {
            result.Suppressed++;
            await audit.WriteAsync(new AuditEntry { Action = "EscalationOccurrenceSuppressed", Module = "Escalations", EntityType = nameof(EscalationOccurrence), EntityId = occurrence.Id.ToString(), Reason = occurrence.SuppressionReason }, cancellationToken);
            return;
        }

        foreach (var recipientId in recipients)
        {
            var dedup = EscalationRuleLogic.DeduplicationKey(occurrenceKey, recipientId);
            if (await db.Notifications.AnyAsync(n => n.DeduplicationKey == dedup, cancellationToken))
            {
                continue;
            }

            var notification = new Notification
            {
                RecipientUserId = recipientId,
                EscalationOccurrenceId = occurrence.Id,
                TargetType = candidate.TargetType,
                TargetId = candidate.TargetId,
                TargetReferenceNumber = candidate.ReferenceNumber,
                TitleAr = Render(rule.TitleTemplateAr, candidate),
                MessageAr = Render(rule.MessageTemplateAr, candidate),
                Priority = rule.Priority,
                Status = NotificationStatus.Unread,
                CreatedAtUtc = now,
                DeduplicationKey = dedup,
                Classification = candidate.Classification
            };
            db.Add(notification);
            db.Add(new NotificationDeliveryAttempt
            {
                NotificationId = notification.Id,
                Channel = NotificationChannel.InApp,
                AttemptNumber = 1,
                Status = NotificationDeliveryStatus.Delivered,
                StartedAtUtc = now,
                CompletedAtUtc = now
            });
            result.NotificationsCreated++;
            await audit.WriteAsync(new AuditEntry { Action = "NotificationCreated", Module = "Notifications", EntityType = nameof(Notification), EntityId = notification.Id.ToString(), NewValues = new { recipientId, candidate.TargetType, candidate.TargetId } }, cancellationToken);
        }

        await audit.WriteAsync(new AuditEntry { Action = "EscalationOccurrenceCreated", Module = "Escalations", EntityType = nameof(EscalationOccurrence), EntityId = occurrence.Id.ToString(), NewValues = new { occurrence.PolicyId, occurrence.RuleId, occurrence.TargetId, occurrence.RecipientCount } }, cancellationToken);
    }

    private async Task<int> NextOccurrenceNumberOrZeroAsync(EscalationRule rule, EscalationCandidate candidate, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var prefix = $"{rule.Id:N}:{candidate.TargetType}:{candidate.TargetId:N}:{rule.Level}:{candidate.TargetCycleKey}:";
        var previous = await db.EscalationOccurrences
            .Where(o => o.OccurrenceKey.StartsWith(prefix))
            .OrderByDescending(o => o.OccurrenceNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (previous is null)
        {
            return 1;
        }

        if (rule.MaximumOccurrences.HasValue && previous.OccurrenceNumber >= rule.MaximumOccurrences.Value)
        {
            return 0;
        }

        if (!rule.RepeatEveryDays.HasValue || now < previous.DetectedAtUtc.AddDays(rule.RepeatEveryDays.Value))
        {
            return 0;
        }

        return previous.OccurrenceNumber + 1;
    }

    private async Task<List<Guid>> ResolveRecipientsAsync(EscalationRule rule, EscalationCandidate candidate, CancellationToken cancellationToken)
    {
        IQueryable<User> query = db.Users.Where(u => u.IsActive && u.ProvisioningStatus == UserProvisioningStatus.Active);
        query = rule.RecipientStrategy switch
        {
            EscalationRecipientStrategy.CurrentAssignedUser when candidate.CurrentAssignedUserId.HasValue => query.Where(u => u.Id == candidate.CurrentAssignedUserId),
            EscalationRecipientStrategy.SpecificUser when rule.SpecificRecipientUserId.HasValue => query.Where(u => u.Id == rule.SpecificRecipientUserId),
            EscalationRecipientStrategy.SpecificRoleInTargetScope => UsersInRole(query, rule.RecipientRoleCode!),
            EscalationRecipientStrategy.FacilityDirector => UsersInRole(query, RoleCodes.FacilityDirector),
            EscalationRecipientStrategy.RegionalDirector => UsersInRole(query, RoleCodes.RegionalDirector),
            EscalationRecipientStrategy.HeadquartersExecutive => UsersInRole(query, RoleCodes.HeadquartersExecutive),
            _ => query.Where(_ => false)
        };

        query = UsersWithPermission(query, PermissionCodes.NotificationsViewOwn);
        var users = await query.Select(u => u.Id).Distinct().ToListAsync(cancellationToken);
        var scoped = new List<Guid>();
        foreach (var userId in users)
        {
            if (await UserIntersectsTargetScopeAsync(userId, candidate, cancellationToken))
            {
                scoped.Add(userId);
            }
        }

        return scoped.Distinct().ToList();
    }

    private IQueryable<User> UsersInRole(IQueryable<User> query, string roleCode) =>
        query.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.Role.Code == roleCode));

    private IQueryable<User> UsersWithPermission(IQueryable<User> query, string permission) =>
        query.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.Role.RolePermissions.Any(rp => rp.Permission.Code == permission)));

    private async Task<bool> UserIntersectsTargetScopeAsync(Guid userId, EscalationCandidate target, CancellationToken cancellationToken)
    {
        var scopes = await db.UserScopes.Where(s => s.UserId == userId && s.IsActive).ToListAsync(cancellationToken);
        if (scopes.Any(s => s.ScopeType == ScopeType.Global))
        {
            return true;
        }

        return target.ScopeType switch
        {
            ScopeType.Global => scopes.Any(s => s.ScopeType == ScopeType.Global),
            ScopeType.Headquarters => scopes.Any(s => s.ScopeType is ScopeType.Global or ScopeType.Headquarters),
            ScopeType.Region => target.RegionId.HasValue && scopes.Any(s => s.RegionId == target.RegionId),
            ScopeType.Facility => target.FacilityId.HasValue && scopes.Any(s => s.FacilityId == target.FacilityId || s.RegionId == target.RegionId),
            ScopeType.FacilityUnit => target.FacilityUnitId.HasValue && scopes.Any(s => s.FacilityUnitId == target.FacilityUnitId || s.FacilityId == target.FacilityId || s.RegionId == target.RegionId),
            _ => false
        };
    }

    private static IQueryable<OperationalNote> ApplyPolicyScope(IQueryable<OperationalNote> query, EscalationPolicy policy) =>
        policy.ScopeType switch
        {
            ScopeType.Global => query,
            ScopeType.Headquarters => query.Where(n => n.ScopeType == ScopeType.Headquarters),
            ScopeType.Region => query.Where(n => n.RegionId == policy.RegionId),
            ScopeType.Facility => query.Where(n => n.FacilityId == policy.FacilityId),
            ScopeType.FacilityUnit => query.Where(n => n.FacilityUnitId == policy.FacilityUnitId),
            _ => query.Where(_ => false)
        };

    private static IQueryable<OperationalNote> ApplyTrigger(IQueryable<OperationalNote> query, EscalationRule rule, DateTimeOffset now) =>
        rule.TriggerType == EscalationTriggerType.DueSoon
            ? query.Where(n => n.DueAtUtc >= now && n.DueAtUtc <= now.AddDays(rule.ThresholdDays))
            : query.Where(n => n.DueAtUtc < now);

    private static string BuildOccurrenceKey(Guid ruleId, EscalationCandidate candidate, int occurrenceNumber) =>
        EscalationRuleLogic.OccurrenceKey(ruleId, candidate.TargetType, candidate.TargetId, candidate.TargetCycleKey, occurrenceNumber);

    private static bool MatchesTrigger(EscalationCandidate candidate, EscalationRule rule, DateTimeOffset now) =>
        rule.TriggerType == EscalationTriggerType.DueSoon
            ? EscalationRuleLogic.IsDueSoon(candidate.DueAtUtc, now, rule.ThresholdDays)
            : EscalationRuleLogic.IsOverdue(candidate.DueAtUtc, now);

    private static string Render(string template, EscalationCandidate candidate) =>
        template.Replace("{reference}", candidate.ReferenceNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("{targetType}", EscalationDisplay.TargetTypeAr(candidate.TargetType), StringComparison.OrdinalIgnoreCase);

    private sealed class MutableRunResult
    {
        public int PoliciesEvaluated { get; set; }
        public int CandidatesEvaluated { get; set; }
        public int OccurrencesCreated { get; set; }
        public int NotificationsCreated { get; set; }
        public int Suppressed { get; set; }
        public int Failed { get; set; }
        public EscalationRunResult ToResult() => new(PoliciesEvaluated, CandidatesEvaluated, OccurrencesCreated, NotificationsCreated, Suppressed, Failed);
    }
}

public sealed class BackgroundJobLeaseService(IBaseeraDbContext db, TimeProvider timeProvider) : IBackgroundJobLeaseService
{
    public async Task<bool> TryAcquireAsync(string jobName, string owner, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var lease = await db.BackgroundJobLeases.FirstOrDefaultAsync(l => l.JobName == jobName, cancellationToken);
        if (lease is null)
        {
            db.Add(new BackgroundJobLease
            {
                JobName = jobName,
                LeaseOwner = owner,
                LeaseAcquiredAtUtc = now,
                LeaseExpiresAtUtc = now.Add(duration),
                HeartbeatAtUtc = now
            });
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (lease.LeaseExpiresAtUtc > now && lease.LeaseOwner != owner)
        {
            return false;
        }

        lease.LeaseOwner = owner;
        lease.LeaseAcquiredAtUtc = now;
        lease.LeaseExpiresAtUtc = now.Add(duration);
        lease.HeartbeatAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class NotificationService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IAuditService audit,
    TimeProvider timeProvider) : INotificationService
{
    public async Task<PagedResult<NotificationDto>> ListAsync(NotificationQuery query, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotificationsViewOwn);
        var userId = RequireUser();
        var source = db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == userId);
        if (query.Status.HasValue)
        {
            source = source.Where(n => n.Status == query.Status);
        }
        else
        {
            source = source.Where(n => n.Status != NotificationStatus.Archived);
        }

        if (query.TargetType.HasValue)
        {
            source = source.Where(n => n.TargetType == query.TargetType);
        }

        if (query.Priority.HasValue)
        {
            source = source.Where(n => n.Priority == query.Priority);
        }

        if (query.CreatedFrom.HasValue)
        {
            source = source.Where(n => n.CreatedAtUtc >= query.CreatedFrom);
        }

        if (query.CreatedTo.HasValue)
        {
            source = source.Where(n => n.CreatedAtUtc <= query.CreatedTo);
        }

        source = query.SortBy?.ToLowerInvariant() switch
        {
            "priority" => query.SortDesc ? source.OrderByDescending(n => n.Priority).ThenByDescending(n => n.CreatedAtUtc) : source.OrderBy(n => n.Priority).ThenByDescending(n => n.CreatedAtUtc),
            "status" => query.SortDesc ? source.OrderByDescending(n => n.Status).ThenByDescending(n => n.CreatedAtUtc) : source.OrderBy(n => n.Status).ThenByDescending(n => n.CreatedAtUtc),
            _ => query.SortDesc ? source.OrderByDescending(n => n.CreatedAtUtc) : source.OrderBy(n => n.CreatedAtUtc)
        };

        var total = await source.CountAsync(cancellationToken);
        var items = await source.Skip(query.Skip).Take(query.Take).Select(n => ToDto(n)).ToListAsync(cancellationToken);
        return new PagedResult<NotificationDto> { Items = items, Page = query.Page, PageSize = query.Take, TotalCount = total };
    }

    public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotificationsViewOwn);
        var userId = RequireUser();
        return db.Notifications.CountAsync(n => n.RecipientUserId == userId && n.Status == NotificationStatus.Unread, cancellationToken);
    }

    public async Task<NotificationDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotificationsViewOwn);
        var userId = RequireUser();
        var notification = await db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id && n.RecipientUserId == userId, cancellationToken);
        return notification is null ? null : ToDto(notification);
    }

    public async Task<NotificationDto> MarkReadAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotificationsMarkRead);
        var notification = await LoadOwnAsync(id, cancellationToken);
        EnsureRowVersion(notification.RowVersion, request.RowVersion);
        if (notification.Status == NotificationStatus.Unread)
        {
            notification.Status = NotificationStatus.Read;
            notification.ReadAtUtc = timeProvider.GetUtcNow();
            await audit.WriteAsync(new AuditEntry { Action = "NotificationRead", Module = "Notifications", EntityType = nameof(Notification), EntityId = notification.Id.ToString() }, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return ToDto(notification);
    }

    public async Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotificationsMarkRead);
        var userId = RequireUser();
        var now = timeProvider.GetUtcNow();
        var unread = await db.Notifications.Where(n => n.RecipientUserId == userId && n.Status == NotificationStatus.Unread).ToListAsync(cancellationToken);
        foreach (var item in unread)
        {
            item.Status = NotificationStatus.Read;
            item.ReadAtUtc = now;
        }

        await audit.WriteAsync(new AuditEntry { Action = "NotificationReadAll", Module = "Notifications", EntityType = nameof(Notification), NewValues = new { Count = unread.Count } }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }

    public async Task<NotificationDto> ArchiveAsync(Guid id, RowVersionRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotificationsArchiveOwn);
        var notification = await LoadOwnAsync(id, cancellationToken);
        EnsureRowVersion(notification.RowVersion, request.RowVersion);
        notification.Status = NotificationStatus.Archived;
        notification.ArchivedAtUtc = timeProvider.GetUtcNow();
        await audit.WriteAsync(new AuditEntry { Action = "NotificationArchived", Module = "Notifications", EntityType = nameof(Notification), EntityId = notification.Id.ToString() }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(notification);
    }

    private async Task<Notification> LoadOwnAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireUser();
        return await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.RecipientUserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("الإشعار غير موجود.");
    }

    private void Ensure(string permission)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تنفيذ هذه العملية.");
        }
    }

    private Guid RequireUser() => currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");

    private static void EnsureRowVersion(byte[] current, string incoming)
    {
        try
        {
            if (!current.SequenceEqual(Convert.FromBase64String(incoming)))
            {
                throw new InvalidOperationException("تم تعديل السجل بواسطة مستخدم آخر. أعد التحميل ثم حاول مجددًا.");
            }
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("إصدار السجل غير صالح.");
        }
    }

    private static NotificationDto ToDto(Notification n) =>
        new(n.Id, n.TargetType, n.TargetId, n.TargetReferenceNumber, n.TitleAr, n.MessageAr, n.Priority, n.Status, n.CreatedAtUtc, n.ReadAtUtc, n.ArchivedAtUtc, Convert.ToBase64String(n.RowVersion));
}

public sealed class EscalationOccurrenceService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IAuditService audit,
    TimeProvider timeProvider) : IEscalationOccurrenceService
{
    public async Task<PagedResult<EscalationOccurrenceDto>> ListAsync(EscalationOccurrenceQuery query, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.EscalationsViewOccurrences);
        var source = db.EscalationOccurrences.AsNoTracking();
        if (query.TargetType.HasValue)
        {
            source = source.Where(o => o.TargetType == query.TargetType);
        }

        if (query.Status.HasValue)
        {
            source = source.Where(o => o.Status == query.Status);
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderByDescending(o => o.DetectedAtUtc).Skip(query.Skip).Take(query.Take).Select(o => ToDto(o)).ToListAsync(cancellationToken);
        return new PagedResult<EscalationOccurrenceDto> { Items = items, Page = query.Page, PageSize = query.Take, TotalCount = total };
    }

    public async Task<EscalationOccurrenceDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.EscalationsViewOccurrences);
        var occurrence = await db.EscalationOccurrences.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        return occurrence is null ? null : ToDto(occurrence);
    }

    public async Task RetryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.EscalationsRetryFailed);
        var notifications = await db.Notifications.Where(n => n.EscalationOccurrenceId == id).ToListAsync(cancellationToken);
        if (notifications.Count == 0)
        {
            throw new KeyNotFoundException("حادثة التصعيد غير موجودة أو لا تحتوي إشعارات.");
        }

        var now = timeProvider.GetUtcNow();
        foreach (var notification in notifications)
        {
            var nextAttempt = await db.NotificationDeliveryAttempts
                .Where(a => a.NotificationId == notification.Id && a.Channel == NotificationChannel.InApp)
                .Select(a => a.AttemptNumber)
                .DefaultIfEmpty()
                .MaxAsync(cancellationToken) + 1;
            db.Add(new NotificationDeliveryAttempt
            {
                NotificationId = notification.Id,
                Channel = NotificationChannel.InApp,
                AttemptNumber = nextAttempt,
                Status = NotificationDeliveryStatus.Delivered,
                StartedAtUtc = now,
                CompletedAtUtc = now
            });
        }

        await audit.WriteAsync(new AuditEntry { Action = "NotificationDeliveryRetried", Module = "Notifications", EntityType = nameof(EscalationOccurrence), EntityId = id.ToString(), NewValues = new { Count = notifications.Count } }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private void Ensure(string permission)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تنفيذ هذه العملية.");
        }
    }

    private static EscalationOccurrenceDto ToDto(EscalationOccurrence o) =>
        new(o.Id, o.PolicyId, o.RuleId, o.TargetType, o.TargetId, o.TargetReferenceNumber, o.EscalationLevel, o.TriggerType, o.OccurrenceNumber, o.DueAtUtc, o.DetectedAtUtc, o.RecipientCount, o.Status, o.SuppressionReason);
}
