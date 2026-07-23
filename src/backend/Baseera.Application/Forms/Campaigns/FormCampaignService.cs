namespace Baseera.Application.Forms.Campaigns;

using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Application.Forms;
using Baseera.Domain.Attachments;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public interface IFormCampaignService
{
    Task<PagedResult<FormCampaignListItemDto>> ListAsync(PagedQuery query, FormCampaignStatus? status, Guid? formDefinitionId, CancellationToken cancellationToken = default);
    Task<FormCampaignDetailDto> GetAsync(Guid campaignId, CancellationToken cancellationToken = default);
    Task<FormCampaignDetailDto> CreateAsync(CreateFormCampaignRequest request, CancellationToken cancellationToken = default);
    Task<FormCampaignDetailDto> UpdateAsync(Guid campaignId, UpdateFormCampaignRequest request, CancellationToken cancellationToken = default);
    Task<FormCampaignDetailDto> CloneAsync(Guid campaignId, CancellationToken cancellationToken = default);
    Task<FormTargetPreviewDto> PreviewTargetsAsync(Guid campaignId, CancellationToken cancellationToken = default);
    Task<FormTargetPreviewDto> PreviewTargetsForRequestAsync(Guid organizationId, IReadOnlyList<FormCampaignTargetRequest> targets, IReadOnlyList<FormCampaignExclusionRequest>? exclusions, CancellationToken cancellationToken = default);
    Task<FormCampaignDetailDto> PublishAsync(Guid campaignId, PublishFormCampaignRequest request, CancellationToken cancellationToken = default);
    Task<FormCampaignDetailDto> PauseAsync(Guid campaignId, FormCampaignTransitionRequest request, CancellationToken cancellationToken = default);
    Task<FormCampaignDetailDto> ResumeAsync(Guid campaignId, FormCampaignTransitionRequest request, CancellationToken cancellationToken = default);
    Task<FormCampaignDetailDto> CancelAsync(Guid campaignId, FormCampaignTransitionRequest request, CancellationToken cancellationToken = default);
    Task<FormCampaignDetailDto> CompleteAsync(Guid campaignId, FormCampaignTransitionRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<FormCycleListItemDto>> ListCyclesAsync(Guid campaignId, PagedQuery query, CancellationToken cancellationToken = default);
    Task<FormCycleDetailDto> GetCycleAsync(Guid campaignId, Guid cycleId, CancellationToken cancellationToken = default);
    Task<PagedResult<FacilityAssignmentDto>> ListAssignmentsAsync(Guid campaignId, Guid cycleId, PagedQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<FormTargetPreviewFacilityDto>> ListTargetOptionRegionsAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<FormTargetPreviewFacilityDto>> ListTargetOptionFacilitiesAsync(Guid? regionId, PagedQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DateTimeOffset>> PreviewUpcomingAsync(FormCampaignScheduleRequest schedule, string? timeZoneId, int count = 10, CancellationToken cancellationToken = default);
}

public sealed class FormCampaignService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IFormScopeService formScope,
    IFormEffectiveAccessService effectiveAccess,
    IOrganizationalScopeService orgScope,
    IFormTargetResolver targetResolver,
    IFormRecurrenceCalculator recurrence,
    IFormTimeZoneResolver timeZones,
    IFormCycleGenerationService cycleGeneration,
    IAuditService audit,
    TimeProvider timeProvider) : IFormCampaignService
{
    public async Task<PagedResult<FormCampaignListItemDto>> ListAsync(
        PagedQuery query,
        FormCampaignStatus? status,
        Guid? formDefinitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureAnyViewPermission();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var scopedForms = await formScope.FilterQueryableAsync(db.FormDefinitions.AsNoTracking(), cancellationToken);
        var scopedFormIds = scopedForms.Select(f => f.Id);

        var q = db.FormCampaigns.AsNoTracking()
            .Include(c => c.FormDefinition)
            .Include(c => c.FormVersion)
            .Where(c => scopedFormIds.Contains(c.FormDefinitionId));

        if (status is not null)
        {
            q = q.Where(c => c.Status == status);
        }

        if (formDefinitionId is not null)
        {
            q = q.Where(c => c.FormDefinitionId == formDefinitionId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(c => c.Code.Contains(term)
                || c.NameAr.Contains(term)
                || (c.NameEn != null && c.NameEn.Contains(term)));
        }

        var canViewSensitiveViaRole = FormAccessHelper.CanViewSensitive(currentUser)
            || currentUser.HasPermission(PermissionCodes.FormsMonitorHeadquarters)
            || currentUser.HasPermission(PermissionCodes.AuditView);

        if (!canViewSensitiveViaRole)
        {
            q = q.Where(c => c.FormDefinition.Classification < ClassificationLevel.Confidential);
        }

        var candidateFormIds = await q.Select(c => c.FormDefinitionId).Distinct().ToListAsync(cancellationToken);
        var allowedFormIds = new HashSet<Guid>();
        if (candidateFormIds.Count > 0 && currentUser.UserId is { } listUserId)
        {
            var roleIds = await db.UserRoles.AsNoTracking()
                .Where(r => r.UserId == listUserId)
                .Select(r => r.RoleId)
                .ToListAsync(cancellationToken);
            var grantsByFormId = await db.FormAccessGrants.AsNoTracking()
                .Where(g => candidateFormIds.Contains(g.FormDefinitionId))
                .GroupBy(g => g.FormDefinitionId)
                .ToDictionaryAsync(g => g.Key, g => g.ToList(), cancellationToken);
            var forms = await db.FormDefinitions.AsNoTracking()
                .Where(f => candidateFormIds.Contains(f.Id))
                .ToListAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();

            foreach (var form in forms)
            {
                grantsByFormId.TryGetValue(form.Id, out var grants);
                var decision = FormGrantResolver.ResolveEffectiveGrant(
                    grants ?? [],
                    FormAccessCapability.View,
                    listUserId,
                    roleIds,
                    form,
                    now);
                if (decision is not false)
                {
                    allowedFormIds.Add(form.Id);
                }
            }
        }

        q = q.Where(c => allowedFormIds.Contains(c.FormDefinitionId));

        var total = await q.CountAsync(cancellationToken);
        var items = await q.OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var campaignIds = items.Select(i => i.Id).ToList();
        var cycleCounts = campaignIds.Count == 0
            ? []
            : await db.FormCycles.AsNoTracking()
                .Where(c => campaignIds.Contains(c.CampaignId))
                .GroupBy(c => c.CampaignId)
                .Select(g => new { CampaignId = g.Key, Count = g.Count(), Last = g.Max(x => (DateTimeOffset?)x.GeneratedAtUtc) })
                .ToListAsync(cancellationToken);
        var countMap = cycleCounts.ToDictionary(x => x.CampaignId, x => x);

        var dtos = new List<FormCampaignListItemDto>();
        foreach (var c in items)
        {
            countMap.TryGetValue(c.Id, out var stats);
            dtos.Add(new FormCampaignListItemDto(
                c.Id, c.Code, c.NameAr, c.NameEn, c.FormDefinitionId, c.FormDefinition.Code, c.FormDefinition.NameAr,
                c.FormVersionId, c.FormVersion.VersionNumber, c.Status, c.RecurrenceKind, c.FirstOpenAtLocal,
                c.NextOccurrenceUtc, stats?.Count ?? 0, stats?.Last,
                await BuildAllowedActionsAsync(c, cancellationToken),
                Convert.ToBase64String(c.RowVersion)));
        }

        return new PagedResult<FormCampaignListItemDto>
        {
            Items = dtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<FormCampaignDetailDto> GetAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await LoadVisibleAsync(campaignId, cancellationToken);
        return await MapDetailAsync(campaign, cancellationToken);
    }

    public async Task<FormCampaignDetailDto> CreateAsync(CreateFormCampaignRequest request, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsManageCampaigns);
        ValidateSchedule(request.Schedule);
        ValidateTargets(request.Targets, request.Exclusions);

        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, request.FormDefinitionId, cancellationToken: cancellationToken);
        if (form.Status == FormDefinitionStatus.Archived)
        {
            throw new InvalidOperationException("لا يمكن إنشاء حملة لنموذج مؤرشف.");
        }

        await effectiveAccess.EnsureCapabilityAsync(form, FormAccessCapability.View, cancellationToken);
        if (FormAccessHelper.RequiresSensitive(form.Classification) && !FormAccessHelper.CanViewSensitive(currentUser))
        {
            throw new KeyNotFoundException("النموذج غير موجود.");
        }

        var version = await db.FormVersions.Include(v => v.Snapshot)
            .FirstOrDefaultAsync(v => v.Id == request.FormVersionId && v.FormDefinitionId == form.Id, cancellationToken)
            ?? throw new KeyNotFoundException("إصدار النموذج غير موجود.");
        if (version.Status != FormVersionStatus.Locked || version.SnapshotId is null || version.Snapshot is null)
        {
            throw new InvalidOperationException("يُسمح بالنشر فقط من إصدار مقفل مع لقطة.");
        }

        var orgId = await ResolveOrganizationIdAsync(form, cancellationToken);
        _ = timeZones.Resolve(request.TimeZoneId ?? FormTimeZoneResolver.DefaultTimeZoneId);
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير معروف.");
        var now = timeProvider.GetUtcNow();

        var campaign = new FormCampaign
        {
            OrganizationId = orgId,
            FormDefinitionId = form.Id,
            FormVersionId = version.Id,
            FormSchemaSnapshotId = version.Snapshot.Id,
            SchemaHash = version.Snapshot.SchemaHash,
            Code = request.Code.Trim(),
            NameAr = request.NameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = FormCampaignStatus.Draft,
            Priority = request.Priority,
            TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? FormTimeZoneResolver.DefaultTimeZoneId : request.TimeZoneId.Trim(),
            RecurrenceKind = request.Schedule.RecurrenceKind,
            RecurrenceConfigurationJson = recurrence.SerializeSchedule(request.Schedule),
            FirstOpenAtLocal = request.Schedule.FirstOpenAtLocal,
            ResponseWindowMinutes = request.Schedule.ResponseWindowMinutes,
            GracePeriodMinutes = request.Schedule.GracePeriodMinutes,
            CloseAfterMinutes = request.Schedule.CloseAfterMinutes,
            BusinessDayAdjustment = request.Schedule.BusinessDayAdjustment,
            CreatedByUserId = userId,
            CreatedAtUtc = now
        };

        ApplyTargetsAndExclusions(campaign, request.Targets, request.Exclusions, userId, now);
        db.Add(campaign);
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormCampaignCreated",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormCampaign),
            EntityId = campaign.Id.ToString(),
            NewValues = new { campaign.Code, campaign.FormVersionId, campaign.SchemaHash, campaign.RecurrenceKind }
        }, cancellationToken);

        return await GetAsync(campaign.Id, cancellationToken);
    }

    public async Task<FormCampaignDetailDto> UpdateAsync(Guid campaignId, UpdateFormCampaignRequest request, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsManageCampaigns);
        ValidateSchedule(request.Schedule);
        ValidateTargets(request.Targets, request.Exclusions);

        var campaign = await LoadTrackedAsync(campaignId, cancellationToken);
        if (!FormCampaignStateMachine.IsMutable(campaign.Status))
        {
            throw new InvalidOperationException("لا يمكن تعديل الحملة بعد النشر. استخدم النسخ لمسودة جديدة.");
        }

        FormAccessHelper.EnsureRowVersion(campaign.RowVersion, request.RowVersion);
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, campaign.FormDefinitionId, cancellationToken: cancellationToken);
        await effectiveAccess.EnsureCapabilityAsync(form, FormAccessCapability.View, cancellationToken);

        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير معروف.");
        var now = timeProvider.GetUtcNow();
        campaign.NameAr = request.NameAr.Trim();
        campaign.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim();
        campaign.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        campaign.Priority = request.Priority;
        campaign.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? FormTimeZoneResolver.DefaultTimeZoneId : request.TimeZoneId.Trim();
        campaign.RecurrenceKind = request.Schedule.RecurrenceKind;
        campaign.RecurrenceConfigurationJson = recurrence.SerializeSchedule(request.Schedule);
        campaign.FirstOpenAtLocal = request.Schedule.FirstOpenAtLocal;
        campaign.ResponseWindowMinutes = request.Schedule.ResponseWindowMinutes;
        campaign.GracePeriodMinutes = request.Schedule.GracePeriodMinutes;
        campaign.CloseAfterMinutes = request.Schedule.CloseAfterMinutes;
        campaign.BusinessDayAdjustment = request.Schedule.BusinessDayAdjustment;
        campaign.UpdatedByUserId = userId;
        campaign.UpdatedAtUtc = now;
        await ReplaceTargetsAsync(campaign.Id, request.Targets, request.Exclusions, userId, now, cancellationToken);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormCampaignUpdated",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormCampaign),
            EntityId = campaign.Id.ToString(),
            NewValues = new { campaign.NameAr, campaign.RecurrenceKind, campaign.SchemaHash }
        }, cancellationToken);

        return await GetAsync(campaign.Id, cancellationToken);
    }

    public async Task<FormCampaignDetailDto> CloneAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsManageCampaigns);
        var source = await LoadVisibleAsync(campaignId, cancellationToken);
        var schedule = MapSchedule(source);
        var targets = source.TargetRules.Select(r => FormTargetResolver.DeserializeTarget(r.RuleType, r.ConfigurationJson)).ToList();
        var exclusions = source.Exclusions.Select(e => new FormCampaignExclusionRequest(e.FacilityId, e.Reason)).ToList();
        var created = await CreateAsync(new CreateFormCampaignRequest(
            source.FormDefinitionId,
            source.FormVersionId,
            $"{source.Code}-COPY-{Guid.NewGuid():N}"[..Math.Min(80, source.Code.Length + 15)],
            $"{source.NameAr} (نسخة)",
            source.NameEn,
            source.Description,
            source.Priority,
            source.TimeZoneId,
            schedule,
            targets,
            exclusions), cancellationToken);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormCampaignCloned",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormCampaign),
            EntityId = created.Id.ToString(),
            NewValues = new { SourceCampaignId = campaignId, created.Code }
        }, cancellationToken);
        return created;
    }

    public async Task<FormTargetPreviewDto> PreviewTargetsAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        EnsurePreviewPermission();
        var campaign = await LoadVisibleAsync(campaignId, cancellationToken);
        var targets = campaign.TargetRules.Select(r => FormTargetResolver.DeserializeTarget(r.RuleType, r.ConfigurationJson)).ToList();
        var exclusions = campaign.Exclusions.Select(e => new FormCampaignExclusionRequest(e.FacilityId, e.Reason)).ToList();
        var preview = await PreviewTargetsForRequestAsync(campaign.OrganizationId, targets, exclusions, cancellationToken);
        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormCampaignTargetPreviewed",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormCampaign),
            EntityId = campaignId.ToString(),
            NewValues = new { preview.FinalTargetCount, preview.TotalExcluded, preview.TargetingFingerprint }
        }, cancellationToken);
        return preview;
    }

    public async Task<FormTargetPreviewDto> PreviewTargetsForRequestAsync(
        Guid organizationId,
        IReadOnlyList<FormCampaignTargetRequest> targets,
        IReadOnlyList<FormCampaignExclusionRequest>? exclusions,
        CancellationToken cancellationToken = default)
    {
        EnsurePreviewPermission();
        var resolution = await targetResolver.ResolveAsync(organizationId, targets, exclusions ?? [], cancellationToken);
        var sample = resolution.Included.Take(50).Select(x => new FormTargetPreviewFacilityDto(
            x.FacilityId, x.FacilityCode, x.FacilityNameAr, x.RegionId, x.RegionNameAr, x.FacilityType)).ToList();
        return new FormTargetPreviewDto(
            timeProvider.GetUtcNow(),
            resolution.Included.Count + resolution.Excluded.Count,
            resolution.Excluded.Count,
            resolution.Included.Count,
            resolution.BreakdownByRegion,
            resolution.BreakdownByFacilityType,
            resolution.Included.Select(x => x.FacilityId).ToList(),
            resolution.Excluded.Select(x => new FormTargetPreviewExclusionDto(x.FacilityId, x.Reason)).ToList(),
            sample,
            resolution.TargetingFingerprint,
            resolution.Warnings,
            resolution.InvalidTargets,
            resolution.Included.Where(x => !x.IsAvailable).Select(x => x.FacilityCode).ToList());
    }

    public async Task<FormCampaignDetailDto> PublishAsync(Guid campaignId, PublishFormCampaignRequest request, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsPublish);
        var campaign = await LoadTrackedAsync(campaignId, cancellationToken);
        FormAccessHelper.EnsureRowVersion(campaign.RowVersion, request.RowVersion);
        if (campaign.Status != FormCampaignStatus.Draft)
        {
            throw new InvalidOperationException("يمكن نشر المسودات فقط.");
        }

        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, campaign.FormDefinitionId, cancellationToken: cancellationToken);
        if (form.Status == FormDefinitionStatus.Archived)
        {
            throw new InvalidOperationException("لا يمكن نشر حملة لنموذج مؤرشف.");
        }

        await effectiveAccess.EnsureCapabilityAsync(form, FormAccessCapability.Publish, cancellationToken);
        var version = await db.FormVersions.Include(v => v.Snapshot)
            .FirstAsync(v => v.Id == campaign.FormVersionId, cancellationToken);
        if (version.Status != FormVersionStatus.Locked || version.Snapshot is null)
        {
            throw new InvalidOperationException("يُسمح بالنشر فقط من إصدار مقفل مع لقطة.");
        }

        campaign.FormSchemaSnapshotId = version.Snapshot.Id;
        campaign.SchemaHash = version.Snapshot.SchemaHash;

        var schedule = MapSchedule(campaign);
        var firstLocal = schedule.FirstOpenAtLocal;
        var firstUtc = timeZones.ToUtc(firstLocal, campaign.TimeZoneId);
        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير معروف.");

        var targetStatus = firstUtc <= now ? FormCampaignStatus.Active : FormCampaignStatus.Scheduled;
        FormCampaignStateMachine.EnsureCanTransition(campaign.Status, targetStatus);
        campaign.Status = targetStatus;
        campaign.PublishedAtUtc = now;
        campaign.PublishedByUserId = userId;
        campaign.NextOccurrenceUtc = firstUtc;
        campaign.UpdatedByUserId = userId;
        campaign.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        if (firstUtc <= now)
        {
            await cycleGeneration.TryGenerateOccurrenceAsync(campaign, firstLocal, currentUser.DisplayName ?? "publisher", cancellationToken);
            var next = recurrence.ComputeNextAfter(schedule, firstLocal);
            campaign.LastGeneratedOccurrenceUtc = firstUtc;
            campaign.NextOccurrenceUtc = next is null ? null : timeZones.ToUtc(next.Value, campaign.TimeZoneId);
            if (campaign.RecurrenceKind == FormRecurrenceKind.Once)
            {
                campaign.NextOccurrenceUtc = null;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormCampaignPublished",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormCampaign),
            EntityId = campaign.Id.ToString(),
            NewValues = new { campaign.Status, campaign.FormVersionId, campaign.SchemaHash, campaign.NextOccurrenceUtc }
        }, cancellationToken);

        return await GetAsync(campaign.Id, cancellationToken);
    }

    public Task<FormCampaignDetailDto> PauseAsync(Guid campaignId, FormCampaignTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(campaignId, FormCampaignStatus.Paused, PermissionCodes.FormsPauseCampaign, "FormCampaignPaused", request, cancellationToken);

    public Task<FormCampaignDetailDto> ResumeAsync(Guid campaignId, FormCampaignTransitionRequest request, CancellationToken cancellationToken = default) =>
        db.ExecuteInTransactionAsync(async ct =>
        {
            FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsPauseCampaign);
            var campaign = await LoadTrackedAsync(campaignId, ct);
            FormAccessHelper.EnsureRowVersion(campaign.RowVersion, request.RowVersion);

            if (campaign.Status is FormCampaignStatus.Completed or FormCampaignStatus.Cancelled)
            {
                throw new InvalidOperationException("لا يمكن استئناف حملة مكتملة أو ملغاة.");
            }

            var now = timeProvider.GetUtcNow();
            var next = await ResolveNextOccurrenceForResumeAsync(campaign, now, ct);
            campaign.PausedAtUtc = null;
            campaign.PausedByUserId = null;
            campaign.PauseReason = null;
            campaign.UpdatedByUserId = currentUser.UserId;
            campaign.UpdatedAtUtc = now;

            if (next is null)
            {
                if (FormCampaignStateMachine.CanTransition(campaign.Status, FormCampaignStatus.Completed))
                {
                    FormCampaignStateMachine.EnsureCanTransition(campaign.Status, FormCampaignStatus.Completed);
                    campaign.Status = FormCampaignStatus.Completed;
                    campaign.ClosedAtUtc = now;
                    campaign.NextOccurrenceUtc = null;
                }
                else
                {
                    throw new InvalidOperationException("لا توجد دورات قادمة ولا يمكن إكمال الحملة.");
                }
            }
            else
            {
                var resumeTo = next > now ? FormCampaignStatus.Scheduled : FormCampaignStatus.Active;
                FormCampaignStateMachine.EnsureCanTransition(campaign.Status, resumeTo);
                campaign.Status = resumeTo;
                campaign.NextOccurrenceUtc = next;
            }

            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(new AuditEntry
            {
                Action = "FormCampaignResumed",
                Module = FormAccessHelper.ModuleName,
                EntityType = nameof(FormCampaign),
                EntityId = campaign.Id.ToString(),
                NewValues = new { campaign.Status, campaign.NextOccurrenceUtc },
                Reason = request.Reason
            }, ct);

            return await GetAsync(campaign.Id, ct);
        }, cancellationToken);

    public Task<FormCampaignDetailDto> CancelAsync(Guid campaignId, FormCampaignTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(campaignId, FormCampaignStatus.Cancelled, PermissionCodes.FormsCancelCampaign, "FormCampaignCancelled", request, cancellationToken, requireReason: true);

    public Task<FormCampaignDetailDto> CompleteAsync(Guid campaignId, FormCampaignTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(campaignId, FormCampaignStatus.Completed, PermissionCodes.FormsPublish, "FormCampaignCompleted", request, cancellationToken);

    public async Task<PagedResult<FormCycleListItemDto>> ListCyclesAsync(Guid campaignId, PagedQuery query, CancellationToken cancellationToken = default)
    {
        _ = await LoadVisibleAsync(campaignId, cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var q = db.FormCycles.AsNoTracking().Where(c => c.CampaignId == campaignId);
        var total = await q.CountAsync(cancellationToken);
        var items = await q.OrderByDescending(c => c.SequenceNumber)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new FormCycleListItemDto(
                c.Id, c.SequenceNumber, c.OccurrenceKey, c.Status, c.ScheduledOccurrenceLocal,
                c.OpenAtUtc, c.DueAtUtc, c.CloseAtUtc, c.AssignedFacilityCount, c.TargetSnapshotHash))
            .ToListAsync(cancellationToken);
        return new PagedResult<FormCycleListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<FormCycleDetailDto> GetCycleAsync(Guid campaignId, Guid cycleId, CancellationToken cancellationToken = default)
    {
        _ = await LoadVisibleAsync(campaignId, cancellationToken);
        var cycle = await db.FormCycles.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.CampaignId == campaignId, cancellationToken)
            ?? throw new KeyNotFoundException("الدورة غير موجودة.");
        return new FormCycleDetailDto(
            cycle.Id, cycle.CampaignId, cycle.SequenceNumber, cycle.OccurrenceKey, cycle.Status,
            cycle.ScheduledOccurrenceLocal, cycle.ScheduledOccurrenceUtc, cycle.OpenAtUtc, cycle.DueAtUtc,
            cycle.GraceEndsAtUtc, cycle.CloseAtUtc, cycle.TimeZoneId, cycle.FormVersionId, cycle.FormSchemaSnapshotId,
            cycle.SchemaHash, cycle.TargetSnapshotHash, cycle.AssignedFacilityCount, cycle.GeneratedAtUtc, cycle.GeneratedBy);
    }

    public async Task<PagedResult<FacilityAssignmentDto>> ListAssignmentsAsync(Guid campaignId, Guid cycleId, PagedQuery query, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsViewCampaignAssignments);
        _ = await LoadVisibleAsync(campaignId, cancellationToken);
        _ = await db.FormCycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cycleId && c.CampaignId == campaignId, cancellationToken)
            ?? throw new KeyNotFoundException("الدورة غير موجودة.");

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var q = orgScope.FilterFacilities(db.Facilities.AsNoTracking())
            .Select(f => f.Id);
        var scopedFacilityIds = await q.ToListAsync(cancellationToken);

        var assignments = db.FormFacilityAssignments.AsNoTracking()
            .Where(a => a.CycleId == cycleId && a.CampaignId == campaignId)
            .Where(a => scopedFacilityIds.Contains(a.FacilityId));

        var total = await assignments.CountAsync(cancellationToken);
        var items = await assignments.OrderBy(a => a.FacilityCodeAtAssignment)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new FacilityAssignmentDto(
                a.Id, a.FacilityId, a.RegionIdAtAssignment, a.FacilityCodeAtAssignment, a.FacilityNameArAtAssignment,
                a.RegionNameArAtAssignment, a.FacilityTypeAtAssignment, a.TargetRuleType, a.AssignedAtUtc,
                a.IsAvailable, a.UnavailableReason))
            .ToListAsync(cancellationToken);
        return new PagedResult<FacilityAssignmentDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<PagedResult<FormTargetPreviewFacilityDto>> ListTargetOptionRegionsAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        EnsurePreviewPermission();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var regions = orgScope.FilterRegions(db.Regions.AsNoTracking().Where(r => r.IsActive));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            regions = regions.Where(r => r.Code.Contains(term) || r.NameAr.Contains(term));
        }

        var total = await regions.CountAsync(cancellationToken);
        var items = await regions.OrderBy(r => r.Code)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new FormTargetPreviewFacilityDto(r.Id, r.Code, r.NameAr, r.Id, r.NameAr, null))
            .ToListAsync(cancellationToken);
        return new PagedResult<FormTargetPreviewFacilityDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<PagedResult<FormTargetPreviewFacilityDto>> ListTargetOptionFacilitiesAsync(Guid? regionId, PagedQuery query, CancellationToken cancellationToken = default)
    {
        EnsurePreviewPermission();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var facilities = orgScope.FilterFacilities(db.Facilities.AsNoTracking().Where(f => f.IsActive));
        if (regionId is { } rid)
        {
            facilities = facilities.Where(f => f.RegionId == rid);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            facilities = facilities.Where(f => f.Code.Contains(term) || f.NameAr.Contains(term));
        }

        var total = await facilities.CountAsync(cancellationToken);
        var items = await facilities.OrderBy(f => f.Code)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(f => new FormTargetPreviewFacilityDto(f.Id, f.Code, f.NameAr, f.RegionId, f.Region.NameAr, f.FacilityType))
            .ToListAsync(cancellationToken);
        return new PagedResult<FormTargetPreviewFacilityDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public Task<IReadOnlyList<DateTimeOffset>> PreviewUpcomingAsync(
        FormCampaignScheduleRequest schedule,
        string? timeZoneId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateSchedule(schedule);
        _ = timeZones.Resolve(timeZoneId ?? FormTimeZoneResolver.DefaultTimeZoneId);
        return Task.FromResult(recurrence.EnumerateUpcoming(schedule, schedule.FirstOpenAtLocal, Math.Clamp(count, 1, 20)));
    }

    private async Task<FormCampaignDetailDto> TransitionAsync(
        Guid campaignId,
        FormCampaignStatus to,
        string permission,
        string auditAction,
        FormCampaignTransitionRequest request,
        CancellationToken cancellationToken,
        bool requireReason = false)
    {
        FormAccessHelper.EnsurePermission(currentUser, permission);
        if (requireReason && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("السبب مطلوب.");
        }

        var campaign = await LoadTrackedAsync(campaignId, cancellationToken);
        FormAccessHelper.EnsureRowVersion(campaign.RowVersion, request.RowVersion);
        FormCampaignStateMachine.EnsureCanTransition(campaign.Status, to);
        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;
        campaign.Status = to;
        campaign.UpdatedAtUtc = now;
        campaign.UpdatedByUserId = userId;
        if (to == FormCampaignStatus.Paused)
        {
            campaign.PausedAtUtc = now;
            campaign.PausedByUserId = userId;
            campaign.PauseReason = request.Reason?.Trim();
            campaign.NextOccurrenceUtc = null;
        }
        else if (to == FormCampaignStatus.Cancelled)
        {
            campaign.CancelledAtUtc = now;
            campaign.CancelledByUserId = userId;
            campaign.CancellationReason = request.Reason?.Trim();
            campaign.NextOccurrenceUtc = null;
        }
        else if (to == FormCampaignStatus.Completed)
        {
            campaign.ClosedAtUtc = now;
            campaign.ClosedByUserId = userId;
            campaign.NextOccurrenceUtc = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry
        {
            Action = auditAction,
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormCampaign),
            EntityId = campaign.Id.ToString(),
            NewValues = new { campaign.Status },
            Reason = request.Reason
        }, cancellationToken);
        return await GetAsync(campaign.Id, cancellationToken);
    }

    private async Task ReplaceTargetsAsync(
        Guid campaignId,
        IReadOnlyList<FormCampaignTargetRequest> targets,
        IReadOnlyList<FormCampaignExclusionRequest>? exclusions,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var oldRules = await db.FormTargetRules.Where(r => r.CampaignId == campaignId).ToListAsync(cancellationToken);
        foreach (var rule in oldRules)
        {
            db.Remove(rule);
        }

        var oldExclusions = await db.FormCampaignExclusions.Where(e => e.CampaignId == campaignId).ToListAsync(cancellationToken);
        foreach (var exclusion in oldExclusions)
        {
            db.Remove(exclusion);
        }

        foreach (var target in targets)
        {
            db.Add(new FormTargetRule
            {
                CampaignId = campaignId,
                RuleType = target.RuleType,
                ConfigurationJson = FormTargetResolver.SerializeTarget(target),
                CreatedByUserId = userId,
                CreatedAtUtc = now
            });
        }

        foreach (var exclusion in exclusions ?? [])
        {
            if (string.IsNullOrWhiteSpace(exclusion.Reason))
            {
                throw new InvalidOperationException("سبب الاستثناء مطلوب.");
            }

            db.Add(new FormCampaignExclusion
            {
                CampaignId = campaignId,
                FacilityId = exclusion.FacilityId,
                Reason = exclusion.Reason.Trim(),
                CreatedByUserId = userId,
                CreatedAtUtc = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyTargetsAndExclusions(
        FormCampaign campaign,
        IReadOnlyList<FormCampaignTargetRequest> targets,
        IReadOnlyList<FormCampaignExclusionRequest>? exclusions,
        Guid userId,
        DateTimeOffset now)
    {
        foreach (var target in targets)
        {
            campaign.TargetRules.Add(new FormTargetRule
            {
                CampaignId = campaign.Id,
                RuleType = target.RuleType,
                ConfigurationJson = FormTargetResolver.SerializeTarget(target),
                CreatedByUserId = userId,
                CreatedAtUtc = now
            });
        }

        foreach (var exclusion in exclusions ?? [])
        {
            if (string.IsNullOrWhiteSpace(exclusion.Reason))
            {
                throw new InvalidOperationException("سبب الاستثناء مطلوب.");
            }

            campaign.Exclusions.Add(new FormCampaignExclusion
            {
                CampaignId = campaign.Id,
                FacilityId = exclusion.FacilityId,
                Reason = exclusion.Reason.Trim(),
                CreatedByUserId = userId,
                CreatedAtUtc = now
            });
        }
    }

    private async Task<Guid> ResolveOrganizationIdAsync(FormDefinition form, CancellationToken cancellationToken)
    {
        var orgId = await db.Departments.AsNoTracking()
            .Where(d => d.Id == form.OwnerDepartmentId)
            .Select(d => (Guid?)d.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);
        if (orgId is { } id)
        {
            return id;
        }

        return await db.Organizations.AsNoTracking().Where(o => o.IsActive).Select(o => o.Id).FirstAsync(cancellationToken);
    }

    private async Task<FormCampaign> LoadVisibleAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await db.FormCampaigns.AsNoTracking()
            .Include(c => c.FormDefinition)
            .Include(c => c.FormVersion)
            .Include(c => c.TargetRules)
            .Include(c => c.Exclusions)
            .FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
            ?? throw new KeyNotFoundException("الحملة غير موجودة.");
        if (!await CanViewCampaignAsync(campaign, cancellationToken))
        {
            throw new KeyNotFoundException("الحملة غير موجودة.");
        }

        return campaign;
    }

    private async Task<FormCampaign> LoadTrackedAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await db.FormCampaigns
            .Include(c => c.FormDefinition)
            .Include(c => c.TargetRules)
            .Include(c => c.Exclusions)
            .FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
            ?? throw new KeyNotFoundException("الحملة غير موجودة.");
        if (!await CanViewCampaignAsync(campaign, cancellationToken))
        {
            throw new KeyNotFoundException("الحملة غير موجودة.");
        }

        return campaign;
    }

    private async Task<bool> CanViewCampaignAsync(FormCampaign campaign, CancellationToken cancellationToken)
    {
        if (!formScope.CanAccess(campaign.FormDefinition))
        {
            return false;
        }

        if (!await effectiveAccess.HasCapabilityAsync(campaign.FormDefinition, FormAccessCapability.View, cancellationToken))
        {
            return false;
        }

        if (FormAccessHelper.RequiresSensitive(campaign.FormDefinition.Classification)
            && !FormAccessHelper.CanViewSensitive(currentUser)
            && !currentUser.HasPermission(PermissionCodes.FormsMonitorHeadquarters)
            && !currentUser.HasPermission(PermissionCodes.AuditView))
        {
            return false;
        }

        return currentUser.HasPermission(PermissionCodes.FormsView)
            || currentUser.HasPermission(PermissionCodes.FormsPublish)
            || currentUser.HasPermission(PermissionCodes.FormsManageCampaigns)
            || currentUser.HasPermission(PermissionCodes.FormsMonitorRegion)
            || currentUser.HasPermission(PermissionCodes.FormsMonitorHeadquarters)
            || currentUser.HasPermission(PermissionCodes.AuditView);
    }

    private void EnsureAnyViewPermission()
    {
        if (!(currentUser.HasPermission(PermissionCodes.FormsView)
            || currentUser.HasPermission(PermissionCodes.FormsPublish)
            || currentUser.HasPermission(PermissionCodes.FormsManageCampaigns)
            || currentUser.HasPermission(PermissionCodes.FormsMonitorRegion)
            || currentUser.HasPermission(PermissionCodes.FormsMonitorHeadquarters)
            || currentUser.HasPermission(PermissionCodes.AuditView)))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية عرض الحملات.");
        }
    }

    private void EnsurePreviewPermission()
    {
        if (!(currentUser.HasPermission(PermissionCodes.FormsPreviewTargets)
            || currentUser.HasPermission(PermissionCodes.FormsPublish)
            || currentUser.HasPermission(PermissionCodes.FormsManageCampaigns)))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية معاينة الاستهداف.");
        }
    }

    private async Task<IReadOnlyList<string>> BuildAllowedActionsAsync(FormCampaign campaign, CancellationToken cancellationToken)
    {
        var actions = new List<string> { "view" };
        if (campaign.Status == FormCampaignStatus.Draft && currentUser.HasPermission(PermissionCodes.FormsManageCampaigns))
        {
            actions.Add("edit");
            actions.Add("preview");
        }

        if (campaign.Status == FormCampaignStatus.Draft
            && currentUser.HasPermission(PermissionCodes.FormsPublish)
            && await effectiveAccess.HasCapabilityAsync(campaign.FormDefinition, FormAccessCapability.Publish, cancellationToken))
        {
            actions.Add("publish");
        }

        if (FormCampaignStateMachine.CanTransition(campaign.Status, FormCampaignStatus.Paused)
            && currentUser.HasPermission(PermissionCodes.FormsPauseCampaign))
        {
            actions.Add("pause");
        }

        if (campaign.Status == FormCampaignStatus.Paused && currentUser.HasPermission(PermissionCodes.FormsPauseCampaign))
        {
            actions.Add("resume");
        }

        if (FormCampaignStateMachine.CanTransition(campaign.Status, FormCampaignStatus.Cancelled)
            && currentUser.HasPermission(PermissionCodes.FormsCancelCampaign))
        {
            actions.Add("cancel");
        }

        if (FormCampaignStateMachine.CanTransition(campaign.Status, FormCampaignStatus.Completed)
            && currentUser.HasPermission(PermissionCodes.FormsPublish))
        {
            actions.Add("complete");
        }

        if (currentUser.HasPermission(PermissionCodes.FormsManageCampaigns))
        {
            actions.Add("clone");
        }

        if (currentUser.HasPermission(PermissionCodes.FormsViewCampaignAssignments))
        {
            actions.Add("viewAssignments");
        }

        return actions;
    }

    private async Task<FormCampaignDetailDto> MapDetailAsync(FormCampaign campaign, CancellationToken cancellationToken)
    {
        var cycleCount = await db.FormCycles.CountAsync(c => c.CampaignId == campaign.Id, cancellationToken);
        var exclusionFacilities = campaign.Exclusions.Select(e => e.FacilityId).ToList();
        var facilityLookup = await db.Facilities.AsNoTracking()
            .Where(f => exclusionFacilities.Contains(f.Id))
            .Select(f => new { f.Id, f.Code, f.NameAr })
            .ToDictionaryAsync(f => f.Id, cancellationToken);

        return new FormCampaignDetailDto(
            campaign.Id,
            campaign.OrganizationId,
            campaign.FormDefinitionId,
            campaign.FormDefinition.Code,
            campaign.FormDefinition.NameAr,
            campaign.FormVersionId,
            campaign.FormVersion.VersionNumber,
            campaign.FormSchemaSnapshotId,
            campaign.SchemaHash,
            campaign.Code,
            campaign.NameAr,
            campaign.NameEn,
            campaign.Description,
            campaign.Status,
            campaign.Priority,
            campaign.TimeZoneId,
            campaign.RecurrenceKind,
            MapSchedule(campaign),
            campaign.TargetRules.Select(r => FormTargetResolver.DeserializeTarget(r.RuleType, r.ConfigurationJson)).ToList(),
            campaign.Exclusions.Select(e => new FormCampaignExclusionDto(
                e.FacilityId,
                facilityLookup.TryGetValue(e.FacilityId, out var f) ? f.Code : e.FacilityId.ToString(),
                facilityLookup.TryGetValue(e.FacilityId, out var f2) ? f2.NameAr : string.Empty,
                e.Reason)).ToList(),
            campaign.FirstOpenAtLocal,
            campaign.NextOccurrenceUtc,
            campaign.LastGeneratedOccurrenceUtc,
            campaign.PublishedAtUtc,
            campaign.PausedAtUtc,
            campaign.PauseReason,
            campaign.CancelledAtUtc,
            campaign.CancellationReason,
            campaign.ClosedAtUtc,
            campaign.CreatedAtUtc,
            cycleCount,
            await BuildAllowedActionsAsync(campaign, cancellationToken),
            Convert.ToBase64String(campaign.RowVersion));
    }

    private FormCampaignScheduleRequest MapSchedule(FormCampaign campaign) =>
        recurrence.DeserializeSchedule(
            campaign.RecurrenceKind,
            campaign.RecurrenceConfigurationJson,
            campaign.FirstOpenAtLocal,
            campaign.ResponseWindowMinutes,
            campaign.GracePeriodMinutes,
            campaign.CloseAfterMinutes,
            campaign.BusinessDayAdjustment);

    private static void ValidateSchedule(FormCampaignScheduleRequest schedule)
    {
        if (schedule.ResponseWindowMinutes <= 0)
        {
            throw new InvalidOperationException("نافذة الاستجابة يجب أن تكون أكبر من صفر.");
        }

        if (schedule.GracePeriodMinutes < 0 || schedule.CloseAfterMinutes < 0)
        {
            throw new InvalidOperationException("المهل الزمنية غير صالحة.");
        }

        if (schedule.UntilLocal is { } until && until < schedule.FirstOpenAtLocal)
        {
            throw new InvalidOperationException("تاريخ النهاية يجب أن يكون بعد البداية.");
        }

        if (schedule.MaxOccurrences is { } max && (max < 1 || max > FormRecurrenceCalculator.MaxOccurrences))
        {
            throw new InvalidOperationException("عدد مرات التكرار خارج الحد المسموح.");
        }

        if (schedule.RecurrenceKind == FormRecurrenceKind.CustomDates)
        {
            var dates = schedule.CustomDatesLocal ?? [];
            if (dates.Count == 0)
            {
                throw new InvalidOperationException("قائمة التواريخ المخصصة فارغة.");
            }

            var distinctCount = dates.Distinct().Count();
            // Limit applies to distinct dates after Distinct (duplicates in the raw list are allowed).
            if (distinctCount > FormRecurrenceCalculator.MaxCustomDates)
            {
                throw new InvalidOperationException(
                    $"عدد التواريخ المخصصة يتجاوز الحد الأقصى ({FormRecurrenceCalculator.MaxCustomDates}).");
            }
        }
    }

    private async Task<DateTimeOffset?> ResolveNextOccurrenceForResumeAsync(
        FormCampaign campaign,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var schedule = MapSchedule(campaign);

        if (schedule.RecurrenceKind == FormRecurrenceKind.Once)
        {
            var hasCycle = await db.FormCycles.AsNoTracking()
                .AnyAsync(c => c.CampaignId == campaign.Id, cancellationToken);
            return hasCycle ? null : timeZones.ToUtc(schedule.FirstOpenAtLocal, campaign.TimeZoneId);
        }

        var cursorLocal = campaign.LastGeneratedOccurrenceUtc is { } lastUtc
            ? timeZones.ToLocal(lastUtc, campaign.TimeZoneId).AddMinutes(1)
            : schedule.FirstOpenAtLocal;
        var upcoming = recurrence.EnumerateUpcoming(schedule, cursorLocal, 1);
        return upcoming.Count == 0 ? null : timeZones.ToUtc(upcoming[0], campaign.TimeZoneId);
    }

    private static void ValidateTargets(
        IReadOnlyList<FormCampaignTargetRequest> targets,
        IReadOnlyList<FormCampaignExclusionRequest>? exclusions)
    {
        if (targets is null || targets.Count == 0)
        {
            throw new InvalidOperationException("يجب تحديد قاعدة استهداف واحدة على الأقل.");
        }

        foreach (var exclusion in exclusions ?? [])
        {
            if (string.IsNullOrWhiteSpace(exclusion.Reason))
            {
                throw new InvalidOperationException("سبب الاستثناء مطلوب.");
            }
        }
    }
}
