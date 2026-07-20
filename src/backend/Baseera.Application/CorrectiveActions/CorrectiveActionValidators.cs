namespace Baseera.Application.CorrectiveActions;

using FluentValidation;

public sealed class CreateCorrectiveActionRequestValidator : AbstractValidator<CreateCorrectiveActionRequest>
{
    public CreateCorrectiveActionRequestValidator()
    {
        RuleFor(x => x.Title).Must(BeMeaningful).WithMessage("العنوان مطلوب.").MaximumLength(300);
        RuleFor(x => x.Description).Must(BeMeaningful).WithMessage("الوصف مطلوب.").MaximumLength(8000);
        RuleFor(x => x.Priority).IsInEnum().WithMessage("الأولوية غير صالحة.");
        RuleFor(x => x.Classification).IsInEnum().When(x => x.Classification.HasValue).WithMessage("مستوى التصنيف غير صالح.");
        RuleFor(x => x.DueAtUtc)
            .Must(d => d is null || d.Value > DateTimeOffset.UtcNow.AddMinutes(-1))
            .WithMessage("تاريخ الاستحقاق يجب أن يكون في المستقبل.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class UpdateCorrectiveActionRequestValidator : AbstractValidator<UpdateCorrectiveActionRequest>
{
    public UpdateCorrectiveActionRequestValidator()
    {
        RuleFor(x => x.Title).Must(BeMeaningful).WithMessage("العنوان مطلوب.").MaximumLength(300);
        RuleFor(x => x.Description).Must(BeMeaningful).WithMessage("الوصف مطلوب.").MaximumLength(8000);
        RuleFor(x => x.Priority).IsInEnum().WithMessage("الأولوية غير صالحة.");
        RuleFor(x => x.Classification).IsInEnum().WithMessage("مستوى التصنيف غير صالح.");
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class AssignCorrectiveActionRequestValidator : AbstractValidator<AssignCorrectiveActionRequest>
{
    public AssignCorrectiveActionRequestValidator()
    {
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب التكليف مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
        RuleFor(x => x)
            .Must(x => x.AssignedToUserId.HasValue ^ x.AssignedToDepartmentId.HasValue)
            .WithMessage("يجب تحديد مستخدم أو إدارة واحدة فقط للتكليف.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class TransitionCorrectiveActionRequestValidator : AbstractValidator<TransitionCorrectiveActionRequest>
{
    public TransitionCorrectiveActionRequestValidator()
    {
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("السبب مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class CompleteCorrectiveActionRequestValidator : AbstractValidator<CompleteCorrectiveActionRequest>
{
    public CompleteCorrectiveActionRequestValidator()
    {
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب الاعتماد مطلوب.").MaximumLength(2000);
        RuleFor(x => x.CompletionSummary).Must(BeMeaningful).WithMessage("ملخص الإنجاز مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class ReopenCorrectiveActionRequestValidator : AbstractValidator<ReopenCorrectiveActionRequest>
{
    public ReopenCorrectiveActionRequestValidator()
    {
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب إعادة الفتح مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}
