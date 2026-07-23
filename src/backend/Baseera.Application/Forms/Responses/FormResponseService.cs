namespace Baseera.Application.Forms.Responses;

using System.Text.Json;
using Baseera.Application.Abstractions;
using Baseera.Application.Audit;
using Baseera.Domain.Attachments;
using Baseera.Domain.Audit;
using Baseera.Domain.Forms;
using Baseera.Domain.Forms.Schema;
using Microsoft.EntityFrameworkCore;

public interface IFormResponseService
{
    Task<FormResponseWorkspacePageDto> ListWorkspaceAsync(
        string? workStatus,
        Guid? campaignId,
        Guid? cycleId,
        Guid? facilityId,
        Guid? regionId,
        DateTimeOffset? dueFrom,
        DateTimeOffset? dueTo,
        string? search,
        int page,
        int pageSize,
        string? sort,
        CancellationToken cancellationToken = default);

    Task<FormResponseWorkspaceDetailDto> GetAssignmentResponseAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    Task<FormResponseDraftSaveResultDto> SaveDraftAsync(
        Guid assignmentId,
        FormResponseDraftSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<FormResponseValidationResultDto> ValidateAsync(
        Guid assignmentId,
        FormResponseValidateRequest request,
        CancellationToken cancellationToken = default);

    Task<FormResponseSubmitResultDto> SubmitAsync(
        Guid assignmentId,
        FormResponseSubmitRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class FormResponseService(
    IBaseeraDbContext db,
    IFormResponseAccessCoordinator access,
    IFormResponseValidator validator,
    IFormResponseCompletionEvaluator completion,
    IFormResponseProjectionService projection,
    IAuditService audit,
    TimeProvider clock) : IFormResponseService
{
    public async Task<FormResponseWorkspacePageDto> ListWorkspaceAsync(
        string? workStatus,
        Guid? campaignId,
        Guid? cycleId,
        Guid? facilityId,
        Guid? regionId,
        DateTimeOffset? dueFrom,
        DateTimeOffset? dueTo,
        string? search,
        int page,
        int pageSize,
        string? sort,
        CancellationToken cancellationToken = default)
    {
        access.EnsureViewResponsesPermission();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var now = clock.GetUtcNow();

        var query =
            from a in db.FormFacilityAssignments.AsNoTracking()
            join c in db.FormCycles.AsNoTracking() on a.CycleId equals c.Id
            join camp in db.FormCampaigns.AsNoTracking() on a.CampaignId equals camp.Id
            join pol in db.FormCampaignResponsePolicies.AsNoTracking() on camp.Id equals pol.CampaignId
            join r in db.FormResponses.AsNoTracking() on a.Id equals r.AssignmentId into rj
            from response in rj.DefaultIfEmpty()
            where a.IsAvailable
            select new { a, c, camp, pol, response };

        if (campaignId.HasValue) query = query.Where(x => x.a.CampaignId == campaignId);
        if (cycleId.HasValue) query = query.Where(x => x.a.CycleId == cycleId);
        if (facilityId.HasValue) query = query.Where(x => x.a.FacilityId == facilityId);
        if (regionId.HasValue) query = query.Where(x => x.a.RegionIdAtAssignment == regionId);
        if (dueFrom.HasValue) query = query.Where(x => x.c.DueAtUtc >= dueFrom);
        if (dueTo.HasValue) query = query.Where(x => x.c.DueAtUtc <= dueTo);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.camp.NameAr.Contains(term)
                || x.camp.Code.Contains(term)
                || x.a.FacilityNameArAtAssignment.Contains(term));
        }

        var rows = await query
            .OrderBy(x => x.c.DueAtUtc)
            .ThenBy(x => x.a.FacilityNameArAtAssignment)
            .ThenBy(x => x.a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize * 5)
            .ToListAsync(cancellationToken);

        var items = new List<FormResponseWorkspaceItemDto>();
        foreach (var row in rows)
        {
            try
            {
                await access.EnsureFacilityInScopeAsync(row.a.FacilityId, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                continue;
            }

            var status = row.response?.Status;
            var due = FormResponseWorkStatusResolver.ResolveEffectiveDueAt(row.c.DueAtUtc, row.response?.DueAtUtcOverride);
            var overdue = FormResponseWorkStatusResolver.IsOverdue(status, row.pol.CompletionBasis, due, now, completion);
            var work = FormResponseWorkStatusResolver.Resolve(status, overdue);
            if (!MatchesWorkStatusFilter(workStatus, work, overdue, completion.IsCompleted(row.pol.CompletionBasis, status), now, row.c))
            {
                continue;
            }

            items.Add(MapWorkspaceItem(row.a, row.c, row.camp, row.pol, row.response, work, overdue, due));
            if (items.Count >= pageSize) break;
        }

        return new FormResponseWorkspacePageDto(items, page, pageSize, items.Count);
    }

    public async Task<FormResponseWorkspaceDetailDto> GetAssignmentResponseAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        access.EnsureViewResponsesPermission();
        var ctx = await LoadContextAsync(assignmentId, track: false, cancellationToken);
        await access.EnsureFacilityInScopeAsync(ctx.Assignment.FacilityId, cancellationToken);
        return await MapDetailAsync(ctx, cancellationToken);
    }

    public async Task<FormResponseDraftSaveResultDto> SaveDraftAsync(
        Guid assignmentId,
        FormResponseDraftSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        access.EnsureRespondPermission();
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            var ctx = await LoadContextAsync(assignmentId, track: true, ct);
            await access.EnsureFacilityInScopeAsync(ctx.Assignment.FacilityId, ct);
            EnsureAssignmentEditable(ctx);

            var response = ctx.Response ?? await CreateDraftResponseAsync(ctx, ct);
            if (request.ClientMutationId != Guid.Empty)
            {
                var existing = await db.FormResponseMutations
                    .FirstOrDefaultAsync(m => m.ResponseId == response.Id && m.ClientMutationId == request.ClientMutationId, ct);
                if (existing is not null)
                {
                    return JsonSerializer.Deserialize<FormResponseDraftSaveResultDto>(existing.ResultPayloadJson)
                        ?? throw new InvalidOperationException("تعذر استعادة نتيجة الحفظ السابق.");
                }
            }

            EnsureDraftEditable(response);
            EnsureDraftVersion(response, request.ExpectedDraftVersion);
            EnsureRowVersion(response, request.RowVersion);

            var schema = FormResponseSchemaLoader.Parse(ctx.Snapshot.CanonicalSchemaJson);
            FormResponseSchemaLoader.EnsureSchemaHashMatches(response, ctx.Cycle);
            var validation = validator.Validate(schema, request.Answers, FormResponseValidationMode.DraftPartial);
            if (validation.Issues.Any(i => i.Severity == "Error" && IsBlockingDraftIssue(i.Code)))
            {
                throw new FormResponseValidationException(validation.Issues.Where(i => i.Severity == "Error").ToList());
            }

            var now = clock.GetUtcNow();
            var userId = access.UserId;
            if (response.FirstStartedAtUtc is null)
            {
                response.FirstStartedAtUtc = now;
                await WriteHistoryAsync(response, "FormResponseStarted", null, FormResponseStatus.Draft, null, null, null, userId, now, ct);
                await audit.WriteAsync(new AuditEntry
                {
                    Action = "FormResponseStarted",
                    Module = FormAccessHelper.ModuleName,
                    EntityType = nameof(FormResponse),
                    EntityId = response.Id.ToString(),
                    NewValues = new { response.AssignmentId, response.CampaignId, response.CycleId, response.FacilityId }
                }, ct);
            }

            response.DraftAnswersJson = validation.CanonicalAnswersJson;
            response.DraftAnswersHash = validation.AnswersHash;
            response.DraftVersion += 1;
            response.LastSavedAtUtc = now;
            response.LastSavedByUserId = userId;
            response.UpdatedAtUtc = now;
            if (response.Status == FormResponseStatus.Returned)
            {
                FormResponseStateMachine.EnsureCanTransition(response.Status, FormResponseStatus.Draft);
                response.Status = FormResponseStatus.Draft;
            }

            var result = new FormResponseDraftSaveResultDto(
                response.Id,
                response.DraftVersion,
                Convert.ToBase64String(response.RowVersion),
                now,
                validation.Issues,
                validation.CalculatedValues,
                validation.VisibleFieldKeys.ToList(),
                validation.RequiredFieldKeys.ToList());

            if (request.ClientMutationId != Guid.Empty)
            {
                db.Add(new FormResponseMutation
                {
                    ResponseId = response.Id,
                    ClientMutationId = request.ClientMutationId,
                    AppliedDraftVersion = response.DraftVersion,
                    AppliedAtUtc = now,
                    ResultPayloadJson = JsonSerializer.Serialize(result)
                });
            }

            await db.SaveChangesAsync(ct);
            result = result with { RowVersion = Convert.ToBase64String(response.RowVersion) };
            return result;
        }, cancellationToken);
    }

    public async Task<FormResponseValidationResultDto> ValidateAsync(
        Guid assignmentId,
        FormResponseValidateRequest request,
        CancellationToken cancellationToken = default)
    {
        access.EnsureRespondPermission();
        var ctx = await LoadContextAsync(assignmentId, track: false, cancellationToken);
        await access.EnsureFacilityInScopeAsync(ctx.Assignment.FacilityId, cancellationToken);
        var schema = FormResponseSchemaLoader.Parse(ctx.Snapshot.CanonicalSchemaJson);
        var validation = validator.Validate(schema, request.Answers, FormResponseValidationMode.FullSubmit);
        return ToValidationDto(validation);
    }

    public async Task<FormResponseSubmitResultDto> SubmitAsync(
        Guid assignmentId,
        FormResponseSubmitRequest request,
        CancellationToken cancellationToken = default)
    {
        access.EnsureRespondPermission();
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            var ctx = await LoadContextAsync(assignmentId, track: true, ct);
            await access.EnsureFacilityInScopeAsync(ctx.Assignment.FacilityId, ct);
            EnsureAssignmentEditable(ctx);
            var response = ctx.Response ?? await CreateDraftResponseAsync(ctx, ct);
            EnsureDraftEditable(response);
            EnsureDraftVersion(response, request.ExpectedDraftVersion);
            EnsureRowVersion(response, request.RowVersion);

            if (ctx.Policy.RequireSubmissionAcknowledgement && !request.Acknowledged)
            {
                throw new ArgumentException("الإقرار مطلوب قبل الإرسال.");
            }

            var now = clock.GetUtcNow();
            var due = FormResponseWorkStatusResolver.ResolveEffectiveDueAt(ctx.Cycle.DueAtUtc, response.DueAtUtcOverride);
            var late = now > due;
            if (late && !ctx.Policy.AllowLateSubmission)
            {
                throw new InvalidOperationException("انتهى الموعد والإرسال المتأخر غير مسموح.");
            }

            if (response.Status == FormResponseStatus.Returned && !ctx.Policy.AllowResubmissionAfterReturn)
            {
                throw new InvalidOperationException("إعادة الإرسال بعد الإعادة غير مسموحة.");
            }

            var schema = FormResponseSchemaLoader.Parse(ctx.Snapshot.CanonicalSchemaJson);
            FormResponseSchemaLoader.EnsureSchemaHashMatches(response, ctx.Cycle);
            var attachments = await LoadReferencedAttachmentsAsync(request.Answers, response.Id, ct);
            var validation = validator.Validate(schema, request.Answers, FormResponseValidationMode.FullSubmit, attachments);
            if (!validation.IsValid)
            {
                throw new FormResponseValidationException(validation.Issues.Where(i => i.Severity == "Error").ToList());
            }

            var from = response.Status;
            if (from == FormResponseStatus.Returned)
            {
                FormResponseStateMachine.EnsureCanTransition(FormResponseStatus.Returned, FormResponseStatus.Draft);
                response.Status = FormResponseStatus.Draft;
                from = FormResponseStatus.Draft;
            }

            FormResponseStateMachine.EnsureCanTransition(from, FormResponseStatus.Submitted);
            var target = FormResponseStateMachine.ResolveSubmissionTargetStatus(ctx.Policy.ReviewMode);
            if (target == FormResponseStatus.UnderReview)
            {
                FormResponseStateMachine.EnsureCanTransition(FormResponseStatus.Submitted, FormResponseStatus.UnderReview);
            }
            var userId = access.UserId;
            var submissionNumber = response.CurrentSubmissionNumber + 1;
            var submission = new FormResponseSubmission
            {
                ResponseId = response.Id,
                SubmissionNumber = submissionNumber,
                FormSchemaSnapshotId = response.FormSchemaSnapshotId,
                SchemaHash = response.SchemaHash,
                CanonicalAnswersJson = validation.CanonicalAnswersJson,
                AnswersHash = validation.AnswersHash,
                SubmittedByUserId = userId,
                SubmittedAtUtc = now,
                WasLateAtSubmission = late,
                EffectiveDueAtSubmissionUtc = due,
                Acknowledged = request.Acknowledged,
                AcknowledgementText = request.AcknowledgementText,
                AcknowledgedAtUtc = request.Acknowledged ? now : null
            };
            db.Add(submission);

            response.DraftAnswersJson = validation.CanonicalAnswersJson;
            response.DraftAnswersHash = validation.AnswersHash;
            response.DraftVersion += 1;
            response.CurrentSubmissionNumber = submissionNumber;
            response.CurrentReviewLevel = ctx.Policy.ReviewMode == FormReviewMode.None ? 0 : 1;
            response.Status = target;
            response.SubmittedAtUtc = now;
            response.SubmittedByUserId = userId;
            response.LastSavedAtUtc = now;
            response.LastSavedByUserId = userId;
            response.UpdatedAtUtc = now;

            await WriteHistoryAsync(response, "FormResponseSubmitted", from, target, submissionNumber, response.CurrentReviewLevel, null, userId, now, ct);
            await audit.WriteAsync(new AuditEntry
            {
                Action = "FormResponseSubmitted",
                Module = FormAccessHelper.ModuleName,
                EntityType = nameof(FormResponse),
                EntityId = response.Id.ToString(),
                NewValues = new
                {
                    response.AssignmentId,
                    response.CampaignId,
                    response.CycleId,
                    response.FacilityId,
                    submissionNumber,
                    FromStatus = from,
                    ToStatus = target
                }
            }, ct);

            await db.SaveChangesAsync(ct);
            return new FormResponseSubmitResultDto(
                response.Id,
                submission.Id,
                submissionNumber,
                response.Status,
                Convert.ToBase64String(response.RowVersion),
                now);
        }, cancellationToken);
    }

    private static bool IsBlockingDraftIssue(string code) =>
        code is "UNKNOWN_FIELD" or "DUPLICATE_KEY" or "TYPE_MISMATCH" or "READONLY_WRITE"
            or "CALCULATED_WRITE" or "ATTACHMENT_UNAUTHORIZED" or "MALFORMED" or "PAYLOAD_TOO_LARGE"
            or "TOO_MANY_KEYS" or "TOO_MANY_ROWS" or "DUPLICATE_ROW";

    private static bool MatchesWorkStatusFilter(
        string? filter,
        FormAssignmentWorkStatus work,
        bool overdue,
        bool completed,
        DateTimeOffset now,
        FormCycle cycle)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter.Equals("current", StringComparison.OrdinalIgnoreCase))
        {
            return now >= cycle.OpenAtUtc && now <= cycle.CloseAtUtc && !completed
                && work is not FormAssignmentWorkStatus.Approved and not FormAssignmentWorkStatus.Rejected and not FormAssignmentWorkStatus.Closed;
        }

        return filter.ToLowerInvariant() switch
        {
            "upcoming" => now < cycle.OpenAtUtc,
            "overdue" => overdue,
            "returned" => work == FormAssignmentWorkStatus.Returned,
            "submitted" => work is FormAssignmentWorkStatus.Submitted or FormAssignmentWorkStatus.UnderReview,
            "completed" => completed,
            _ => true
        };
    }

    private FormResponseWorkspaceItemDto MapWorkspaceItem(
        FormFacilityAssignment a,
        FormCycle c,
        FormCampaign camp,
        FormCampaignResponsePolicy pol,
        FormResponse? response,
        FormAssignmentWorkStatus work,
        bool overdue,
        DateTimeOffset due) =>
        new(
            a.Id,
            camp.Id,
            camp.Code,
            camp.NameAr,
            c.Id,
            c.OccurrenceKey,
            a.FacilityId,
            a.FacilityNameArAtAssignment,
            a.RegionIdAtAssignment,
            a.RegionNameArAtAssignment,
            c.OpenAtUtc,
            c.DueAtUtc,
            c.GraceEndsAtUtc,
            c.CloseAtUtc,
            due,
            response?.Id,
            response?.Status,
            work,
            overdue,
            completion.IsCompleted(pol.CompletionBasis, response?.Status),
            response?.DraftVersion,
            response?.LastSavedAtUtc,
            response?.SubmittedAtUtc,
            response?.CurrentReviewLevel ?? 0,
            pol.RequiredApprovalLevels,
            ResolveRespondentActions(a, c, response, pol, work, clock.GetUtcNow()),
            response is null ? null : Convert.ToBase64String(response.RowVersion));

    private async Task<FormResponseWorkspaceDetailDto> MapDetailAsync(ResponseContext ctx, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var status = ctx.Response?.Status;
        var due = FormResponseWorkStatusResolver.ResolveEffectiveDueAt(ctx.Cycle.DueAtUtc, ctx.Response?.DueAtUtcOverride);
        var overdue = FormResponseWorkStatusResolver.IsOverdue(status, ctx.Policy.CompletionBasis, due, now, completion);
        var work = FormResponseWorkStatusResolver.Resolve(status, overdue);
        var schema = FormResponseSchemaLoader.Parse(ctx.Snapshot.CanonicalSchemaJson);
        var isOwner = true;
        var projected = projection.ProjectAnswers(
            schema,
            ctx.Form.Classification,
            ctx.Response?.DraftAnswersJson,
            access.CanViewSensitiveResponses(),
            isOwner);
        FormResponseSubmissionDto? latest = null;
        IReadOnlyList<FormResponseReviewCommentDto> comments = [];
        if (ctx.Response is not null)
        {
            var submission = await db.FormResponseSubmissions.AsNoTracking()
                .Where(s => s.ResponseId == ctx.Response.Id)
                .OrderByDescending(s => s.SubmissionNumber)
                .FirstOrDefaultAsync(ct);
            if (submission is not null)
            {
                var projectedSubmission = projection.ProjectAnswers(
                    schema,
                    ctx.Form.Classification,
                    submission.CanonicalAnswersJson,
                    access.CanViewSensitiveResponses(),
                    isOwner);
                latest = new FormResponseSubmissionDto(
                    submission.Id,
                    submission.SubmissionNumber,
                    projectedSubmission.AnswersJson ?? "{}",
                    submission.AnswersHash,
                    submission.SubmittedByUserId,
                    submission.SubmittedAtUtc,
                    submission.WasLateAtSubmission,
                    submission.EffectiveDueAtSubmissionUtc,
                    submission.Acknowledged);
            }

            comments = await db.FormResponseReviewComments.AsNoTracking()
                .Where(c => c.ResponseId == ctx.Response.Id && c.IsVisibleToRespondent)
                .OrderBy(c => c.CreatedAtUtc)
                .Select(c => new FormResponseReviewCommentDto(
                    c.Id, c.SubmissionId, c.ReviewDecisionId, c.FieldKey, c.Body, c.IsVisibleToRespondent,
                    c.CreatedByUserId, c.CreatedAtUtc))
                .ToListAsync(ct);
        }

        return new FormResponseWorkspaceDetailDto(
            ctx.Assignment.Id,
            ctx.Campaign.Id,
            ctx.Campaign.Code,
            ctx.Campaign.NameAr,
            ctx.Cycle.Id,
            ctx.Cycle.OccurrenceKey,
            ctx.Assignment.FacilityId,
            ctx.Assignment.FacilityNameArAtAssignment,
            ctx.Assignment.RegionIdAtAssignment,
            ctx.Assignment.RegionNameArAtAssignment,
            ctx.Cycle.OpenAtUtc,
            ctx.Cycle.DueAtUtc,
            ctx.Cycle.GraceEndsAtUtc,
            ctx.Cycle.CloseAtUtc,
            due,
            ctx.Cycle.Status,
            ctx.Assignment.IsAvailable,
            ctx.Assignment.UnavailableReason,
            ctx.Response?.Id,
            status,
            work,
            overdue,
            completion.IsCompleted(ctx.Policy.CompletionBasis, status),
            ctx.Response?.DraftVersion ?? 0,
            projected.AnswersJson,
            ctx.Snapshot.CanonicalSchemaJson,
            ctx.Cycle.SchemaHash,
            ctx.Form.Classification,
            FormResponsePolicyRules.ToDto(ctx.Policy),
            latest,
            comments,
            null,
            ResolveRespondentActions(ctx.Assignment, ctx.Cycle, ctx.Response, ctx.Policy, work, now),
            ctx.Response is null ? null : Convert.ToBase64String(ctx.Response.RowVersion),
            projected.Visibility,
            projected.Redacted);
    }

    private static IReadOnlyList<string> ResolveRespondentActions(
        FormFacilityAssignment assignment,
        FormCycle cycle,
        FormResponse? response,
        FormCampaignResponsePolicy policy,
        FormAssignmentWorkStatus work,
        DateTimeOffset now)
    {
        var actions = new List<string>();
        if (!assignment.IsAvailable) return actions;
        if (cycle.Status is FormCycleStatus.Closed or FormCycleStatus.Cancelled) return actions;
        if (now < cycle.OpenAtUtc) return actions;
        if (now > cycle.CloseAtUtc) return actions;

        if (response is null || FormResponseStateMachine.CanEditDraft(response.Status))
        {
            actions.Add("SaveDraft");
            actions.Add("Validate");
            if (response?.Status != FormResponseStatus.Returned || policy.AllowResubmissionAfterReturn)
            {
                actions.Add("Submit");
            }
        }

        actions.Add("ViewHistory");
        return actions;
    }

    private async Task<ResponseContext> LoadContextAsync(Guid assignmentId, bool track, CancellationToken ct)
    {
        var assignmentQuery = track ? db.FormFacilityAssignments : db.FormFacilityAssignments.AsNoTracking();
        var assignment = await assignmentQuery.FirstOrDefaultAsync(a => a.Id == assignmentId, ct)
            ?? throw new KeyNotFoundException("الاستحقاق غير موجود.");

        var cycle = await (track ? db.FormCycles : db.FormCycles.AsNoTracking())
            .FirstAsync(c => c.Id == assignment.CycleId, ct);
        var campaign = await (track ? db.FormCampaigns : db.FormCampaigns.AsNoTracking())
            .FirstAsync(c => c.Id == assignment.CampaignId, ct);
        var policy = await (track ? db.FormCampaignResponsePolicies : db.FormCampaignResponsePolicies.AsNoTracking())
            .FirstOrDefaultAsync(p => p.CampaignId == campaign.Id, ct)
            ?? FormResponsePolicyRules.CreateDefault(campaign.Id);
        var form = await db.FormDefinitions.AsNoTracking().FirstAsync(f => f.Id == campaign.FormDefinitionId, ct);
        var snapshot = await db.FormSchemaSnapshots.AsNoTracking()
            .FirstAsync(s => s.Id == cycle.FormSchemaSnapshotId, ct);
        var response = await (track ? db.FormResponses : db.FormResponses.AsNoTracking())
            .FirstOrDefaultAsync(r => r.AssignmentId == assignmentId, ct);

        return new ResponseContext(assignment, cycle, campaign, policy, form, snapshot, response);
    }

    private async Task<FormResponse> CreateDraftResponseAsync(ResponseContext ctx, CancellationToken ct)
    {
        EnsureAssignmentEditable(ctx);
        FormResponseStateMachine.EnsureCanTransition(null, FormResponseStatus.Draft);
        var now = clock.GetUtcNow();
        var response = new FormResponse
        {
            AssignmentId = ctx.Assignment.Id,
            CampaignId = ctx.Assignment.CampaignId,
            CycleId = ctx.Assignment.CycleId,
            FacilityId = ctx.Assignment.FacilityId,
            FormSchemaSnapshotId = ctx.Cycle.FormSchemaSnapshotId,
            SchemaHash = ctx.Cycle.SchemaHash,
            Status = FormResponseStatus.Draft,
            DraftAnswersJson = "{}",
            DraftAnswersHash = string.Empty,
            DraftVersion = 0,
            CreatedAtUtc = now
        };
        db.Add(response);
        await db.SaveChangesAsync(ct);
        ctx.Response = response;
        return response;
    }

    private void EnsureAssignmentEditable(ResponseContext ctx)
    {
        if (!ctx.Assignment.IsAvailable)
        {
            throw new InvalidOperationException("الاستحقاق غير متاح.");
        }

        if (ctx.Cycle.Status is FormCycleStatus.Closed or FormCycleStatus.Cancelled)
        {
            throw new InvalidOperationException("الدورة مغلقة أو ملغاة.");
        }

        if (ctx.Campaign.Status == FormCampaignStatus.Cancelled)
        {
            throw new InvalidOperationException("الحملة ملغاة.");
        }

        var now = clock.GetUtcNow();
        if (now < ctx.Cycle.OpenAtUtc)
        {
            throw new InvalidOperationException("لم يفتح موعد التعبئة بعد.");
        }

        if (now > ctx.Cycle.CloseAtUtc)
        {
            throw new InvalidOperationException("أُغلق موعد التعبئة.");
        }
    }

    private static void EnsureDraftEditable(FormResponse response)
    {
        if (!FormResponseStateMachine.CanEditDraft(response.Status))
        {
            throw new InvalidOperationException("لا يمكن تعديل المسودة في الحالة الحالية.");
        }
    }

    private void EnsureDraftVersion(FormResponse response, int expected)
    {
        if (response.DraftVersion != expected)
        {
            throw new FormResponseConflictException(new FormResponseConflictDto(
                response.DraftVersion,
                Convert.ToBase64String(response.RowVersion),
                response.LastSavedAtUtc,
                "DraftVersionConflict"));
        }
    }

    private void EnsureRowVersion(FormResponse response, string? incoming)
    {
        if (response.DraftVersion == 0 && string.IsNullOrWhiteSpace(incoming))
        {
            return;
        }

        try
        {
            FormAccessHelper.EnsureRowVersion(response.RowVersion, incoming ?? string.Empty);
        }
        catch (InvalidOperationException)
        {
            throw new FormResponseConflictException(new FormResponseConflictDto(
                response.DraftVersion,
                Convert.ToBase64String(response.RowVersion),
                response.LastSavedAtUtc,
                "RowVersionConflict"));
        }
    }

    private async Task WriteHistoryAsync(
        FormResponse response,
        string eventType,
        FormResponseStatus? from,
        FormResponseStatus? to,
        int? submissionNumber,
        int? reviewLevel,
        string? reason,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        db.Add(new FormResponseHistory
        {
            ResponseId = response.Id,
            EventType = eventType,
            FromStatus = from,
            ToStatus = to,
            SubmissionNumber = submissionNumber,
            ReviewLevel = reviewLevel,
            Reason = reason,
            ActorUserId = actorUserId,
            OccurredAtUtc = now
        });
        await Task.CompletedTask;
    }

    private async Task<IReadOnlyDictionary<Guid, Attachment>> LoadReferencedAttachmentsAsync(
        JsonElement answers,
        Guid responseId,
        CancellationToken ct)
    {
        var ids = new HashSet<Guid>();
        CollectGuids(answers, ids);
        if (ids.Count == 0) return new Dictionary<Guid, Attachment>();
        var list = await db.Attachments.AsNoTracking()
            .Where(a => ids.Contains(a.Id)
                && a.EntityType == "FormResponse"
                && a.EntityId == responseId)
            .ToListAsync(ct);
        return list.ToDictionary(a => a.Id);
    }

    private static void CollectGuids(JsonElement element, HashSet<Guid> ids)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String when Guid.TryParse(element.GetString(), out var id):
                ids.Add(id);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) CollectGuids(item, ids);
                break;
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject()) CollectGuids(prop.Value, ids);
                break;
        }
    }

    private static FormResponseValidationResultDto ToValidationDto(FormResponseValidationResult validation) =>
        new(
            validation.IsValid,
            validation.Issues,
            validation.CanonicalAnswersJson,
            validation.AnswersHash,
            validation.CalculatedValues,
            validation.VisibleFieldKeys.ToList(),
            validation.RequiredFieldKeys.ToList());

    private sealed class ResponseContext(
        FormFacilityAssignment assignment,
        FormCycle cycle,
        FormCampaign campaign,
        FormCampaignResponsePolicy policy,
        FormDefinition form,
        FormSchemaSnapshot snapshot,
        FormResponse? response)
    {
        public FormFacilityAssignment Assignment { get; } = assignment;
        public FormCycle Cycle { get; } = cycle;
        public FormCampaign Campaign { get; } = campaign;
        public FormCampaignResponsePolicy Policy { get; } = policy;
        public FormDefinition Form { get; } = form;
        public FormSchemaSnapshot Snapshot { get; } = snapshot;
        public FormResponse? Response { get; set; } = response;
    }
}
