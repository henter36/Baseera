namespace Baseera.Application.Notes;

using FluentValidation;

public sealed class CreateNoteRequestValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteRequestValidator()
    {
        RuleFor(x => x.Title).Must(BeMeaningful).WithMessage("العنوان مطلوب.").MaximumLength(300);
        RuleFor(x => x.Description).Must(BeMeaningful).WithMessage("الوصف مطلوب.").MaximumLength(8000);
        RuleFor(x => x.NoteTypeId).NotEmpty().WithMessage("نوع الملاحظة مطلوب.");
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
        RuleFor(x => x.NoteTypeId).NotEmpty().WithMessage("نوع الملاحظة مطلوب.");
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

public sealed class CreateNoteTypeRequestValidator : AbstractValidator<CreateNoteTypeRequest>
{
    public CreateNoteTypeRequestValidator()
    {
        RuleFor(x => x.Code).Must(BeMeaningful).WithMessage("رمز النوع مطلوب.").MaximumLength(50);
        RuleFor(x => x.NameAr).Must(BeMeaningful).WithMessage("اسم النوع مطلوب.").MaximumLength(200);
        RuleFor(x => x.DescriptionAr).MaximumLength(1000);
        RuleFor(x => x.EntryInstructionsAr).MaximumLength(2000);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DefaultSeverity).IsInEnum();
        RuleFor(x => x.DefaultDueDays).GreaterThanOrEqualTo(0).When(x => x.DefaultDueDays.HasValue);
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class UpdateNoteTypeRequestValidator : AbstractValidator<UpdateNoteTypeRequest>
{
    public UpdateNoteTypeRequestValidator()
    {
        RuleFor(x => x.NameAr).Must(BeMeaningful).WithMessage("اسم النوع مطلوب.").MaximumLength(200);
        RuleFor(x => x.DescriptionAr).MaximumLength(1000);
        RuleFor(x => x.EntryInstructionsAr).MaximumLength(2000);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DefaultSeverity).IsInEnum();
        RuleFor(x => x.DefaultDueDays).GreaterThanOrEqualTo(0).When(x => x.DefaultDueDays.HasValue);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class ReplaceRoleNoteTypeGrantsRequestValidator : AbstractValidator<ReplaceRoleNoteTypeGrantsRequest>
{
    public ReplaceRoleNoteTypeGrantsRequestValidator()
    {
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب التعديل مطلوب.").MaximumLength(1000);
        RuleFor(x => x.Grants).Must(HaveDistinctNoteTypes).WithMessage("لا يمكن تكرار نوع الملاحظة في الطلب.");
        RuleForEach(x => x.Grants).ChildRules(item =>
        {
            item.RuleFor(x => x.NoteTypeId).NotEmpty();
        });
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool HaveDistinctNoteTypes(IEnumerable<ReplaceRoleNoteTypeGrantItem> grants) =>
        grants.Select(grant => grant.NoteTypeId).Distinct().Count() == grants.Count();
}

public sealed class ReplaceUserNoteTypeOverridesRequestValidator : AbstractValidator<ReplaceUserNoteTypeOverridesRequest>
{
    public ReplaceUserNoteTypeOverridesRequestValidator()
    {
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب التعديل مطلوب.").MaximumLength(1000);
        RuleFor(x => x.Overrides).Must(HaveDistinctNoteTypes).WithMessage("لا يمكن تكرار نوع الملاحظة في الطلب.");
        RuleForEach(x => x.Overrides).ChildRules(item =>
        {
            item.RuleFor(x => x.NoteTypeId).NotEmpty();
        });
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool HaveDistinctNoteTypes(IEnumerable<ReplaceUserNoteTypeOverrideItem> overrides) =>
        overrides.Select(overrideRow => overrideRow.NoteTypeId).Distinct().Count() == overrides.Count();
}

public sealed class UpdateUserNoteIntakeProfileRequestValidator : AbstractValidator<UpdateUserNoteIntakeProfileRequest>
{
    public UpdateUserNoteIntakeProfileRequestValidator()
    {
        RuleFor(x => x.LockType).IsInEnum();
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب تغيير سياق الإدخال مطلوب.").MaximumLength(1000);
        RuleFor(x => x)
            .Must(x => x.LockType != Domain.Notes.NoteIntakeLockType.None || (!x.RegionId.HasValue && !x.FacilityId.HasValue))
            .WithMessage("دون تثبيت لا يقبل معرف منطقة أو موقع.");
        RuleFor(x => x)
            .Must(x => x.LockType != Domain.Notes.NoteIntakeLockType.Region || (x.RegionId.HasValue && !x.FacilityId.HasValue))
            .WithMessage("تثبيت المنطقة يتطلب RegionId فقط.");
        RuleFor(x => x)
            .Must(x => x.LockType != Domain.Notes.NoteIntakeLockType.Facility || x.FacilityId.HasValue)
            .WithMessage("تثبيت الموقع يتطلب FacilityId.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class CreateNoteRoutingRuleRequestValidator : AbstractValidator<CreateNoteRoutingRuleRequest>
{
    public CreateNoteRoutingRuleRequestValidator()
    {
        RuleFor(x => x.Code).Must(BeMeaningful).WithMessage("رمز قاعدة التوجيه مطلوب.").MaximumLength(80);
        RuleFor(x => x.NameAr).Must(BeMeaningful).WithMessage("اسم قاعدة التوجيه مطلوب.").MaximumLength(200);
        RuleFor(x => x.DescriptionAr).MaximumLength(1000);
        RuleFor(x => x.NoteTypeId).NotEmpty();
        RuleFor(x => x.ScopeType).IsInEnum();
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ProcessingTargetType).IsInEnum();
        RuleFor(x => x.DefaultDueDays).GreaterThanOrEqualTo(0).When(x => x.DefaultDueDays.HasValue);
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب التعديل مطلوب.").MaximumLength(1000);
        RuleFor(x => x).Must(ValidTargetShape).WithMessage("هدف التوجيه يجب أن يكون إدارة فقط أو دورًا فقط.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool ValidTargetShape(CreateNoteRoutingRuleRequest request) =>
        request.ProcessingTargetType switch
        {
            Domain.Notes.NoteRoutingProcessingTargetType.Department => request.ProcessingDepartmentId.HasValue && !request.ProcessingRoleId.HasValue,
            Domain.Notes.NoteRoutingProcessingTargetType.Role => request.ProcessingRoleId.HasValue && !request.ProcessingDepartmentId.HasValue,
            _ => false
        };
}

public sealed class UpdateNoteRoutingRuleRequestValidator : AbstractValidator<UpdateNoteRoutingRuleRequest>
{
    public UpdateNoteRoutingRuleRequestValidator()
    {
        RuleFor(x => x.NameAr).Must(BeMeaningful).WithMessage("اسم قاعدة التوجيه مطلوب.").MaximumLength(200);
        RuleFor(x => x.DescriptionAr).MaximumLength(1000);
        RuleFor(x => x.ScopeType).IsInEnum();
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ProcessingTargetType).IsInEnum();
        RuleFor(x => x.DefaultDueDays).GreaterThanOrEqualTo(0).When(x => x.DefaultDueDays.HasValue);
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب التعديل مطلوب.").MaximumLength(1000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
        RuleFor(x => x).Must(ValidTargetShape).WithMessage("هدف التوجيه يجب أن يكون إدارة فقط أو دورًا فقط.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool ValidTargetShape(UpdateNoteRoutingRuleRequest request) =>
        request.ProcessingTargetType switch
        {
            Domain.Notes.NoteRoutingProcessingTargetType.Department => request.ProcessingDepartmentId.HasValue && !request.ProcessingRoleId.HasValue,
            Domain.Notes.NoteRoutingProcessingTargetType.Role => request.ProcessingRoleId.HasValue && !request.ProcessingDepartmentId.HasValue,
            _ => false
        };
}

public sealed class RunNoteRoutingRequestValidator : AbstractValidator<RunNoteRoutingRequest>
{
    public RunNoteRoutingRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب تشغيل التوجيه مطلوب.").MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).Must(BeMeaningful).WithMessage("مفتاح منع التكرار مطلوب.").MaximumLength(120);
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

// ===== Phase 1B validators =====

public sealed class TriageValidRequestValidator : AbstractValidator<TriageValidRequest>
{
    public TriageValidRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class ProposeInvalidRequestValidator : AbstractValidator<ProposeInvalidRequest>
{
    public ProposeInvalidRequestValidator()
    {
        RuleFor(x => x.JustificationAr).Must(BeMeaningful).WithMessage("مبرر اعتبار الملاحظة غير صحيحة مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class ProposeDuplicateRequestValidator : AbstractValidator<ProposeDuplicateRequest>
{
    public ProposeDuplicateRequestValidator()
    {
        RuleFor(x => x.OriginalNoteId).NotEmpty().WithMessage("الملاحظة الأصلية مطلوبة.");
        RuleFor(x => x.JustificationAr).Must(BeMeaningful).WithMessage("مبرر اعتبارها مكررة مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class RecordTreatmentResultRequestValidator : AbstractValidator<RecordTreatmentResultRequest>
{
    public RecordTreatmentResultRequestValidator()
    {
        RuleFor(x => x.TreatmentResultText).Must(BeMeaningful).WithMessage("نتيجة المعالجة مطلوبة.").MaximumLength(4000);
        RuleFor(x => x.ExecutionType).IsInEnum();
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class ProposeNoActionRequestValidator : AbstractValidator<ProposeNoActionRequest>
{
    public ProposeNoActionRequestValidator()
    {
        RuleFor(x => x.JustificationAr).Must(BeMeaningful).WithMessage("مبرر عدم الحاجة إلى إجراء مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class ApproveNoteDecisionRequestValidator : AbstractValidator<ApproveNoteDecisionRequest>
{
    public ApproveNoteDecisionRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
        RuleFor(x => x.ReviewReason).MaximumLength(2000);
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class ReturnNoteDecisionRequestValidator : AbstractValidator<ReturnNoteDecisionRequest>
{
    public ReturnNoteDecisionRequestValidator()
    {
        RuleFor(x => x.ReviewReason).Must(BeMeaningful).WithMessage("سبب الإعادة مطلوب.").MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class AddPartsRequirementRequestValidator : AbstractValidator<AddPartsRequirementRequest>
{
    public AddPartsRequirementRequestValidator()
    {
        RuleFor(x => x.ItemName).Must(BeMeaningful).WithMessage("اسم القطعة مطلوب.").MaximumLength(300);
        RuleFor(x => x.ItemCode).MaximumLength(100);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("الكمية يجب أن تكون أكبر من صفر.");
        RuleFor(x => x.Unit).Must(BeMeaningful).WithMessage("الوحدة مطلوبة.").MaximumLength(50);
        RuleFor(x => x.RequestNumber).MaximumLength(100);
        RuleFor(x => x.SupplierOrSource).MaximumLength(300);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class UpdatePartsRequirementRequestValidator : AbstractValidator<UpdatePartsRequirementRequest>
{
    public UpdatePartsRequirementRequestValidator()
    {
        RuleFor(x => x.ItemName).Must(BeMeaningful).WithMessage("اسم القطعة مطلوب.").MaximumLength(300);
        RuleFor(x => x.ItemCode).MaximumLength(100);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("الكمية يجب أن تكون أكبر من صفر.");
        RuleFor(x => x.Unit).Must(BeMeaningful).WithMessage("الوحدة مطلوبة.").MaximumLength(50);
        RuleFor(x => x.RequestNumber).MaximumLength(100);
        RuleFor(x => x.SupplierOrSource).MaximumLength(300);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class UpdatePartsRequirementStatusRequestValidator : AbstractValidator<UpdatePartsRequirementStatusRequest>
{
    public UpdatePartsRequirementStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class CancelPartsRequirementRequestValidator : AbstractValidator<CancelPartsRequirementRequest>
{
    public CancelPartsRequirementRequestValidator()
    {
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب الإلغاء مطلوب.").MaximumLength(1000);
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class RequestSlaPauseRequestValidator : AbstractValidator<RequestSlaPauseRequest>
{
    public RequestSlaPauseRequestValidator()
    {
        RuleFor(x => x.Reason).Must(BeMeaningful).WithMessage("سبب طلب التجميد مطلوب.").MaximumLength(1000);
        RuleFor(x => x.RelatedPartsRequirementIds).NotEmpty().WithMessage("يتطلب طلب التجميد ربط عنصر قطعة واحد على الأقل.");
        RuleFor(x => x.RowVersion).Must(BeMeaningful).WithMessage("إصدار السجل مطلوب.");
    }

    private static bool BeMeaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}
