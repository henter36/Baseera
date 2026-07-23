namespace Baseera.Application.Forms.Responses;

using Baseera.Application.Abstractions;
using Baseera.Application.Audit;
using Baseera.Application.Forms;
using Baseera.Domain.Audit;
using Baseera.Domain.Forms;
using Microsoft.EntityFrameworkCore;

public interface IFormResponseReviewService
{
    Task<FormResponseWorkspacePageDto> ListInboxAsync(
        FormResponseReviewInboxQuery query,
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
        FormResponseReviewInboxQuery query,
        CancellationToken cancellationToken = default)
    {
        access.EnsureReviewPermission();
        FormResponseListQueries.EnsureKnownReviewStatus(query.Status);
        query = FormResponseListQueries.Normalize(query);
        var now = clock.GetUtcNow();

        var baseQuery = BuildReviewInboxQuery();
        baseQuery = FormResponseListQueries.ApplyInboxFilters(baseQuery, query, now);
        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery
            .OrderBy(x => x.Response.SubmittedAtUtc)
            .ThenBy(x => x.Response.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = ProjectReviewInboxItems(rows, now);
        return new FormResponseWorkspacePageDto(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<FormResponseReviewDetailDto> GetReviewAsync(Guid responseId, CancellationToken cancellationToken = default)
    {
        access.EnsureReviewDetailPermission();
        var response = await LoadResponseOrNotFoundAsync(responseId, track: false, cancellationToken);
        await access.EnsureFacilityInScopeAsync(response.FacilityId, cancellationToken);
        var campaign = await db.FormCampaigns.AsNoTracking()
            .FirstAsync(c => c.Id == response.CampaignId, cancellationToken);
        await access.EnsureFormCapabilityAsync(campaign.FormDefinitionId, FormAccessCapability.ViewResponses, cancellationToken);
        var workspace = await responseService.GetReviewerAssignmentDetailAsync(response.AssignmentId, cancellationToken);
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
            db.Add(CreateDecision(new ReviewDecisionWrite(
                response, submission, FormResponseReviewDecisionType.StartReview, null, null, from,
                FormResponseStatus.UnderReview, userId, now)));
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
            var decision = CreateDecision(new ReviewDecisionWrite(
                response, submission, FormResponseReviewDecisionType.Return, request.Reason, request.NewDueAtUtc,
                from, FormResponseStatus.Returned, userId, now));
            db.Add(decision);
            await AddCommentsAsync(new ReviewCommentWrite(response, submission, decision.Id, request.Comments, userId, now), ct);
            response.Status = FormResponseStatus.Returned;
            response.ReturnedAtUtc = now;
            response.DueAtUtcOverride = request.NewDueAtUtc;
            response.UpdatedAtUtc = now;
            WriteHistory(new ResponseHistoryWrite(response, "FormResponseReturned", from, FormResponseStatus.Returned, request.Reason, userId, now));
            await WriteAuditAsync(response, "FormResponseReturned", from, FormResponseStatus.Returned, request.Reason, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

    public Task ApproveAsync(Guid responseId, FormResponseApproveRequest request, CancellationToken cancellationToken = default) =>
        db.ExecuteInTransactionAsync(async ct =>
        {
            access.EnsureApprovePermission();
            var reviewContext = await LoadReviewContextAsync(responseId, request, ct);
            var decisionContext = await ValidateReviewDecisionAsync(reviewContext, ct);
            ApplyApprovalDecision(decisionContext);
            await PersistReviewDecisionAsync(decisionContext, ct);
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
            var decision = CreateDecision(new ReviewDecisionWrite(
                response, submission, FormResponseReviewDecisionType.Reject, request.Reason, null,
                from, FormResponseStatus.Rejected, userId, now));
            db.Add(decision);
            await AddCommentsAsync(new ReviewCommentWrite(response, submission, decision.Id, request.Comments, userId, now), ct);
            response.Status = FormResponseStatus.Rejected;
            response.RejectedAtUtc = now;
            response.UpdatedAtUtc = now;
            WriteHistory(new ResponseHistoryWrite(response, "FormResponseRejected", from, FormResponseStatus.Rejected, request.Reason, userId, now));
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
            db.Add(CreateDecision(new ReviewDecisionWrite(
                response, submission, FormResponseReviewDecisionType.Close, request.Reason, null,
                from, FormResponseStatus.Closed, userId, now)));
            response.Status = FormResponseStatus.Closed;
            response.ClosedAtUtc = now;
            response.UpdatedAtUtc = now;
            WriteHistory(new ResponseHistoryWrite(response, "FormResponseClosed", from, FormResponseStatus.Closed, request.Reason, userId, now));
            await WriteAuditAsync(response, "FormResponseClosed", from, FormResponseStatus.Closed, request.Reason, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

    public async Task<IReadOnlyList<FormResponseSubmissionDto>> ListSubmissionsAsync(Guid responseId, CancellationToken cancellationToken = default)
    {
        access.EnsureReviewDetailPermission();
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
        access.EnsureReviewDetailPermission();
        var response = await LoadResponseOrNotFoundAsync(responseId, track: false, cancellationToken);
        await access.EnsureFacilityInScopeAsync(response.FacilityId, cancellationToken);
        return await db.FormResponseHistories.AsNoTracking()
            .Where(h => h.ResponseId == responseId)
            .OrderBy(h => h.OccurredAtUtc)
            .Select(h => new FormResponseHistoryDto(
                h.Id, h.EventType, h.FromStatus, h.ToStatus, h.SubmissionNumber, h.ReviewLevel, h.Reason, h.ActorUserId, h.OccurredAtUtc))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<FormResponseListQueries.InboxRow> BuildReviewInboxQuery()
    {
        var scopedFacilityIds = access.FilterFacilities(db.Facilities.AsNoTracking()).Select(f => f.Id);
        return from r in db.FormResponses.AsNoTracking()
            join a in db.FormFacilityAssignments.AsNoTracking() on r.AssignmentId equals a.Id
            join c in db.FormCycles.AsNoTracking() on r.CycleId equals c.Id
            join camp in db.FormCampaigns.AsNoTracking() on r.CampaignId equals camp.Id
            join pol in db.FormCampaignResponsePolicies.AsNoTracking() on camp.Id equals pol.CampaignId
            where (r.Status == FormResponseStatus.Submitted || r.Status == FormResponseStatus.UnderReview)
                && scopedFacilityIds.Contains(r.FacilityId)
            select new FormResponseListQueries.InboxRow
            {
                Response = r,
                Assignment = a,
                Cycle = c,
                Campaign = camp,
                Policy = pol
            };
    }

    private IReadOnlyList<FormResponseWorkspaceItemDto> ProjectReviewInboxItems(
        IReadOnlyList<FormResponseListQueries.InboxRow> rows,
        DateTimeOffset now) =>
        rows.Select(row =>
        {
            var due = FormResponseWorkStatusResolver.ResolveEffectiveDueAt(row.Cycle.DueAtUtc, row.Response.DueAtUtcOverride);
            var isOverdue = FormResponseWorkStatusResolver.IsOverdue(row.Response.Status, row.Policy.CompletionBasis, due, now, completion);
            var work = FormResponseWorkStatusResolver.Resolve(row.Response.Status, isOverdue);
            return new FormResponseWorkspaceItemDto(
                row.Assignment.Id, row.Campaign.Id, row.Campaign.Code, row.Campaign.NameAr, row.Cycle.Id, row.Cycle.OccurrenceKey,
                row.Assignment.FacilityId, row.Assignment.FacilityNameArAtAssignment, row.Assignment.RegionIdAtAssignment, row.Assignment.RegionNameArAtAssignment,
                row.Cycle.OpenAtUtc, row.Cycle.DueAtUtc, row.Cycle.GraceEndsAtUtc, row.Cycle.CloseAtUtc, due,
                row.Response.Id, row.Response.Status, work, isOverdue,
                completion.IsCompleted(row.Policy.CompletionBasis, row.Response.Status),
                row.Response.DraftVersion, row.Response.LastSavedAtUtc, row.Response.SubmittedAtUtc,
                row.Response.CurrentReviewLevel, row.Policy.RequiredApprovalLevels,
                ResolveReviewActions(row.Response, row.Policy),
                Convert.ToBase64String(row.Response.RowVersion));
        }).ToList();

    private async Task<ResponseReviewContext> LoadReviewContextAsync(
        Guid responseId,
        FormResponseApproveRequest request,
        CancellationToken ct)
    {
        var response = await LoadResponseOrNotFoundAsync(responseId, track: true, ct);
        await access.EnsureFacilityInScopeAsync(response.FacilityId, ct);
        EnsureNotSelf(response);
        FormAccessHelper.EnsureResponseRowVersion(response, request.RowVersion);
        var policy = await db.FormCampaignResponsePolicies.FirstAsync(p => p.CampaignId == response.CampaignId, ct);
        var submission = await CurrentSubmissionAsync(response, ct);
        return new ResponseReviewContext(response, policy, request, submission, access.UserId, clock.GetUtcNow());
    }

    private async Task<ApprovalDecisionContext> ValidateReviewDecisionAsync(ResponseReviewContext context, CancellationToken ct)
    {
        EnsureSeparationOfDuties(context.Response, context.Policy, context.UserId);
        await EnsureNoDuplicateApprovalAsync(context.Response, ct);
        await EnsureNoConsecutiveLevelsAsync(context.Response, context.Policy, context.UserId, ct);

        var from = context.Response.Status;
        if (from == FormResponseStatus.Submitted)
        {
            FormResponseStateMachine.EnsureCanTransition(from, FormResponseStatus.UnderReview);
            context.Response.Status = FormResponseStatus.UnderReview;
            from = FormResponseStatus.UnderReview;
        }

        var level = Math.Max(1, context.Response.CurrentReviewLevel);
        var target = FormResponseStateMachine.ResolveApprovalTargetStatus(level, context.Policy.RequiredApprovalLevels);
        FormResponseStateMachine.EnsureCanTransition(from, target);
        return new ApprovalDecisionContext(context, from, target, level);
    }

    private void ApplyApprovalDecision(ApprovalDecisionContext context)
    {
        var decision = CreateDecision(new ReviewDecisionWrite(
            context.Review.Response,
            context.Review.Submission,
            FormResponseReviewDecisionType.Approve,
            context.Review.Request.Reason,
            null,
            context.FromStatus,
            context.TargetStatus,
            context.Review.UserId,
            context.Review.Now,
            context.ReviewLevel));
        db.Add(decision);
        context.DecisionId = decision.Id;
        context.Review.Response.Status = context.TargetStatus;
        if (context.TargetStatus == FormResponseStatus.Approved)
        {
            context.Review.Response.ApprovedAtUtc = context.Review.Now;
        }
        else
        {
            context.Review.Response.CurrentReviewLevel = context.ReviewLevel + 1;
        }

        context.Review.Response.UpdatedAtUtc = context.Review.Now;
    }

    private async Task PersistReviewDecisionAsync(ApprovalDecisionContext context, CancellationToken ct)
    {
        await AddCommentsAsync(new ReviewCommentWrite(
            context.Review.Response,
            context.Review.Submission,
            context.DecisionId,
            context.Review.Request.Comments,
            context.Review.UserId,
            context.Review.Now), ct);
        WriteHistory(new ResponseHistoryWrite(
            context.Review.Response,
            "FormResponseApproved",
            context.FromStatus,
            context.TargetStatus,
            context.Review.Request.Reason,
            context.Review.UserId,
            context.Review.Now));
        await WriteAuditAsync(
            context.Review.Response,
            "FormResponseApproved",
            context.FromStatus,
            context.TargetStatus,
            context.Review.Request.Reason,
            ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsConcurrentApprovalConflict(ex))
        {
            throw ApprovalLevelAlreadyDecided(context.Review.Response);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (await ApprovalExistsAsync(
                    context.Review.Response.Id,
                    context.Review.Submission.Id,
                    context.ReviewLevel,
                    ct))
            {
                throw ApprovalLevelAlreadyDecided(context.Review.Response);
            }

            throw new FormResponseConflictException(new FormResponseConflictDto(
                context.Review.Response.DraftVersion,
                Convert.ToBase64String(context.Review.Response.RowVersion),
                context.Review.Response.LastSavedAtUtc,
                "RowVersionConflict"));
        }
    }

    private static FormResponseConflictException ApprovalLevelAlreadyDecided(FormResponse response) =>
        new(new FormResponseConflictDto(
            response.DraftVersion,
            Convert.ToBase64String(response.RowVersion),
            response.LastSavedAtUtc,
            "APPROVAL_LEVEL_ALREADY_DECIDED"));

    private Task<bool> ApprovalExistsAsync(
        Guid responseId,
        Guid submissionId,
        int reviewLevel,
        CancellationToken ct) =>
        db.FormResponseReviewDecisions.AsNoTracking().AnyAsync(d =>
            d.ResponseId == responseId
            && d.SubmissionId == submissionId
            && d.ReviewLevel == reviewLevel
            && d.Decision == FormResponseReviewDecisionType.Approve, ct);

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

    private async Task EnsureNoDuplicateApprovalAsync(FormResponse response, CancellationToken ct)
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
            && d.Decision == FormResponseReviewDecisionType.Approve, ct);
        if (exists)
        {
            throw new FormResponseConflictException(new FormResponseConflictDto(
                response.DraftVersion,
                Convert.ToBase64String(response.RowVersion),
                response.LastSavedAtUtc,
                "APPROVAL_LEVEL_ALREADY_DECIDED"));
        }
    }

    private const string ApproveLevelUniqueIndex = "IX_FormResponseReviewDecisions_ApproveLevel";

    private static bool IsConcurrentApprovalConflict(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(ApproveLevelUniqueIndex, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var number = current.GetType().GetProperty("Number")?.GetValue(current);
            if (number is not (2601 or 2627))
            {
                continue;
            }

            for (Exception? scan = exception; scan is not null; scan = scan.InnerException)
            {
                if (scan.Message.Contains(ApproveLevelUniqueIndex, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
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

    private static FormResponseReviewDecision CreateDecision(ReviewDecisionWrite write) =>
        new()
        {
            ResponseId = write.Response.Id,
            SubmissionId = write.Submission.Id,
            ReviewLevel = write.Level ?? Math.Max(1, write.Response.CurrentReviewLevel),
            Decision = write.Type,
            Reason = write.Reason,
            NewDueAtUtc = write.NewDue,
            ReviewedByUserId = write.UserId,
            ReviewedAtUtc = write.Now,
            FromStatus = write.From,
            ToStatus = write.To
        };

    private async Task AddCommentsAsync(ReviewCommentWrite write, CancellationToken ct)
    {
        if (write.Comments is null || write.Comments.Count == 0) return;
        var schema = FormResponseSchemaLoader.Parse(
            (await db.FormSchemaSnapshots.AsNoTracking().FirstAsync(s => s.Id == write.Response.FormSchemaSnapshotId, ct)).CanonicalSchemaJson);
        var fieldKeys = schema.Pages.SelectMany(p => p.Sections).SelectMany(s => s.Fields).Select(f => f.Key)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var comment in write.Comments)
        {
            if (comment.FieldKey is not null && !fieldKeys.Contains(comment.FieldKey))
            {
                throw new ArgumentException("مفتاح الحقل في التعليق غير موجود في المخطط.");
            }

            db.Add(new FormResponseReviewComment
            {
                ResponseId = write.Response.Id,
                SubmissionId = write.Submission.Id,
                ReviewDecisionId = write.DecisionId,
                FieldKey = comment.FieldKey,
                Body = comment.Body.Trim(),
                IsVisibleToRespondent = comment.IsVisibleToRespondent,
                CreatedByUserId = write.UserId,
                CreatedAtUtc = write.Now
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

    private void WriteHistory(ResponseHistoryWrite write)
    {
        db.Add(new FormResponseHistory
        {
            ResponseId = write.Response.Id,
            EventType = write.EventType,
            FromStatus = write.From,
            ToStatus = write.To,
            SubmissionNumber = write.Response.CurrentSubmissionNumber,
            ReviewLevel = write.Response.CurrentReviewLevel,
            Reason = write.Reason,
            ActorUserId = write.UserId,
            OccurredAtUtc = write.Now
        });
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

    private sealed record ResponseReviewContext(
        FormResponse Response,
        FormCampaignResponsePolicy Policy,
        FormResponseApproveRequest Request,
        FormResponseSubmission Submission,
        Guid UserId,
        DateTimeOffset Now);

    private sealed class ApprovalDecisionContext(
        ResponseReviewContext review,
        FormResponseStatus fromStatus,
        FormResponseStatus targetStatus,
        int reviewLevel)
    {
        public ResponseReviewContext Review { get; } = review;
        public FormResponseStatus FromStatus { get; } = fromStatus;
        public FormResponseStatus TargetStatus { get; } = targetStatus;
        public int ReviewLevel { get; } = reviewLevel;
        public Guid DecisionId { get; set; }
    }

    private sealed record ReviewDecisionWrite(
        FormResponse Response,
        FormResponseSubmission Submission,
        FormResponseReviewDecisionType Type,
        string? Reason,
        DateTimeOffset? NewDue,
        FormResponseStatus From,
        FormResponseStatus To,
        Guid UserId,
        DateTimeOffset Now,
        int? Level = null);

    private sealed record ReviewCommentWrite(
        FormResponse Response,
        FormResponseSubmission Submission,
        Guid DecisionId,
        IReadOnlyList<FormResponseReviewCommentRequest>? Comments,
        Guid UserId,
        DateTimeOffset Now);

    private sealed record ResponseHistoryWrite(
        FormResponse Response,
        string EventType,
        FormResponseStatus From,
        FormResponseStatus To,
        string? Reason,
        Guid UserId,
        DateTimeOffset Now);
}
