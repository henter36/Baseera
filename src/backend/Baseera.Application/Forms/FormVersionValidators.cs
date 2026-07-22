namespace Baseera.Application.Forms;

using FluentValidation;

public sealed class SaveFormSchemaRequestValidator : AbstractValidator<SaveFormSchemaRequest>
{
    public SaveFormSchemaRequestValidator()
    {
        RuleFor(x => x.SchemaJson).NotEmpty().WithMessage("مخطط النموذج مطلوب.");
        RuleFor(x => x.RowVersion).NotEmpty().WithMessage("إصدار السجل مطلوب.");
    }
}

public sealed class FormVersionTransitionRequestValidator : AbstractValidator<FormVersionTransitionRequest>
{
    public FormVersionTransitionRequestValidator()
    {
        RuleFor(x => x.RowVersion).NotEmpty().WithMessage("إصدار السجل مطلوب.");
        RuleFor(x => x.Reason).MaximumLength(2000);
    }
}

public sealed class CreateFormTemplateRequestValidator : AbstractValidator<CreateFormTemplateRequest>
{
    public CreateFormTemplateRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(80);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(120);
    }
}

public sealed class CreateFormFromTemplateRequestValidator : AbstractValidator<CreateFormFromTemplateRequest>
{
    public CreateFormFromTemplateRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(80);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
    }
}
