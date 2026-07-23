namespace Baseera.Application.Forms.Responses;

using System.Text.Json;
using Baseera.Application.Abstractions;
using Baseera.Application.Audit;
using Baseera.Application.Forms;
using Baseera.Domain.Attachments;
using Baseera.Domain.Audit;
using Baseera.Domain.Forms;
using Baseera.Domain.Forms.Schema;
using Microsoft.EntityFrameworkCore;

public interface IFormResponseService
{
    Task<FormResponseWorkspacePageDto> ListWorkspaceAsync(
        FormResponseWorkspaceQuery query,
        CancellationToken cancellationToken = default);

    Task<FormResponseWorkspaceDetailDto> GetAssignmentResponseAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    Task<FormResponseWorkspaceDetailDto> GetReviewerAssignmentDetailAsync(
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
        FormResponseWorkspaceQuery query,
        CancellationToken cancellationToken = default)
    {
        access.EnsureRespondentWorkspacePermission();
        FormResponseListQueries.EnsureKnownWorkStatus(query.WorkStatus);
        query = FormResponseListQueries.Normalize(query);
        var now = clock.GetUtcNow();

        var scopedFacilityIds = access.FilterFacilities(db.Facilities.AsNoTracking()).Select(f => f.Id);
        IQueryable<FormResponseListQueries.WorkspaceRow> baseQuery =
            from a in db.FormFacilityAssignments.AsNoTracking()
            join c in db.FormCycles.AsNoTracking() on a.CycleId equals c.Id
            join camp in db.FormCampaigns.AsNoTracking() on a.CampaignId equals camp.Id
            join pol in db.FormCampaignResponsePolicies.AsNoTracking() on camp.Id equals pol.CampaignId
            join r in db.FormResponses.AsNoTracking() on a.Id equals r.AssignmentId into rj
            from response in rj.DefaultIfEmpty()
            where a.IsAvailable && scopedFacilityIds.Contains(a.FacilityId)
            select new FormResponseListQueries.WorkspaceRow
            {
                Assignment = a,
                Cycle = c,
                Campaign = camp,
                Policy = pol,
                Response = response
            };

        baseQuery = FormResponseListQueries.ApplyWorkspaceFilters(baseQuery, query, now);
        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery
            .OrderBy(x => x.Cycle.DueAtUtc)
            .ThenBy(x => x.Assignment.FacilityNameArAtAssignment)
            .ThenBy(x => x.Assignment.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(row =>
        {
            var status = row.Response?.Status;
            var due = FormResponseWorkStatusResolver.ResolveEffectiveDueAt(row.Cycle.DueAtUtc, row.Response?.DueAtUtcOverride);
            var overdue = FormResponseWorkStatusResolver.IsOverdue(status, row.Policy.CompletionBasis, due, now, completion);
            var work = FormResponseWorkStatusResolver.Resolve(status, overdue);
            return MapWorkspaceItem(new WorkspaceMappingContext(
                row.Assignment, row.Cycle, row.Campaign, row.Policy, row.Response, work, overdue, due, now));
        }).ToList();

        return new FormResponseWorkspacePageDto(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<FormResponseWorkspaceDetailDto> GetAssignmentResponseAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        access.EnsureRespondentWorkspacePermission();
        var ctx = await LoadContextAsync(assignmentId, track: false, cancellationToken);
        await access.EnsureFacilityInScopeAsync(ctx.Assignment.FacilityId, cancellationToken);
        if (!access.CanActAsFacilityRespondent(ctx.Assignment.FacilityId))
        {
            throw new KeyNotFoundException("الاستحقاق غير موجود.");
        }

        return await MapDetailAsync(ctx, forReviewer: false, cancellationToken);
    }

    public async Task<FormResponseWorkspaceDetailDto> GetReviewerAssignmentDetailAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        access.EnsureReviewDetailPermission();
        var ctx = await LoadContextAsync(assignmentId, track: false, cancellationToken);
        await access.EnsureFacilityInScopeAsync(ctx.Assignment.FacilityId, cancellationToken);
        return await MapDetailAsync(ctx, forReviewer: true, cancellationToken);
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
            FormAccessHelper.EnsureDraftVersion(response, request.ExpectedDraftVersion);
            FormAccessHelper.EnsureResponseRowVersion(response, request.RowVersion);

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
                WriteHistory(new ResponseHistoryWrite(
                    response, "FormResponseStarted", null, FormResponseStatus.Draft, null, null, null, userId, now));
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
            var submissionContext = await LoadSubmissionContextAsync(assignmentId, request, ct);
            ValidateSubmissionRequest(submissionContext);
            ValidateSubmissionTiming(submissionContext);
            var validation = await ValidateAndCanonicalizeAnswersAsync(submissionContext, ct);
            var snapshot = CreateSubmissionSnapshot(submissionContext, validation);
            ApplySubmissionState(submissionContext, snapshot, validation);
            await WriteSubmissionAuditAsync(submissionContext, snapshot, ct);
            await db.SaveChangesAsync(ct);
            return new FormResponseSubmitResultDto(
                submissionContext.Response.Id,
                snapshot.Submission.Id,
                snapshot.SubmissionNumber,
                submissionContext.Response.Status,
                Convert.ToBase64String(submissionContext.Response.RowVersion),
                submissionContext.Now);
        }, cancellationToken);
    }

    private async Task<SubmissionContext> LoadSubmissionContextAsync(
        Guid assignmentId,
        FormResponseSubmitRequest request,
        CancellationToken ct)
    {
        var ctx = await LoadContextAsync(assignmentId, track: true, ct);
        await access.EnsureFacilityInScopeAsync(ctx.Assignment.FacilityId, ct);
        EnsureAssignmentEditable(ctx);
        var response = ctx.Response ?? await CreateDraftResponseAsync(ctx, ct);
        EnsureDraftEditable(response);
        FormAccessHelper.EnsureDraftVersion(response, request.ExpectedDraftVersion);
        FormAccessHelper.EnsureResponseRowVersion(response, request.RowVersion);
        var now = clock.GetUtcNow();
        var due = FormResponseWorkStatusResolver.ResolveEffectiveDueAt(ctx.Cycle.DueAtUtc, response.DueAtUtcOverride);
        return new SubmissionContext(ctx, response, request, now, due, access.UserId);
    }

    private static void ValidateSubmissionRequest(SubmissionContext context)
    {
        if (context.Ctx.Policy.RequireSubmissionAcknowledgement && !context.Request.Acknowledged)
        {
            throw new ArgumentException("الإقرار مطلوب قبل الإرسال.");
        }

        if (context.Response.Status == FormResponseStatus.Returned
            && !context.Ctx.Policy.AllowResubmissionAfterReturn)
        {
            throw new InvalidOperationException("إعادة الإرسال بعد الإعادة غير مسموحة.");
        }
    }

    private static void ValidateSubmissionTiming(SubmissionContext context)
    {
        context.Late = context.Now > context.Due;
        if (context.Late && !context.Ctx.Policy.AllowLateSubmission)
        {
            throw new InvalidOperationException("انتهى الموعد والإرسال المتأخر غير مسموح.");
        }
    }

    private async Task<FormResponseValidationResult> ValidateAndCanonicalizeAnswersAsync(
        SubmissionContext context,
        CancellationToken ct)
    {
        var schema = FormResponseSchemaLoader.Parse(context.Ctx.Snapshot.CanonicalSchemaJson);
        FormResponseSchemaLoader.EnsureSchemaHashMatches(context.Response, context.Ctx.Cycle);
        var attachments = await LoadReferencedAttachmentsAsync(context.Request.Answers, context.Response.Id, ct);
        var validation = validator.Validate(schema, context.Request.Answers, FormResponseValidationMode.FullSubmit, attachments);
        if (!validation.IsValid)
        {
            throw new FormResponseValidationException(validation.Issues.Where(i => i.Severity == "Error").ToList());
        }

        return validation;
    }

    private SubmissionSnapshot CreateSubmissionSnapshot(
        SubmissionContext context,
        FormResponseValidationResult validation)
    {
        var from = context.Response.Status;
        if (from == FormResponseStatus.Returned)
        {
            FormResponseStateMachine.EnsureCanTransition(FormResponseStatus.Returned, FormResponseStatus.Draft);
            context.Response.Status = FormResponseStatus.Draft;
            from = FormResponseStatus.Draft;
        }

        FormResponseStateMachine.EnsureCanTransition(from, FormResponseStatus.Submitted);
        var target = FormResponseStateMachine.ResolveSubmissionTargetStatus(context.Ctx.Policy.ReviewMode);
        if (target == FormResponseStatus.UnderReview)
        {
            FormResponseStateMachine.EnsureCanTransition(FormResponseStatus.Submitted, FormResponseStatus.UnderReview);
        }

        context.FromStatus = from;
        context.TargetStatus = target;
        var submissionNumber = context.Response.CurrentSubmissionNumber + 1;
        var submission = new FormResponseSubmission
        {
            ResponseId = context.Response.Id,
            SubmissionNumber = submissionNumber,
            FormSchemaSnapshotId = context.Response.FormSchemaSnapshotId,
            SchemaHash = context.Response.SchemaHash,
            CanonicalAnswersJson = validation.CanonicalAnswersJson,
            AnswersHash = validation.AnswersHash,
            SubmittedByUserId = context.UserId,
            SubmittedAtUtc = context.Now,
            WasLateAtSubmission = context.Late,
            EffectiveDueAtSubmissionUtc = context.Due,
            Acknowledged = context.Request.Acknowledged,
            AcknowledgementText = context.Request.AcknowledgementText,
            AcknowledgedAtUtc = context.Request.Acknowledged ? context.Now : null
        };
        db.Add(submission);
        return new SubmissionSnapshot(submission, submissionNumber);
    }

    private void ApplySubmissionState(
        SubmissionContext context,
        SubmissionSnapshot snapshot,
        FormResponseValidationResult validation)
    {
        context.Response.DraftAnswersJson = validation.CanonicalAnswersJson;
        context.Response.DraftAnswersHash = validation.AnswersHash;
        context.Response.DraftVersion += 1;
        context.Response.CurrentSubmissionNumber = snapshot.SubmissionNumber;
        context.Response.CurrentReviewLevel = context.Ctx.Policy.ReviewMode == FormReviewMode.None ? 0 : 1;
        context.Response.Status = context.TargetStatus;
        context.Response.SubmittedAtUtc = context.Now;
        context.Response.SubmittedByUserId = context.UserId;
        context.Response.LastSavedAtUtc = context.Now;
        context.Response.LastSavedByUserId = context.UserId;
        context.Response.UpdatedAtUtc = context.Now;
    }

    private async Task WriteSubmissionAuditAsync(SubmissionContext context, SubmissionSnapshot snapshot, CancellationToken ct)
    {
        WriteHistory(new ResponseHistoryWrite(
            context.Response,
            "FormResponseSubmitted",
            context.FromStatus,
            context.TargetStatus,
            snapshot.SubmissionNumber,
            context.Response.CurrentReviewLevel,
            null,
            context.UserId,
            context.Now));
        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormResponseSubmitted",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormResponse),
            EntityId = context.Response.Id.ToString(),
            NewValues = new
            {
                context.Response.AssignmentId,
                context.Response.CampaignId,
                context.Response.CycleId,
                context.Response.FacilityId,
                snapshot.SubmissionNumber,
                FromStatus = context.FromStatus,
                ToStatus = context.TargetStatus
            }
        }, ct);
    }

    private static bool IsBlockingDraftIssue(string code) =>
        code is "UNKNOWN_FIELD" or "DUPLICATE_KEY" or "TYPE_MISMATCH" or "READONLY_WRITE"
            or "CALCULATED_WRITE" or "ATTACHMENT_UNAUTHORIZED" or "MALFORMED" or "PAYLOAD_TOO_LARGE"
            or "TOO_MANY_KEYS" or "TOO_MANY_ROWS" or "DUPLICATE_ROW";

    private FormResponseWorkspaceItemDto MapWorkspaceItem(WorkspaceMappingContext mapping) =>
        new(
            mapping.Assignment.Id,
            mapping.Campaign.Id,
            mapping.Campaign.Code,
            mapping.Campaign.NameAr,
            mapping.Cycle.Id,
            mapping.Cycle.OccurrenceKey,
            mapping.Assignment.FacilityId,
            mapping.Assignment.FacilityNameArAtAssignment,
            mapping.Assignment.RegionIdAtAssignment,
            mapping.Assignment.RegionNameArAtAssignment,
            mapping.Cycle.OpenAtUtc,
            mapping.Cycle.DueAtUtc,
            mapping.Cycle.GraceEndsAtUtc,
            mapping.Cycle.CloseAtUtc,
            mapping.Due,
            mapping.Response?.Id,
            mapping.Response?.Status,
            mapping.Work,
            mapping.Overdue,
            completion.IsCompleted(mapping.Policy.CompletionBasis, mapping.Response?.Status),
            mapping.Response?.DraftVersion,
            mapping.Response?.LastSavedAtUtc,
            mapping.Response?.SubmittedAtUtc,
            mapping.Response?.CurrentReviewLevel ?? 0,
            mapping.Policy.RequiredApprovalLevels,
            ResolveRespondentActions(mapping.Assignment, mapping.Cycle, mapping.Response, mapping.Policy, mapping.Now),
            mapping.Response is null ? null : Convert.ToBase64String(mapping.Response.RowVersion));

    private async Task<FormResponseWorkspaceDetailDto> MapDetailAsync(ResponseContext ctx, bool forReviewer, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var status = ctx.Response?.Status;
        var due = FormResponseWorkStatusResolver.ResolveEffectiveDueAt(ctx.Cycle.DueAtUtc, ctx.Response?.DueAtUtcOverride);
        var overdue = FormResponseWorkStatusResolver.IsOverdue(status, ctx.Policy.CompletionBasis, due, now, completion);
        var work = FormResponseWorkStatusResolver.Resolve(status, overdue);
        var schema = FormResponseSchemaLoader.Parse(ctx.Snapshot.CanonicalSchemaJson);
        var isOwner = ctx.Response is not null && access.IsRespondentOwner(ctx.Response);
        if (forReviewer)
        {
            isOwner = false;
        }

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

            var commentQuery = db.FormResponseReviewComments.AsNoTracking()
                .Where(c => c.ResponseId == ctx.Response.Id);
            if (!forReviewer)
            {
                commentQuery = commentQuery.Where(c => c.IsVisibleToRespondent);
            }

            comments = await commentQuery
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
            ResolveRespondentActions(ctx.Assignment, ctx.Cycle, ctx.Response, ctx.Policy, now),
            ctx.Response is null ? null : Convert.ToBase64String(ctx.Response.RowVersion),
            projected.Visibility,
            projected.Redacted);
    }

    private static IReadOnlyList<string> ResolveRespondentActions(
        FormFacilityAssignment assignment,
        FormCycle cycle,
        FormResponse? response,
        FormCampaignResponsePolicy policy,
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

    private void WriteHistory(ResponseHistoryWrite write)
    {
        db.Add(new FormResponseHistory
        {
            ResponseId = write.Response.Id,
            EventType = write.EventType,
            FromStatus = write.From,
            ToStatus = write.To,
            SubmissionNumber = write.SubmissionNumber,
            ReviewLevel = write.ReviewLevel,
            Reason = write.Reason,
            ActorUserId = write.ActorUserId,
            OccurredAtUtc = write.Now
        });
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

    private sealed class SubmissionContext(
        ResponseContext ctx,
        FormResponse response,
        FormResponseSubmitRequest request,
        DateTimeOffset now,
        DateTimeOffset due,
        Guid userId)
    {
        public ResponseContext Ctx { get; } = ctx;
        public FormResponse Response { get; } = response;
        public FormResponseSubmitRequest Request { get; } = request;
        public DateTimeOffset Now { get; } = now;
        public DateTimeOffset Due { get; } = due;
        public Guid UserId { get; } = userId;
        public bool Late { get; set; }
        public FormResponseStatus FromStatus { get; set; }
        public FormResponseStatus TargetStatus { get; set; }
    }

    private sealed record SubmissionSnapshot(FormResponseSubmission Submission, int SubmissionNumber);

    private sealed record WorkspaceMappingContext(
        FormFacilityAssignment Assignment,
        FormCycle Cycle,
        FormCampaign Campaign,
        FormCampaignResponsePolicy Policy,
        FormResponse? Response,
        FormAssignmentWorkStatus Work,
        bool Overdue,
        DateTimeOffset Due,
        DateTimeOffset Now);

    private sealed record ResponseHistoryWrite(
        FormResponse Response,
        string EventType,
        FormResponseStatus? From,
        FormResponseStatus? To,
        int? SubmissionNumber,
        int? ReviewLevel,
        string? Reason,
        Guid ActorUserId,
        DateTimeOffset Now);

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
