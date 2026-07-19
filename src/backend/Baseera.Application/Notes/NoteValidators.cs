namespace Baseera.Application.Notes;

using FluentValidation;

public sealed class CreateNoteRequestValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteRequestValidator()
    {
        RuleFor(x => x.Title).Must(BeMeaningful).WithMessage("العنوان مطلوب.").MaximumLength(300);
        RuleFor(x => x.Description).Must(BeMeaningful).WithMessage("الوصف مطلوب.").MaximumLength(8000);
        RuleFor(x => x.Category).IsInEnum().WithMessage("التصنيف غير صالح.");
        RuleFor(x => x.Severity).IsInEnum().WithMessage("مستوى الخطورة غير صالح.");
        RuleFor(x => x.SourceType).IsInEnum().WithMessage("نوع المصدر غير صالح.");
        RuleFor(x => x.Classification).IsInEnum().WithMessage("مستوى التصنيف غير صالح.");
        RuleFor(x => x.ScopeType).IsInEnum().WithMessage("نوع النطاق غير صالح.");
        RuleFor(x => x.SourceReference).MaximumLength(200).When(x => x.SourceReference is not null);
        RuleFor(x => x.DueAtUtc)
            .Must(d => d is null || d.Value > DateTimeOffset.UtcNow.AddMinutes(-1))
            .WithMessage("تاريخ الاستحقاق يجب أن يكون في المستقبل.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class UpdateNoteRequestValidator : AbstractValidator<UpdateNoteRequest>
{
    public UpdateNoteRequestValidator()
    {
        RuleFor(x => x.Title).Must(BeMeaningful).WithMessage("العنوان مطلوب.").MaximumLength(300);
        RuleFor(x => x.Description).Must(BeMeaningful).WithMessage("الوصف مطلوب.").MaximumLength(8000);
        RuleFor(x => x.Category).IsInEnum().WithMessage("التصنيف غير صالح.");
        RuleFor(x => x.Severity).IsInEnum().WithMessage("مستوى الخطورة غير صالح.");
        RuleFor(x => x.SourceType).IsInEnum().WithMessage("نوع المصدر غير صالح.");
        RuleFor(x => x.Classification).IsInEnum().WithMessage("مستوى التصنيف غير صالح.");
        RuleFor(x => x.SourceReference).MaximumLength(200).When(x => x.SourceReference is not null);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
        RuleFor(x => x.DueAtUtc)
            .Must(d => d is null || d.Value > DateTimeOffset.UtcNow.AddYears(-5))
            .WithMessage("تاريخ الاستحقاق غير منطقي.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class AssignNoteRequestValidator : AbstractValidator<AssignNoteRequest>
{
    public AssignNoteRequestValidator()
    {
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب التكليف مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
        RuleFor(x => x)
            .Must(x => x.AssignedToUserId.HasValue ^ x.AssignedToDepartmentId.HasValue)
            .WithMessage("يجب تحديد مستخدم أو إدارة واحدة فقط للتكليف.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class WorkflowActionRequestValidator : AbstractValidator<WorkflowActionRequest>
{
    public WorkflowActionRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
        RuleFor(x => x.Reason).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class TransitionNoteRequestValidator : AbstractValidator<TransitionNoteRequest>
{
    public TransitionNoteRequestValidator()
    {
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("السبب مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class CloseNoteRequestValidator : AbstractValidator<CloseNoteRequest>
{
    public CloseNoteRequestValidator()
    {
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب الإغلاق مطلوب.").MaximumLength(2000);
        RuleFor(x => x.ClosureSummary).Must(BeMeaningful).WithMessage("ملخص الإغلاق مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class ReopenNoteRequestValidator : AbstractValidator<ReopenNoteRequest>
{
    public ReopenNoteRequestValidator()
    {
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب إعادة الفتح مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}
