namespace Baseera.Application.Forms;

using System.Text.RegularExpressions;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using FluentValidation;

/// <summary>
/// Shared validation helpers for Forms module requests.
/// </summary>
internal static class FormRequestValidation
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly Regex CodePattern = new(
        @"^[A-Za-z][A-Za-z0-9._-]{2,79}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    public static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);

    public static bool BeValidCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            return CodePattern.IsMatch(value.Trim());
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>Row version must be non-empty and a syntactically valid Base64 string.</summary>
    public static bool BeValidRowVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed class CreateFormRequestValidator : AbstractValidator<CreateFormRequest>
{
    public CreateFormRequestValidator()
    {
        RuleFor(x => x.Code)
            .Must(FormRequestValidation.BeMeaningful)
            .WithMessage("رمز النموذج مطلوب.")
            .MaximumLength(80)
            .Must(FormRequestValidation.BeValidCode)
            .WithMessage("رمز النموذج يجب أن يبدأ بحرف ويحتوي على أحرف كبيرة وأرقام أو . _ - فقط.");
        RuleFor(x => x.NameAr).Must(FormRequestValidation.BeMeaningful).WithMessage("اسم النموذج مطلوب.").MaximumLength(200);
        RuleFor(x => x.NameEn).MaximumLength(200).When(x => x.NameEn is not null);
        RuleFor(x => x.Description).Must(FormRequestValidation.BeMeaningful).WithMessage("الوصف مطلوب.").MaximumLength(2000);
        RuleFor(x => x.Classification).IsInEnum().WithMessage("مستوى التصنيف غير صالح.");
        RuleFor(x => x.ScopeType).IsInEnum().WithMessage("نوع النطاق غير صالح.");
        RuleFor(x => x).Must(ValidScopeShape).WithMessage("تركيبة النطاق غير صالحة.");
    }

    private static bool ValidScopeShape(CreateFormRequest request) =>
        request.ScopeType switch
        {
            ScopeType.Global or ScopeType.Headquarters =>
                !request.RegionId.HasValue && !request.FacilityId.HasValue && !request.FacilityUnitId.HasValue,
            ScopeType.Region =>
                request.RegionId.HasValue && !request.FacilityId.HasValue && !request.FacilityUnitId.HasValue,
            ScopeType.Facility =>
                request.RegionId.HasValue && request.FacilityId.HasValue && !request.FacilityUnitId.HasValue,
            ScopeType.FacilityUnit =>
                request.RegionId.HasValue && request.FacilityId.HasValue && request.FacilityUnitId.HasValue,
            _ => false
        };
}

public sealed class UpdateFormRequestValidator : AbstractValidator<UpdateFormRequest>
{
    public UpdateFormRequestValidator()
    {
        RuleFor(x => x.NameAr).Must(FormRequestValidation.BeMeaningful).WithMessage("اسم النموذج مطلوب.").MaximumLength(200);
        RuleFor(x => x.NameEn).MaximumLength(200).When(x => x.NameEn is not null);
        RuleFor(x => x.Description).Must(FormRequestValidation.BeMeaningful).WithMessage("الوصف مطلوب.").MaximumLength(2000);
        RuleFor(x => x.Classification).IsInEnum().WithMessage("مستوى التصنيف غير صالح.");
        RuleFor(x => x.RowVersion).Must(FormRequestValidation.BeValidRowVersion).WithMessage("إصدار السجل غير صالح.");
    }
}

public sealed class FormTransitionRequestValidator : AbstractValidator<FormTransitionRequest>
{
    public FormTransitionRequestValidator()
    {
        RuleFor(x => x.Reason).Must(FormRequestValidation.BeMeaningful).WithMessage("السبب مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(FormRequestValidation.BeValidRowVersion).WithMessage("إصدار السجل غير صالح.");
    }
}

public sealed class FormRejectTransitionRequestValidator : AbstractValidator<FormTransitionRequest>
{
    public FormRejectTransitionRequestValidator()
    {
        RuleFor(x => x.Reason).Must(FormRequestValidation.BeMeaningful).WithMessage("سبب الرفض مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(FormRequestValidation.BeValidRowVersion).WithMessage("إصدار السجل غير صالح.");
    }
}

public sealed class FormArchiveTransitionRequestValidator : AbstractValidator<FormTransitionRequest>
{
    public FormArchiveTransitionRequestValidator()
    {
        RuleFor(x => x.Reason).Must(FormRequestValidation.BeMeaningful).WithMessage("سبب الأرشفة مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(FormRequestValidation.BeValidRowVersion).WithMessage("إصدار السجل غير صالح.");
    }
}

public sealed class CreateFormAccessGrantRequestValidator : AbstractValidator<CreateFormAccessGrantRequest>
{
    public CreateFormAccessGrantRequestValidator()
    {
        RuleFor(x => x.PrincipalType).IsInEnum();
        RuleFor(x => x.PrincipalId).NotEmpty();
        RuleFor(x => x.Capability).IsInEnum();
        RuleFor(x => x.Effect).IsInEnum();
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب المنح مطلوب.").MaximumLength(1000);
        RuleFor(x => x).Must(ValidScopeShape).WithMessage("تركيبة نطاق المنح غير صالحة.");
        RuleFor(x => x)
            .Must(x => !x.ValidFromUtc.HasValue || !x.ValidToUtc.HasValue || x.ValidToUtc > x.ValidFromUtc)
            .WithMessage("ValidTo يجب أن يكون بعد ValidFrom.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool ValidScopeShape(CreateFormAccessGrantRequest request) =>
        request.ScopeType switch
        {
            null => !request.RegionId.HasValue && !request.FacilityId.HasValue,
            ScopeType.Global or ScopeType.Headquarters =>
                !request.RegionId.HasValue && !request.FacilityId.HasValue,
            ScopeType.Region =>
                request.RegionId.HasValue && !request.FacilityId.HasValue,
            ScopeType.Facility =>
                request.FacilityId.HasValue,
            _ => false
        };
}

public sealed class UpdateFormGovernancePolicyRequestValidator : AbstractValidator<UpdateFormGovernancePolicyRequest>
{
    public UpdateFormGovernancePolicyRequestValidator()
    {
        RuleFor(x => x.DefaultRetentionDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SensitiveRetentionDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinimumRetentionDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => x.MinimumRetentionDays <= x.DefaultRetentionDays)
            .WithMessage("الحد الأدنى لمدة الاحتفاظ يجب ألا يتجاوز مدة الاحتفاظ الافتراضية.");
        RuleFor(x => x)
            .Must(x => x.MinimumRetentionDays <= x.SensitiveRetentionDays)
            .WithMessage("الحد الأدنى لمدة الاحتفاظ يجب ألا يتجاوز مدة الاحتفاظ للمحتوى الحساس.");
        RuleFor(x => x.RowVersion).Must(FormRequestValidation.BeValidRowVersion).WithMessage("إصدار السجل غير صالح.");
    }
}
