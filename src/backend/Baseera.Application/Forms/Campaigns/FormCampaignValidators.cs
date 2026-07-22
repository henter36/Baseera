namespace Baseera.Application.Forms.Campaigns;

using FluentValidation;

public sealed class CreateFormCampaignRequestValidator : AbstractValidator<CreateFormCampaignRequest>
{
    public CreateFormCampaignRequestValidator()
    {
        RuleFor(x => x.FormDefinitionId).NotEmpty();
        RuleFor(x => x.FormVersionId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(80)
            .Matches("^[A-Za-z][A-Za-z0-9_-]*$").WithMessage("رمز الحملة غير صالح.");
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Schedule).NotNull().SetValidator(new FormCampaignScheduleRequestValidator());
        RuleFor(x => x.Targets).NotEmpty();
        RuleForEach(x => x.Exclusions!).ChildRules(e =>
        {
            e.RuleFor(x => x.FacilityId).NotEmpty();
            e.RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        });
    }
}

public sealed class UpdateFormCampaignRequestValidator : AbstractValidator<UpdateFormCampaignRequest>
{
    public UpdateFormCampaignRequestValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RowVersion).NotEmpty();
        RuleFor(x => x.Schedule).NotNull().SetValidator(new FormCampaignScheduleRequestValidator());
        RuleFor(x => x.Targets).NotEmpty();
    }
}

public sealed class FormCampaignScheduleRequestValidator : AbstractValidator<FormCampaignScheduleRequest>
{
    public FormCampaignScheduleRequestValidator()
    {
        RuleFor(x => x.ResponseWindowMinutes).GreaterThan(0);
        RuleFor(x => x.GracePeriodMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CloseAfterMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IntervalDays!).InclusiveBetween(1, FormRecurrenceCalculator.MaxIntervalDays)
            .When(x => x.IntervalDays.HasValue);
        RuleFor(x => x.IntervalWeeks!).InclusiveBetween(1, FormRecurrenceCalculator.MaxIntervalWeeks)
            .When(x => x.IntervalWeeks.HasValue);
        RuleFor(x => x.DayOfMonth!).InclusiveBetween(1, 31).When(x => x.DayOfMonth.HasValue);
        RuleFor(x => x.MaxOccurrences!).InclusiveBetween(1, FormRecurrenceCalculator.MaxOccurrences)
            .When(x => x.MaxOccurrences.HasValue);
        RuleFor(x => x.CustomDatesLocal!)
            .Must(d => d.Count <= FormRecurrenceCalculator.MaxCustomDates)
            .When(x => x.CustomDatesLocal is not null);
    }
}

public sealed class PublishFormCampaignRequestValidator : AbstractValidator<PublishFormCampaignRequest>
{
    public PublishFormCampaignRequestValidator() => RuleFor(x => x.RowVersion).NotEmpty();
}

public sealed class FormCampaignTransitionRequestValidator : AbstractValidator<FormCampaignTransitionRequest>
{
    public FormCampaignTransitionRequestValidator() => RuleFor(x => x.RowVersion).NotEmpty();
}
