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
    IFormCampaignAccessCoordinator access,
    IFormCampaignScheduleCoordinator schedule,
    IFormTargetResolver targetResolver,
    IAuditService audit) : IFormCampaignService
{
    private const string CampaignNotFoundMessage = "الحملة غير موجودة.";

    public async Task<PagedResult<FormCampaignListItemDto>> ListAsync(
        PagedQuery query,
        FormCampaignStatus? status,
        Guid? formDefinitionId,
        CancellationToken cancellationToken = default)
    {
        access.EnsureAnyViewPermission();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = await BuildVisibleCampaignQueryAsync(status, formDefinitionId, query.Search, cancellationToken);
        var total = await q.CountAsync(cancellationToken);
        var items = await q.OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var countMap = await LoadCycleStatisticsAsync(items.Select(i => i.Id).ToList(), cancellationToken);
        var draftForms = items
            .Where(c => c.Status == FormCampaignStatus.Draft)
            .GroupBy(c => c.FormDefinitionId)
            .Select(g => g.First().FormDefinition)
            .ToList();
        var publishCapabilityByFormId = await access.ResolvePublishCapabilitiesAsync(draftForms, cancellationToken);
        var dtos = MapCampaignListItems(items, countMap, publishCapabilityByFormId);

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
        access.EnsurePermission(PermissionCodes.FormsManageCampaigns);
        ValidateSchedule(request.Schedule);
        ValidateTargets(request.Targets, request.Exclusions);

        var form = await access.LoadInScopeFormAsync(request.FormDefinitionId, cancellationToken);
        if (form.Status == FormDefinitionStatus.Archived)
        {
            throw new InvalidOperationException("لا يمكن إنشاء حملة لنموذج مؤرشف.");
        }

        await access.EnsureViewCapabilityAsync(form, cancellationToken);
        access.EnsureCanViewSensitiveForm(form);

        var version = await db.FormVersions.Include(v => v.Snapshot)
            .FirstOrDefaultAsync(v => v.Id == request.FormVersionId && v.FormDefinitionId == form.Id, cancellationToken)
            ?? throw new KeyNotFoundException("إصدار النموذج غير موجود.");
        if (version.Status != FormVersionStatus.Locked || version.SnapshotId is null || version.Snapshot is null)
        {
            throw new InvalidOperationException("يُسمح بالنشر فقط من إصدار مقفل مع لقطة.");
        }

        var orgId = await ResolveOrganizationIdAsync(form, cancellationToken);
        schedule.ValidateTimeZone(request.TimeZoneId);
        var userId = access.UserId ?? throw new UnauthorizedAccessException("المستخدم غير معروف.");
        var now = schedule.GetUtcNow();

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
            RecurrenceConfigurationJson = schedule.SerializeSchedule(request.Schedule),
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
        access.EnsurePermission(PermissionCodes.FormsManageCampaigns);
        ValidateSchedule(request.Schedule);
        ValidateTargets(request.Targets, request.Exclusions);

        var campaign = await LoadTrackedAsync(campaignId, cancellationToken);
        if (!FormCampaignStateMachine.IsMutable(campaign.Status))
        {
            throw new InvalidOperationException("لا يمكن تعديل الحملة بعد النشر. استخدم النسخ لمسودة جديدة.");
        }

        FormAccessHelper.EnsureRowVersion(campaign.RowVersion, request.RowVersion);
        var form = await access.LoadInScopeFormAsync(campaign.FormDefinitionId, cancellationToken);
        await access.EnsureViewCapabilityAsync(form, cancellationToken);

        var userId = access.UserId ?? throw new UnauthorizedAccessException("المستخدم غير معروف.");
        var now = schedule.GetUtcNow();
        campaign.NameAr = request.NameAr.Trim();
        campaign.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim();
        campaign.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        campaign.Priority = request.Priority;
        campaign.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? FormTimeZoneResolver.DefaultTimeZoneId : request.TimeZoneId.Trim();
        campaign.RecurrenceKind = request.Schedule.RecurrenceKind;
        campaign.RecurrenceConfigurationJson = schedule.SerializeSchedule(request.Schedule);
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
        access.EnsurePermission(PermissionCodes.FormsManageCampaigns);
        var source = await LoadVisibleAsync(campaignId, cancellationToken);
        var mappedSchedule = schedule.MapSchedule(source);
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
            mappedSchedule,
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
        access.EnsurePreviewPermission();
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
        access.EnsurePreviewPermission();
        var resolution = await targetResolver.ResolveAsync(organizationId, targets, exclusions ?? [], cancellationToken);
        var sample = resolution.Included.Take(50).Select(x => new FormTargetPreviewFacilityDto(
            x.FacilityId, x.FacilityCode, x.FacilityNameAr, x.RegionId, x.RegionNameAr, x.FacilityType)).ToList();
        return new FormTargetPreviewDto(
            schedule.GetUtcNow(),
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
        access.EnsurePermission(PermissionCodes.FormsPublish);
        var campaign = await LoadTrackedAsync(campaignId, cancellationToken);
        FormAccessHelper.EnsureRowVersion(campaign.RowVersion, request.RowVersion);
        if (campaign.Status != FormCampaignStatus.Draft)
        {
            throw new InvalidOperationException("يمكن نشر المسودات فقط.");
        }

        var form = await access.LoadInScopeFormAsync(campaign.FormDefinitionId, cancellationToken);
        if (form.Status == FormDefinitionStatus.Archived)
        {
            throw new InvalidOperationException("لا يمكن نشر حملة لنموذج مؤرشف.");
        }

        await access.EnsurePublishCapabilityAsync(form, cancellationToken);
        var version = await db.FormVersions.Include(v => v.Snapshot)
            .FirstAsync(v => v.Id == campaign.FormVersionId, cancellationToken);
        if (version.Status != FormVersionStatus.Locked || version.Snapshot is null)
        {
            throw new InvalidOperationException("يُسمح بالنشر فقط من إصدار مقفل مع لقطة.");
        }

        campaign.FormSchemaSnapshotId = version.Snapshot.Id;
        campaign.SchemaHash = version.Snapshot.SchemaHash;

        var mappedSchedule = schedule.MapSchedule(campaign);
        var firstLocal = mappedSchedule.FirstOpenAtLocal;
        var firstUtc = schedule.ToUtc(firstLocal, campaign.TimeZoneId);
        var now = schedule.GetUtcNow();
        var userId = access.UserId ?? throw new UnauthorizedAccessException("المستخدم غير معروف.");

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
            await schedule.GenerateOccurrenceAsync(campaign, firstLocal, access.DisplayName ?? "publisher", cancellationToken);
            var next = schedule.ComputeNextAfter(mappedSchedule, firstLocal, campaign.TimeZoneId);
            campaign.LastGeneratedOccurrenceUtc = firstUtc;
            campaign.NextOccurrenceUtc = campaign.RecurrenceKind == FormRecurrenceKind.Once ? null : next;
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
            access.EnsurePermission(PermissionCodes.FormsPauseCampaign);
            var campaign = await LoadTrackedAsync(campaignId, ct);
            FormAccessHelper.EnsureRowVersion(campaign.RowVersion, request.RowVersion);

            if (campaign.Status is FormCampaignStatus.Completed or FormCampaignStatus.Cancelled)
            {
                throw new InvalidOperationException("لا يمكن استئناف حملة مكتملة أو ملغاة.");
            }

            var now = schedule.GetUtcNow();
            var next = await schedule.ResolveNextOccurrenceForResumeAsync(campaign, ct);
            campaign.PausedAtUtc = null;
            campaign.PausedByUserId = null;
            campaign.PauseReason = null;
            campaign.UpdatedByUserId = access.UserId;
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
        access.EnsurePermission(PermissionCodes.FormsViewCampaignAssignments);
        _ = await LoadVisibleAsync(campaignId, cancellationToken);
        _ = await db.FormCycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cycleId && c.CampaignId == campaignId, cancellationToken)
            ?? throw new KeyNotFoundException("الدورة غير موجودة.");

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var scopedFacilityIds = await access.FilterFacilities(db.Facilities.AsNoTracking())
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

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
        access.EnsurePreviewPermission();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var regions = access.FilterRegions(db.Regions.AsNoTracking().Where(r => r.IsActive));
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
        access.EnsurePreviewPermission();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var facilities = access.FilterFacilities(db.Facilities.AsNoTracking().Where(f => f.IsActive));
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
        FormCampaignScheduleRequest scheduleRequest,
        string? timeZoneId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateSchedule(scheduleRequest);
        schedule.ValidateTimeZone(timeZoneId);
        return Task.FromResult(schedule.PreviewUpcoming(scheduleRequest, count));
    }

    private async Task<IQueryable<FormCampaign>> BuildVisibleCampaignQueryAsync(
        FormCampaignStatus? status,
        Guid? formDefinitionId,
        string? search,
        CancellationToken cancellationToken)
    {
        var scopedForms = await access.FilterScopedFormsAsync(cancellationToken);
        var scopedFormIds = scopedForms.Select(f => f.Id);

        var q = db.FormCampaigns.AsNoTracking()
            .Include(c => c.FormDefinition)
            .Include(c => c.FormVersion)
            .Where(c => scopedFormIds.Contains(c.FormDefinitionId));

        q = ApplyCampaignFilters(q, status, formDefinitionId, search);

        if (!access.CanViewSensitiveViaRole)
        {
            q = q.Where(c => c.FormDefinition.Classification < ClassificationLevel.Confidential);
        }

        var allowedFormIds = await ResolveViewableFormIdsAsync(q, cancellationToken);
        return q.Where(c => allowedFormIds.Contains(c.FormDefinitionId));
    }

    private static IQueryable<FormCampaign> ApplyCampaignFilters(
        IQueryable<FormCampaign> query,
        FormCampaignStatus? status,
        Guid? formDefinitionId,
        string? search)
    {
        if (status is not null)
        {
            query = query.Where(c => c.Status == status);
        }

        if (formDefinitionId is not null)
        {
            query = query.Where(c => c.FormDefinitionId == formDefinitionId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Code.Contains(term)
                || c.NameAr.Contains(term)
                || (c.NameEn != null && c.NameEn.Contains(term)));
        }

        return query;
    }

    private async Task<HashSet<Guid>> ResolveViewableFormIdsAsync(
        IQueryable<FormCampaign> query,
        CancellationToken cancellationToken)
    {
        var candidateFormIds = await query.Select(c => c.FormDefinitionId).Distinct().ToListAsync(cancellationToken);
        var viewCapabilities = await access.ResolveViewCapabilitiesAsync(candidateFormIds, cancellationToken);
        return viewCapabilities.Where(x => x.Value).Select(x => x.Key).ToHashSet();
    }

    private async Task<Dictionary<Guid, (int Count, DateTimeOffset? Last)>> LoadCycleStatisticsAsync(
        IReadOnlyList<Guid> campaignIds,
        CancellationToken cancellationToken)
    {
        if (campaignIds.Count == 0)
        {
            return [];
        }

        var cycleCounts = await db.FormCycles.AsNoTracking()
            .Where(c => campaignIds.Contains(c.CampaignId))
            .GroupBy(c => c.CampaignId)
            .Select(g => new { CampaignId = g.Key, Count = g.Count(), Last = g.Max(x => (DateTimeOffset?)x.GeneratedAtUtc) })
            .ToListAsync(cancellationToken);
        return cycleCounts.ToDictionary(x => x.CampaignId, x => (x.Count, x.Last));
    }

    private List<FormCampaignListItemDto> MapCampaignListItems(
        IReadOnlyList<FormCampaign> items,
        IReadOnlyDictionary<Guid, (int Count, DateTimeOffset? Last)> countMap,
        IReadOnlyDictionary<Guid, bool> publishCapabilityByFormId)
    {
        var dtos = new List<FormCampaignListItemDto>(items.Count);
        foreach (var c in items)
        {
            countMap.TryGetValue(c.Id, out var stats);
            dtos.Add(new FormCampaignListItemDto(
                c.Id, c.Code, c.NameAr, c.NameEn, c.FormDefinitionId, c.FormDefinition.Code, c.FormDefinition.NameAr,
                c.FormVersionId, c.FormVersion.VersionNumber, c.Status, c.RecurrenceKind, c.FirstOpenAtLocal,
                c.NextOccurrenceUtc, stats.Count, stats.Last,
                BuildAllowedActions(c, publishCapabilityByFormId),
                Convert.ToBase64String(c.RowVersion)));
        }

        return dtos;
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
        access.EnsurePermission(permission);
        if (requireReason && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("السبب مطلوب.");
        }

        var campaign = await LoadTrackedAsync(campaignId, cancellationToken);
        FormAccessHelper.EnsureRowVersion(campaign.RowVersion, request.RowVersion);
        FormCampaignStateMachine.EnsureCanTransition(campaign.Status, to);
        var now = schedule.GetUtcNow();
        var userId = access.UserId;
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
            ?? throw new KeyNotFoundException(CampaignNotFoundMessage);
        if (!await access.CanViewCampaignAsync(campaign, cancellationToken))
        {
            throw new KeyNotFoundException(CampaignNotFoundMessage);
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
            ?? throw new KeyNotFoundException(CampaignNotFoundMessage);
        if (!await access.CanViewCampaignAsync(campaign, cancellationToken))
        {
            throw new KeyNotFoundException(CampaignNotFoundMessage);
        }

        return campaign;
    }

    private IReadOnlyList<string> BuildAllowedActions(
        FormCampaign campaign,
        IReadOnlyDictionary<Guid, bool>? publishCapabilityByFormId = null)
    {
        var hasPublishCapabilityOnForm = false;
        if (campaign.Status == FormCampaignStatus.Draft && access.HasPermission(PermissionCodes.FormsPublish))
        {
            hasPublishCapabilityOnForm = publishCapabilityByFormId?.GetValueOrDefault(campaign.FormDefinitionId) ?? false;
        }

        return FormCampaignAllowedActions.Build(
            campaign.Status,
            access.HasPermission(PermissionCodes.FormsManageCampaigns),
            access.HasPermission(PermissionCodes.FormsPublish),
            hasPublishCapabilityOnForm,
            access.HasPermission(PermissionCodes.FormsPauseCampaign),
            access.HasPermission(PermissionCodes.FormsCancelCampaign),
            access.HasPermission(PermissionCodes.FormsViewCampaignAssignments));
    }

    private async Task<IReadOnlyList<string>> BuildAllowedActionsAsync(
        FormCampaign campaign,
        CancellationToken cancellationToken)
    {
        var hasPublishCapabilityOnForm = false;
        if (campaign.Status == FormCampaignStatus.Draft && access.HasPermission(PermissionCodes.FormsPublish))
        {
            hasPublishCapabilityOnForm = await access.HasPublishCapabilityAsync(campaign.FormDefinition, cancellationToken);
        }

        return FormCampaignAllowedActions.Build(
            campaign.Status,
            access.HasPermission(PermissionCodes.FormsManageCampaigns),
            access.HasPermission(PermissionCodes.FormsPublish),
            hasPublishCapabilityOnForm,
            access.HasPermission(PermissionCodes.FormsPauseCampaign),
            access.HasPermission(PermissionCodes.FormsCancelCampaign),
            access.HasPermission(PermissionCodes.FormsViewCampaignAssignments));
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
            schedule.MapSchedule(campaign),
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

    private static void ValidateSchedule(FormCampaignScheduleRequest scheduleRequest)
    {
        if (scheduleRequest.ResponseWindowMinutes <= 0)
        {
            throw new InvalidOperationException("نافذة الاستجابة يجب أن تكون أكبر من صفر.");
        }

        if (scheduleRequest.GracePeriodMinutes < 0 || scheduleRequest.CloseAfterMinutes < 0)
        {
            throw new InvalidOperationException("المهل الزمنية غير صالحة.");
        }

        if (scheduleRequest.UntilLocal is { } until && until < scheduleRequest.FirstOpenAtLocal)
        {
            throw new InvalidOperationException("تاريخ النهاية يجب أن يكون بعد البداية.");
        }

        if (scheduleRequest.MaxOccurrences is { } max && (max < 1 || max > FormRecurrenceCalculator.MaxOccurrences))
        {
            throw new InvalidOperationException("عدد مرات التكرار خارج الحد المسموح.");
        }

        if (scheduleRequest.RecurrenceKind == FormRecurrenceKind.CustomDates)
        {
            var dates = scheduleRequest.CustomDatesLocal ?? [];
            if (dates.Count == 0)
            {
                throw new InvalidOperationException("قائمة التواريخ المخصصة فارغة.");
            }

            var distinctCount = dates.Distinct().Count();
            if (distinctCount > FormRecurrenceCalculator.MaxCustomDates)
            {
                throw new InvalidOperationException(
                    $"عدد التواريخ المخصصة يتجاوز الحد الأقصى ({FormRecurrenceCalculator.MaxCustomDates}).");
            }
        }
    }

    private static void ValidateTargets(
        IReadOnlyList<FormCampaignTargetRequest> targets,
        IReadOnlyList<FormCampaignExclusionRequest>? exclusions)
    {
        if (targets is null || targets.Count == 0)
        {
            throw new InvalidOperationException("يجب تحديد قاعدة استهداف واحدة على الأقل.");
        }

        if ((exclusions ?? []).Any(e => string.IsNullOrWhiteSpace(e.Reason)))
        {
            throw new InvalidOperationException("سبب الاستثناء مطلوب.");
        }
    }
}
