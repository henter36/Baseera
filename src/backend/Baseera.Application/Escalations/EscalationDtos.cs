namespace Baseera.Application.Escalations;

using Baseera.Application.Common;
using Baseera.Domain.Common;
using Baseera.Domain.Escalations;
using FluentValidation;

public sealed record EscalationPolicyDto(
    Guid Id,
    string Code,
    string NameAr,
    string? Description,
    EscalationTargetType TargetType,
    bool IsEnabled,
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId,
    int RuleCount,
    string RowVersion);

public sealed record EscalationRuleDto(
    Guid Id,
    Guid EscalationPolicyId,
    int Level,
    int Priority,
    EscalationTriggerType TriggerType,
    int ThresholdDays,
    int? RepeatEveryDays,
    int? MaximumOccurrences,
    EscalationRecipientStrategy RecipientStrategy,
    string? RecipientRoleCode,
    Guid? SpecificRecipientUserId,
    string TitleTemplateAr,
    string MessageTemplateAr,
    bool IsEnabled,
    string RowVersion);

public sealed record EscalationOccurrenceDto(
    Guid Id,
    Guid PolicyId,
    Guid RuleId,
    EscalationTargetType TargetType,
    Guid TargetId,
    string TargetReferenceNumber,
    int EscalationLevel,
    EscalationTriggerType TriggerType,
    int OccurrenceNumber,
    DateTimeOffset DueAtUtc,
    DateTimeOffset DetectedAtUtc,
    int RecipientCount,
    EscalationOccurrenceStatus Status,
    string? SuppressionReason);

public sealed record NotificationDto(
    Guid Id,
    EscalationTargetType TargetType,
    Guid TargetId,
    string TargetReferenceNumber,
    string TitleAr,
    string MessageAr,
    int Priority,
    NotificationStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    string RowVersion);

public sealed record CreateEscalationPolicyRequest(
    string Code,
    string NameAr,
    string? Description,
    EscalationTargetType TargetType,
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId);

public sealed record UpdateEscalationPolicyRequest(
    string NameAr,
    string? Description,
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId,
    string RowVersion);

public sealed record CreateEscalationRuleRequest(
    int Level,
    int Priority,
    EscalationTriggerType TriggerType,
    int ThresholdDays,
    int? RepeatEveryDays,
    int? MaximumOccurrences,
    EscalationRecipientStrategy RecipientStrategy,
    string? RecipientRoleCode,
    Guid? SpecificRecipientUserId,
    string TitleTemplateAr,
    string MessageTemplateAr);

public sealed record UpdateEscalationRuleRequest(
    int Priority,
    EscalationTriggerType TriggerType,
    int ThresholdDays,
    int? RepeatEveryDays,
    int? MaximumOccurrences,
    EscalationRecipientStrategy RecipientStrategy,
    string? RecipientRoleCode,
    Guid? SpecificRecipientUserId,
    string TitleTemplateAr,
    string MessageTemplateAr,
    string RowVersion);

public sealed record RowVersionRequest(string RowVersion);

public sealed record EscalationRunResult(int PoliciesEvaluated, int CandidatesEvaluated, int OccurrencesCreated, int NotificationsCreated, int Suppressed, int Failed);

public sealed class EscalationPolicyQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
    public EscalationTargetType? TargetType { get; set; }
    public bool? IsEnabled { get; set; }
    public int Skip => PagingMath.SafeSkip(Page, PageSize);
    public int Take => Math.Clamp(PageSize, 1, 200);
}

public sealed class EscalationOccurrenceQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
    public EscalationTargetType? TargetType { get; set; }
    public EscalationOccurrenceStatus? Status { get; set; }
    public int Skip => PagingMath.SafeSkip(Page, PageSize);
    public int Take => Math.Clamp(PageSize, 1, 200);
}

public sealed class NotificationQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
    public NotificationStatus? Status { get; set; }
    public EscalationTargetType? TargetType { get; set; }
    public int? Priority { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
    public int Skip => PagingMath.SafeSkip(Page, PageSize);
    public int Take => Math.Clamp(PageSize, 1, 200);
}

internal static class PagingMath
{
    public static int SafeSkip(int page, int pageSize) =>
        (int)Math.Min(Math.Max((long)page - 1, 0) * Math.Clamp(pageSize, 1, 200), int.MaxValue);
}

public sealed class CreateEscalationPolicyRequestValidator : AbstractValidator<CreateEscalationPolicyRequest>
{
    public CreateEscalationPolicyRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100).Matches("^[A-Za-z0-9_.-]+$");
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.TargetType).IsInEnum();
        RuleFor(x => x.ScopeType).IsInEnum();
        RuleFor(x => x).Must(HaveValidScopeShape).WithMessage("نطاق السياسة غير متوافق مع نوع النطاق.");
    }

    internal static bool HaveValidScopeShape(CreateEscalationPolicyRequest request) =>
        ScopeShapeIsValid(request.ScopeType, request.RegionId, request.FacilityId, request.FacilityUnitId);

    internal static bool ScopeShapeIsValid(ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? unitId) => scopeType switch
    {
        ScopeType.Global or ScopeType.Headquarters => regionId is null && facilityId is null && unitId is null,
        ScopeType.Region => regionId.HasValue && facilityId is null && unitId is null,
        ScopeType.Facility => facilityId.HasValue && unitId is null,
        ScopeType.FacilityUnit => facilityId.HasValue && unitId.HasValue,
        _ => false
    };
}

public sealed class UpdateEscalationPolicyRequestValidator : AbstractValidator<UpdateEscalationPolicyRequest>
{
    public UpdateEscalationPolicyRequestValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.RowVersion).NotEmpty();
        RuleFor(x => x).Must(x => CreateEscalationPolicyRequestValidator.ScopeShapeIsValid(x.ScopeType, x.RegionId, x.FacilityId, x.FacilityUnitId))
            .WithMessage("نطاق السياسة غير متوافق مع نوع النطاق.");
    }
}

public sealed class CreateEscalationRuleRequestValidator : AbstractValidator<CreateEscalationRuleRequest>
{
    public CreateEscalationRuleRequestValidator()
    {
        RuleFor(x => x.Level).GreaterThan(0);
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TriggerType).IsInEnum();
        RuleFor(x => x.ThresholdDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RepeatEveryDays).GreaterThan(0).When(x => x.RepeatEveryDays.HasValue);
        RuleFor(x => x.MaximumOccurrences).GreaterThan(0).When(x => x.MaximumOccurrences.HasValue);
        RuleFor(x => x.RecipientStrategy).IsInEnum();
        RuleFor(x => x.RecipientRoleCode).NotEmpty().When(x => x.RecipientStrategy == EscalationRecipientStrategy.SpecificRoleInTargetScope);
        RuleFor(x => x.SpecificRecipientUserId).NotNull().When(x => x.RecipientStrategy == EscalationRecipientStrategy.SpecificUser);
        RuleFor(x => x.TitleTemplateAr).NotEmpty().MaximumLength(300);
        RuleFor(x => x.MessageTemplateAr).NotEmpty().MaximumLength(1200);
    }
}

public sealed class UpdateEscalationRuleRequestValidator : AbstractValidator<UpdateEscalationRuleRequest>
{
    public UpdateEscalationRuleRequestValidator()
    {
        RuleFor(x => x.RowVersion).NotEmpty();
        RuleFor(x => new CreateEscalationRuleRequest(
            1,
            x.Priority,
            x.TriggerType,
            x.ThresholdDays,
            x.RepeatEveryDays,
            x.MaximumOccurrences,
            x.RecipientStrategy,
            x.RecipientRoleCode,
            x.SpecificRecipientUserId,
            x.TitleTemplateAr,
            x.MessageTemplateAr)).SetValidator(new CreateEscalationRuleRequestValidator());
    }
}
