namespace Baseera.Application.Forms.Responses;

using Baseera.Application.Abstractions;
using Baseera.Application.Audit;
using Baseera.Domain.Audit;
using Baseera.Domain.Forms;
using Microsoft.EntityFrameworkCore;

public interface IFormResponseReviewService
{
    Task<FormResponseWorkspacePageDto> ListInboxAsync(
        string? status,
        Guid? campaignId,
        Guid? cycleId,
        Guid? regionId,
        Guid? facilityId,
        int? reviewLevel,
        DateTimeOffset? submittedFrom,
        DateTimeOffset? submittedTo,
        bool? overdue,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<FormResponseReviewDetailDto> GetReviewAsync(Guid responseId, CancellationToken cancellationToken = default);
    Task StartReviewAsync(Guid responseId, string rowVersion, CancellationToken cancellationToken = default);
    Task ReturnAsync(Guid responseId, FormResponseReturnRequest request, CancellationToken cancellationToken = default);
    Task ApproveAsync(Guid responseId, FormResponseApproveRequest request, CancellationToken cancellationToken = default);
    Task RejectAsync(Guid responseId, FormResponseRejectRequest request, CancellationToken cancellationToken = default);
    Task CloseAsync(Guid responseId, FormResponseCloseRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormResponseSubmissionDto>> ListSubmissionsAsync(Guid responseId, CancellationToken cancellationToken = default);
    Task<FormResponseSubmissionDto> GetSubmissionAsync(Guid responseId, int submissionNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormResponseHistoryDto>> GetHistoryAsync(Guid responseId, CancellationToken cancellationToken = default);
}

public sealed class FormResponseReviewService(
    IBaseeraDbContext db,
    IFormResponseAccessCoordinator access,
    IFormResponseCompletionEvaluator completion,
    IFormResponseService responseService,
    IAuditService audit,
    TimeProvider clock) : IFormResponseReviewService
{
    public async Task<FormResponseWorkspacePageDto> ListInboxAsync(
        string? status,
        Guid? campaignId,
        Guid? cycleId,
        Guid? regionId,
        Guid? facilityId,
        int? reviewLevel,
        DateTimeOffset? submittedFrom,
        DateTimeOffset? submittedTo,
        bool? overdue,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        access.EnsureReviewPermission();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var now = clock.GetUtcNow();

        var query =
            from r in db.FormResponses.AsNoTracking()
            join a in db.FormFacilityAssignments.AsNoTracking() on r.AssignmentId equals a.Id
            join c in db.FormCycles.AsNoTracking() on r.CycleId equals c.Id
            join camp in db.FormCampaigns.AsNoTracking() on r.CampaignId equals camp.Id
            join pol in db.FormCampaignResponsePolicies.AsNoTracking() on camp.Id equals pol.CampaignId
            where r.Status == FormResponseStatus.Submitted || r.Status == FormResponseStatus.UnderReview
            select new { r, a, c, camp, pol };

        if (campaignId.HasValue) query = query.Where(x => x.r.CampaignId == campaignId);
        if (cycleId.HasValue) query = query.Where(x => x.r.CycleId == cycleId);
        if (regionId.HasValue) query = query.Where(x => x.a.RegionIdAtAssignment == regionId);
        if (facilityId.HasValue) query = query.Where(x => x.r.FacilityId == facilityId);
        if (reviewLevel.HasValue) query = query.Where(x => x.r.CurrentReviewLevel == reviewLevel);
        if (submittedFrom.HasValue) query = query.Where(x => x.r.SubmittedAtUtc >= submittedFrom);
        if (submittedTo.HasValue) query = query.Where(x => x.r.SubmittedAtUtc <= submittedTo);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.camp.NameAr.Contains(term) || x.a.FacilityNameArAtAssignment.Contains(term));
        }

        var rows = await query
            .OrderBy(x => x.r.SubmittedAtUtc)
            .ThenBy(x => x.r.Id)
            .Take(pageSize * 5)
            .ToListAsync(cancellationToken);

        var items = new List<FormResponseWorkspaceItemDto>();
        foreach (var row in rows)
        {
            try { await access.EnsureFacilityInScopeAsync(row.r.FacilityId, cancellationToken); }
            catch (KeyNotFoundException) { continue; }

            var due = FormResponseWorkStatusResolver.ResolveEffectiveDueAt(row.c.DueAtUtc, row.r.DueAtUtcOverride);
            var isOverdue = FormResponseWorkStatusResolver.IsOverdue(row.r.Status, row.pol.CompletionBasis, due, now, completion);
            if (overdue == true && !isOverdue) continue;
            if (overdue == false && isOverdue) continue;
            if (!string.IsNullOrWhiteSpace(status)
                && !string.Equals(row.r.Status.ToString(), status, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var work = FormResponseWorkStatusResolver.Resolve(row.r.Status, isOverdue);
            items.Add(new FormResponseWorkspaceItemDto(
                row.a.Id, row.camp.Id, row.camp.Code, row.camp.NameAr, row.c.Id, row.c.OccurrenceKey,
                row.a.FacilityId, row.a.FacilityNameArAtAssignment, row.a.RegionIdAtAssignment, row.a.RegionNameArAtAssignment,
                row.c.OpenAtUtc, row.c.DueAtUtc, row.c.GraceEndsAtUtc, row.c.CloseAtUtc, due,
                row.r.Id, row.r.Status, work, isOverdue,
                completion.IsCompleted(row.pol.CompletionBasis, row.r.Status),
                row.r.DraftVersion, row.r.LastSavedAtUtc, row.r.SubmittedAtUtc,
                row.r.CurrentReviewLevel, row.pol.RequiredApprovalLevels,
                ResolveReviewActions(row.r, row.pol),
                Convert.ToBase64String(row.r.RowVersion)));
            if (items.Count >= pageSize) break;
        }

        return new FormResponseWorkspacePageDto(items, page, pageSize, items.Count);
    }

    public async Task<FormResponseReviewDetailDto> GetReviewAsync(Guid responseId, CancellationToken cancellationToken = default)
    {
        access.EnsureViewResponsesPermission();
        var response = await LoadResponseOrNotFoundAsync(responseId, track: false, cancellationToken);
        await access.EnsureFacilityInScopeAsync(response.FacilityId, cancellationToken);
        var workspace = await responseService.GetAssignmentResponseAsync(response.AssignmentId, cancellationToken);
        var submissions = await ListSubmissionsAsync(responseId, cancellationToken);
        var decisions = await db.FormResponseReviewDecisions.AsNoTracking()
            .Where(d => d.ResponseId == responseId)
            .OrderBy(d => d.ReviewedAtUtc)
            .Select(d => new FormResponseReviewDecisionDto(
                d.Id, d.SubmissionId, d.ReviewLevel, d.Decision, d.Reason, d.NewDueAtUtc,
                d.ReviewedByUserId, d.ReviewedAtUtc, d.FromStatus, d.ToStatus))
            .ToListAsync(cancellationToken);
        var comments = await db.FormResponseReviewComments.AsNoTracking()
            .Where(c => c.ResponseId == responseId)
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new FormResponseReviewCommentDto(
                c.Id, c.SubmissionId, c.ReviewDecisionId, c.FieldKey, c.Body, c.IsVisibleToRespondent,
                c.CreatedByUserId, c.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        var history = await GetHistoryAsync(responseId, cancellationToken);
        return new FormResponseReviewDetailDto(workspace, submissions, decisions, comments, history);
    }

    public Task StartReviewAsync(Guid responseId, string rowVersion, CancellationToken cancellationToken = default) =>
        db.ExecuteInTransactionAsync(async ct =>
        {
            access.EnsureReviewPermission();
            var response = await LoadResponseOrNotFoundAsync(responseId, track: true, ct);
            await access.EnsureFacilityInScopeAsync(response.FacilityId, ct);
            EnsureNotSelf(response);
            FormAccessHelper.EnsureRowVersion(response.RowVersion, rowVersion);
            var from = response.Status;
            FormResponseStateMachine.EnsureCanTransition(from, FormResponseStatus.UnderReview);
            var now = clock.GetUtcNow();
            var userId = access.UserId;
            var submission = await CurrentSubmissionAsync(response, ct);
            response.Status = FormResponseStatus.UnderReview;
            response.UpdatedAtUtc = now;
            db.Add(CreateDecision(response, submission, FormResponseReviewDecisionType.StartReview, null, null, from, FormResponseStatus.UnderReview, userId, now));
            await WriteAuditAsync(response, "FormResponseReviewStarted", from, FormResponseStatus.UnderReview, null, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

    public Task ReturnAsync(Guid responseId, FormResponseReturnRequest request, CancellationToken cancellationToken = default) =>
        db.ExecuteInTransactionAsync(async ct =>
        {
            access.EnsureReviewPermission();
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new ArgumentException("سبب الإعادة إلزامي.");
            }

            var response = await LoadResponseOrNotFoundAsync(responseId, track: true, ct);
            await access.EnsureFacilityInScopeAsync(response.FacilityId, ct);
            EnsureNotSelf(response);
            FormAccessHelper.EnsureRowVersion(response.RowVersion, request.RowVersion);
            ValidateNewDue(request.NewDueAtUtc);
            var from = response.Status;
            FormResponseStateMachine.EnsureCanTransition(from, FormResponseStatus.Returned);
            var now = clock.GetUtcNow();
            var userId = access.UserId;
            var submission = await CurrentSubmissionAsync(response, ct);
            var decision = CreateDecision(response, submission, FormResponseReviewDecisionType.Return, request.Reason, request.NewDueAtUtc, from, FormResponseStatus.Returned, userId, now);
            db.Add(decision);
            await AddCommentsAsync(response, submission, decision.Id, request.Comments, userId, now, ct);
            response.Status = FormResponseStatus.Returned;
            response.ReturnedAtUtc = now;
            response.DueAtUtcOverride = request.NewDueAtUtc;
            response.UpdatedAtUtc = now;
            await WriteHistoryAsync(response, "FormResponseReturned", from, FormResponseStatus.Returned, request.Reason, userId, now, ct);
            await WriteAuditAsync(response, "FormResponseReturned", from, FormResponseStatus.Returned, request.Reason, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

    public Task ApproveAsync(Guid responseId, FormResponseApproveRequest request, CancellationToken cancellationToken = default) =>
        db.ExecuteInTransactionAsync(async ct =>
        {
            access.EnsureApprovePermission();
            var response = await LoadResponseOrNotFoundAsync(responseId, track: true, ct);
            await access.EnsureFacilityInScopeAsync(response.FacilityId, ct);
            EnsureNotSelf(response);
            FormAccessHelper.EnsureRowVersion(response.RowVersion, request.RowVersion);
            var policy = await db.FormCampaignResponsePolicies.FirstAsync(p => p.CampaignId == response.CampaignId, ct);
            EnsureSeparationOfDuties(response, policy, access.UserId);
            await EnsureNoDuplicateApprovalAsync(response, access.UserId, ct);
            await EnsureNoConsecutiveLevelsAsync(response, policy, access.UserId, ct);

            var from = response.Status;
            if (from == FormResponseStatus.Submitted)
            {
                FormResponseStateMachine.EnsureCanTransition(from, FormResponseStatus.UnderReview);
                response.Status = FormResponseStatus.UnderReview;
                from = FormResponseStatus.UnderReview;
            }

            var level = Math.Max(1, response.CurrentReviewLevel);
            var target = FormResponseStateMachine.ResolveApprovalTargetStatus(level, policy.RequiredApprovalLevels);
            FormResponseStateMachine.EnsureCanTransition(from, target);
            var now = clock.GetUtcNow();
            var userId = access.UserId;
            var submission = await CurrentSubmissionAsync(response, ct);
            var decision = CreateDecision(response, submission, FormResponseReviewDecisionType.Approve, request.Reason, null, from, target, userId, now, level);
            db.Add(decision);
            await AddCommentsAsync(response, submission, decision.Id, request.Comments, userId, now, ct);
            response.Status = target;
            if (target == FormResponseStatus.Approved)
            {
                response.ApprovedAtUtc = now;
            }
            else
            {
                response.CurrentReviewLevel = level + 1;
            }

            response.UpdatedAtUtc = now;
            await WriteHistoryAsync(response, "FormResponseApproved", from, target, request.Reason, userId, now, ct);
            await WriteAuditAsync(response, "FormResponseApproved", from, target, request.Reason, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

    public Task RejectAsync(Guid responseId, FormResponseRejectRequest request, CancellationToken cancellationToken = default) =>
        db.ExecuteInTransactionAsync(async ct =>
        {
            access.EnsureReviewPermission();
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new ArgumentException("سبب الرفض إلزامي.");
            }

            var response = await LoadResponseOrNotFoundAsync(responseId, track: true, ct);
            await access.EnsureFacilityInScopeAsync(response.FacilityId, ct);
            EnsureNotSelf(response);
            FormAccessHelper.EnsureRowVersion(response.RowVersion, request.RowVersion);
            var from = response.Status;
            FormResponseStateMachine.EnsureCanTransition(from, FormResponseStatus.Rejected);
            var now = clock.GetUtcNow();
            var userId = access.UserId;
            var submission = await CurrentSubmissionAsync(response, ct);
            var decision = CreateDecision(response, submission, FormResponseReviewDecisionType.Reject, request.Reason, null, from, FormResponseStatus.Rejected, userId, now);
            db.Add(decision);
            await AddCommentsAsync(response, submission, decision.Id, request.Comments, userId, now, ct);
            response.Status = FormResponseStatus.Rejected;
            response.RejectedAtUtc = now;
            response.UpdatedAtUtc = now;
            await WriteHistoryAsync(response, "FormResponseRejected", from, FormResponseStatus.Rejected, request.Reason, userId, now, ct);
            await WriteAuditAsync(response, "FormResponseRejected", from, FormResponseStatus.Rejected, request.Reason, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

    public Task CloseAsync(Guid responseId, FormResponseCloseRequest request, CancellationToken cancellationToken = default) =>
        db.ExecuteInTransactionAsync(async ct =>
        {
            access.EnsureClosePermission();
            var response = await LoadResponseOrNotFoundAsync(responseId, track: true, ct);
            await access.EnsureFacilityInScopeAsync(response.FacilityId, ct);
            FormAccessHelper.EnsureRowVersion(response.RowVersion, request.RowVersion);
            var policy = await db.FormCampaignResponsePolicies.FirstAsync(p => p.CampaignId == response.CampaignId, ct);
            if (!FormResponseStateMachine.CanClose(response.Status, policy.ReviewMode))
            {
                throw new InvalidOperationException("لا يمكن إغلاق الرد في الحالة الحالية.");
            }

            var from = response.Status;
            FormResponseStateMachine.EnsureCanTransition(from, FormResponseStatus.Closed);
            var now = clock.GetUtcNow();
            var userId = access.UserId;
            var submission = await CurrentSubmissionAsync(response, ct);
            db.Add(CreateDecision(response, submission, FormResponseReviewDecisionType.Close, request.Reason, null, from, FormResponseStatus.Closed, userId, now));
            response.Status = FormResponseStatus.Closed;
            response.ClosedAtUtc = now;
            response.UpdatedAtUtc = now;
            await WriteHistoryAsync(response, "FormResponseClosed", from, FormResponseStatus.Closed, request.Reason, userId, now, ct);
            await WriteAuditAsync(response, "FormResponseClosed", from, FormResponseStatus.Closed, request.Reason, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

    public async Task<IReadOnlyList<FormResponseSubmissionDto>> ListSubmissionsAsync(Guid responseId, CancellationToken cancellationToken = default)
    {
        access.EnsureViewResponsesPermission();
        var response = await LoadResponseOrNotFoundAsync(responseId, track: false, cancellationToken);
        await access.EnsureFacilityInScopeAsync(response.FacilityId, cancellationToken);
        return await db.FormResponseSubmissions.AsNoTracking()
            .Where(s => s.ResponseId == responseId)
            .OrderBy(s => s.SubmissionNumber)
            .Select(s => new FormResponseSubmissionDto(
                s.Id, s.SubmissionNumber, s.CanonicalAnswersJson, s.AnswersHash, s.SubmittedByUserId,
                s.SubmittedAtUtc, s.WasLateAtSubmission, s.EffectiveDueAtSubmissionUtc, s.Acknowledged))
            .ToListAsync(cancellationToken);
    }

    public async Task<FormResponseSubmissionDto> GetSubmissionAsync(Guid responseId, int submissionNumber, CancellationToken cancellationToken = default)
    {
        var list = await ListSubmissionsAsync(responseId, cancellationToken);
        return list.FirstOrDefault(s => s.SubmissionNumber == submissionNumber)
            ?? throw new KeyNotFoundException("نسخة الإرسال غير موجودة.");
    }

    public async Task<IReadOnlyList<FormResponseHistoryDto>> GetHistoryAsync(Guid responseId, CancellationToken cancellationToken = default)
    {
        access.EnsureViewResponsesPermission();
        var response = await LoadResponseOrNotFoundAsync(responseId, track: false, cancellationToken);
        await access.EnsureFacilityInScopeAsync(response.FacilityId, cancellationToken);
        return await db.FormResponseHistories.AsNoTracking()
            .Where(h => h.ResponseId == responseId)
            .OrderBy(h => h.OccurredAtUtc)
            .Select(h => new FormResponseHistoryDto(
                h.Id, h.EventType, h.FromStatus, h.ToStatus, h.SubmissionNumber, h.ReviewLevel, h.Reason, h.ActorUserId, h.OccurredAtUtc))
            .ToListAsync(cancellationToken);
    }

    private async Task<FormResponse> LoadResponseOrNotFoundAsync(Guid responseId, bool track, CancellationToken ct)
    {
        var query = track ? db.FormResponses : db.FormResponses.AsNoTracking();
        return await query.FirstOrDefaultAsync(r => r.Id == responseId, ct)
            ?? throw new KeyNotFoundException("الرد غير موجود.");
    }

    private void EnsureNotSelf(FormResponse response)
    {
        if (response.SubmittedByUserId == access.UserId)
        {
            throw new UnauthorizedAccessException("لا يمكن مراجعة أو اعتماد ردك الخاص.");
        }
    }

    private static void EnsureSeparationOfDuties(FormResponse response, FormCampaignResponsePolicy policy, Guid userId)
    {
        if (!policy.RequireSeparationOfDuties) return;
        if (response.SubmittedByUserId == userId)
        {
            throw new UnauthorizedAccessException("فصل المهام يمنع اعتماد ردك الخاص.");
        }
    }

    private async Task EnsureNoDuplicateApprovalAsync(FormResponse response, Guid userId, CancellationToken ct)
    {
        var level = Math.Max(1, response.CurrentReviewLevel);
        var submission = await db.FormResponseSubmissions.AsNoTracking()
            .Where(s => s.ResponseId == response.Id)
            .OrderByDescending(s => s.SubmissionNumber)
            .FirstAsync(ct);
        var exists = await db.FormResponseReviewDecisions.AnyAsync(d =>
            d.ResponseId == response.Id
            && d.SubmissionId == submission.Id
            && d.ReviewLevel == level
            && d.ReviewedByUserId == userId
            && d.Decision == FormResponseReviewDecisionType.Approve, ct);
        if (exists)
        {
            throw new InvalidOperationException("لا يمكن اعتماد المستوى نفسه مرتين من المستخدم نفسه.");
        }
    }

    private async Task EnsureNoConsecutiveLevelsAsync(
        FormResponse response,
        FormCampaignResponsePolicy policy,
        Guid userId,
        CancellationToken ct)
    {
        if (!policy.RequireSeparationOfDuties || policy.ReviewMode != FormReviewMode.MultiLevel) return;
        var level = Math.Max(1, response.CurrentReviewLevel);
        if (level <= 1) return;
        var submission = await CurrentSubmissionAsync(response, ct);
        var prior = await db.FormResponseReviewDecisions.AsNoTracking().AnyAsync(d =>
            d.ResponseId == response.Id
            && d.SubmissionId == submission.Id
            && d.ReviewLevel == level - 1
            && d.ReviewedByUserId == userId
            && d.Decision == FormResponseReviewDecisionType.Approve, ct);
        if (prior)
        {
            throw new UnauthorizedAccessException("لا يمكن اعتماد مستويين متتاليين من المستخدم نفسه.");
        }
    }

    private async Task<FormResponseSubmission> CurrentSubmissionAsync(FormResponse response, CancellationToken ct) =>
        await db.FormResponseSubmissions
            .Where(s => s.ResponseId == response.Id)
            .OrderByDescending(s => s.SubmissionNumber)
            .FirstAsync(ct);

    private static FormResponseReviewDecision CreateDecision(
        FormResponse response,
        FormResponseSubmission submission,
        FormResponseReviewDecisionType type,
        string? reason,
        DateTimeOffset? newDue,
        FormResponseStatus from,
        FormResponseStatus to,
        Guid userId,
        DateTimeOffset now,
        int? level = null) =>
        new()
        {
            ResponseId = response.Id,
            SubmissionId = submission.Id,
            ReviewLevel = level ?? Math.Max(1, response.CurrentReviewLevel),
            Decision = type,
            Reason = reason,
            NewDueAtUtc = newDue,
            ReviewedByUserId = userId,
            ReviewedAtUtc = now,
            FromStatus = from,
            ToStatus = to
        };

    private async Task AddCommentsAsync(
        FormResponse response,
        FormResponseSubmission submission,
        Guid decisionId,
        IReadOnlyList<FormResponseReviewCommentRequest>? comments,
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (comments is null || comments.Count == 0) return;
        var schema = FormResponseSchemaLoader.Parse(
            (await db.FormSchemaSnapshots.AsNoTracking().FirstAsync(s => s.Id == response.FormSchemaSnapshotId, ct)).CanonicalSchemaJson);
        var fieldKeys = schema.Pages.SelectMany(p => p.Sections).SelectMany(s => s.Fields).Select(f => f.Key)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var comment in comments)
        {
            if (comment.FieldKey is not null && !fieldKeys.Contains(comment.FieldKey))
            {
                throw new ArgumentException("مفتاح الحقل في التعليق غير موجود في المخطط.");
            }

            db.Add(new FormResponseReviewComment
            {
                ResponseId = response.Id,
                SubmissionId = submission.Id,
                ReviewDecisionId = decisionId,
                FieldKey = comment.FieldKey,
                Body = comment.Body.Trim(),
                IsVisibleToRespondent = comment.IsVisibleToRespondent,
                CreatedByUserId = userId,
                CreatedAtUtc = now
            });
        }
    }

    private void ValidateNewDue(DateTimeOffset? newDue)
    {
        if (newDue is null) return;
        var now = clock.GetUtcNow();
        if (newDue <= now)
        {
            throw new ArgumentException("الموعد الجديد يجب أن يكون في المستقبل.");
        }

        if (newDue > now.AddDays(180))
        {
            throw new ArgumentException("الموعد الجديد خارج الحدود المعقولة.");
        }
    }

    private static IReadOnlyList<string> ResolveReviewActions(FormResponse response, FormCampaignResponsePolicy policy)
    {
        var actions = new List<string> { "View" };
        if (response.Status is FormResponseStatus.Submitted or FormResponseStatus.UnderReview)
        {
            actions.Add("StartReview");
            actions.Add("Return");
            actions.Add("Approve");
            actions.Add("Reject");
        }

        if (FormResponseStateMachine.CanClose(response.Status, policy.ReviewMode))
        {
            actions.Add("Close");
        }

        return actions;
    }

    private async Task WriteHistoryAsync(
        FormResponse response,
        string eventType,
        FormResponseStatus from,
        FormResponseStatus to,
        string? reason,
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        db.Add(new FormResponseHistory
        {
            ResponseId = response.Id,
            EventType = eventType,
            FromStatus = from,
            ToStatus = to,
            SubmissionNumber = response.CurrentSubmissionNumber,
            ReviewLevel = response.CurrentReviewLevel,
            Reason = reason,
            ActorUserId = userId,
            OccurredAtUtc = now
        });
        await Task.CompletedTask;
    }

    private Task WriteAuditAsync(
        FormResponse response,
        string action,
        FormResponseStatus from,
        FormResponseStatus to,
        string? reason,
        CancellationToken ct) =>
        audit.WriteAsync(new AuditEntry
        {
            Action = action,
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormResponse),
            EntityId = response.Id.ToString(),
            NewValues = new
            {
                response.AssignmentId,
                response.CampaignId,
                response.CycleId,
                response.FacilityId,
                response.CurrentSubmissionNumber,
                response.CurrentReviewLevel,
                FromStatus = from,
                ToStatus = to,
                Reason = reason
            }
        }, ct);
}
