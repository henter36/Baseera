namespace Baseera.Application.Forms.Campaigns;

using Baseera.Domain.Forms;

public sealed record FormCampaignListItemDto(
    Guid Id,
    string Code,
    string NameAr,
    string? NameEn,
    Guid FormDefinitionId,
    string FormCode,
    string FormNameAr,
    Guid FormVersionId,
    int VersionNumber,
    FormCampaignStatus Status,
    FormRecurrenceKind RecurrenceKind,
    DateTimeOffset FirstOpenAtLocal,
    DateTimeOffset? NextOccurrenceUtc,
    int CycleCount,
    DateTimeOffset? LastCycleAtUtc,
    IReadOnlyList<string> AllowedActions,
    string RowVersion);

public sealed record FormCampaignDetailDto(
    Guid Id,
    Guid OrganizationId,
    Guid FormDefinitionId,
    string FormCode,
    string FormNameAr,
    Guid FormVersionId,
    int VersionNumber,
    Guid FormSchemaSnapshotId,
    string SchemaHash,
    string Code,
    string NameAr,
    string? NameEn,
    string? Description,
    FormCampaignStatus Status,
    FormCampaignPriority Priority,
    string TimeZoneId,
    FormRecurrenceKind RecurrenceKind,
    FormCampaignScheduleRequest Schedule,
    IReadOnlyList<FormCampaignTargetRequest> Targets,
    IReadOnlyList<FormCampaignExclusionDto> Exclusions,
    DateTimeOffset FirstOpenAtLocal,
    DateTimeOffset? NextOccurrenceUtc,
    DateTimeOffset? LastGeneratedOccurrenceUtc,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? PausedAtUtc,
    string? PauseReason,
    DateTimeOffset? CancelledAtUtc,
    string? CancellationReason,
    DateTimeOffset? ClosedAtUtc,
    DateTimeOffset CreatedAtUtc,
    int CycleCount,
    IReadOnlyList<string> AllowedActions,
    string RowVersion);

public sealed record FormCampaignExclusionDto(
    Guid FacilityId,
    string FacilityCode,
    string FacilityNameAr,
    string Reason);

public sealed record CreateFormCampaignRequest(
    Guid FormDefinitionId,
    Guid FormVersionId,
    string Code,
    string NameAr,
    string? NameEn,
    string? Description,
    FormCampaignPriority Priority,
    string? TimeZoneId,
    FormCampaignScheduleRequest Schedule,
    IReadOnlyList<FormCampaignTargetRequest> Targets,
    IReadOnlyList<FormCampaignExclusionRequest>? Exclusions);

public sealed record UpdateFormCampaignRequest(
    string NameAr,
    string? NameEn,
    string? Description,
    FormCampaignPriority Priority,
    string? TimeZoneId,
    FormCampaignScheduleRequest Schedule,
    IReadOnlyList<FormCampaignTargetRequest> Targets,
    IReadOnlyList<FormCampaignExclusionRequest>? Exclusions,
    string RowVersion);

public sealed record FormCampaignTargetRequest(
    FormTargetRuleType RuleType,
    IReadOnlyList<Guid>? RegionIds,
    IReadOnlyList<Guid>? FacilityIds,
    DynamicCriteriaRequest? DynamicCriteria);

public sealed record DynamicCriteriaRequest(
    IReadOnlyList<Guid>? RegionIds,
    IReadOnlyList<string>? FacilityTypes,
    bool? IsActive);

public sealed record FormCampaignExclusionRequest(Guid FacilityId, string Reason);

public sealed record FormCampaignScheduleRequest(
    FormRecurrenceKind RecurrenceKind,
    DateTimeOffset FirstOpenAtLocal,
    int ResponseWindowMinutes,
    int GracePeriodMinutes,
    int CloseAfterMinutes,
    BusinessDayAdjustment BusinessDayAdjustment,
    int? IntervalDays,
    int? IntervalWeeks,
    IReadOnlyList<DayOfWeek>? WeekDays,
    int? DayOfMonth,
    MonthlyMissingDayPolicy? MissingDayPolicy,
    DateTimeOffset? UntilLocal,
    int? MaxOccurrences,
    IReadOnlyList<DateTimeOffset>? CustomDatesLocal);

public sealed record PublishFormCampaignRequest(string RowVersion);

public sealed record FormCampaignTransitionRequest(string RowVersion, string? Reason);

public sealed record FormTargetPreviewDto(
    DateTimeOffset AsOfUtc,
    int TotalMatched,
    int TotalExcluded,
    int FinalTargetCount,
    IReadOnlyDictionary<string, int> BreakdownByRegion,
    IReadOnlyDictionary<string, int> BreakdownByFacilityType,
    IReadOnlyList<Guid> IncludedFacilityIds,
    IReadOnlyList<FormTargetPreviewExclusionDto> Exclusions,
    IReadOnlyList<FormTargetPreviewFacilityDto> Sample,
    string TargetingFingerprint,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> InvalidTargets,
    IReadOnlyList<string> UnavailableFacilities);

public sealed record FormTargetPreviewExclusionDto(Guid FacilityId, string Reason);

public sealed record FormTargetPreviewFacilityDto(
    Guid FacilityId,
    string Code,
    string NameAr,
    Guid RegionId,
    string RegionNameAr,
    string? FacilityType);

public sealed record FormCycleListItemDto(
    Guid Id,
    int SequenceNumber,
    string OccurrenceKey,
    FormCycleStatus Status,
    DateTimeOffset ScheduledOccurrenceLocal,
    DateTimeOffset OpenAtUtc,
    DateTimeOffset DueAtUtc,
    DateTimeOffset CloseAtUtc,
    int AssignedFacilityCount,
    string TargetSnapshotHash);

public sealed record FormCycleDetailDto(
    Guid Id,
    Guid CampaignId,
    int SequenceNumber,
    string OccurrenceKey,
    FormCycleStatus Status,
    DateTimeOffset ScheduledOccurrenceLocal,
    DateTimeOffset ScheduledOccurrenceUtc,
    DateTimeOffset OpenAtUtc,
    DateTimeOffset DueAtUtc,
    DateTimeOffset GraceEndsAtUtc,
    DateTimeOffset CloseAtUtc,
    string TimeZoneId,
    Guid FormVersionId,
    Guid FormSchemaSnapshotId,
    string SchemaHash,
    string TargetSnapshotHash,
    int AssignedFacilityCount,
    DateTimeOffset GeneratedAtUtc,
    string GeneratedBy);

public sealed record FacilityAssignmentDto(
    Guid Id,
    Guid FacilityId,
    Guid RegionIdAtAssignment,
    string FacilityCodeAtAssignment,
    string FacilityNameArAtAssignment,
    string RegionNameArAtAssignment,
    string? FacilityTypeAtAssignment,
    FormTargetRuleType TargetRuleType,
    DateTimeOffset AssignedAtUtc,
    bool IsAvailable,
    string? UnavailableReason);

public sealed record FormCampaignSchedulerRunResult(
    int DueCampaigns,
    int CyclesCreated,
    int AssignmentsCreated,
    int DuplicatesSkipped,
    int CyclesStatusUpdated,
    int Failures,
    bool CatchUpLimitReached,
    TimeSpan Duration);

public sealed record FormCampaignSchedulerOptions(
    int BatchSize,
    int MaxCatchUpOccurrencesPerRun,
    int MaximumAttempts,
    int RetryBaseSeconds);

public sealed record ResolvedFacilityTarget(
    Guid FacilityId,
    Guid RegionId,
    string FacilityCode,
    string FacilityNameAr,
    string RegionNameAr,
    string? FacilityType,
    FormTargetRuleType MatchedRuleType,
    bool IsAvailable,
    string? UnavailableReason);

public sealed record FormTargetResolutionResult(
    IReadOnlyList<ResolvedFacilityTarget> Included,
    IReadOnlyList<(Guid FacilityId, string Reason)> Excluded,
    IReadOnlyDictionary<string, int> BreakdownByRegion,
    IReadOnlyDictionary<string, int> BreakdownByFacilityType,
    string TargetingFingerprint,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> InvalidTargets);
