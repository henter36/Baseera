using Baseera.Application.Notes;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Notes;

namespace Baseera.UnitTests;

public sealed class CreateNoteRequestValidatorTests
{
    private readonly CreateNoteRequestValidator _validator = new();

    private static CreateNoteRequest Valid() => new(
        Title: "عطل في الإنارة",
        Description: "إنارة الممر رقم 3 معطلة منذ يومين",
        Category: NoteCategory.Technical,
        Severity: NoteSeverity.Medium,
        SourceType: NoteSourceType.Manual,
        SourceReference: null,
        Classification: ClassificationLevel.Internal,
        ScopeType: ScopeType.Facility,
        RegionId: null,
        FacilityId: Guid.NewGuid(),
        FacilityUnitId: null,
        OwnerDepartmentId: null,
        DueAtUtc: DateTimeOffset.UtcNow.AddDays(3));

    [Fact]
    public void Valid_request_passes() =>
        Assert.True(_validator.Validate(Valid()).IsValid);

    [Fact]
    public void Empty_title_is_rejected()
    {
        var request = Valid() with { Title = "   " };
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Empty_description_is_rejected()
    {
        var request = Valid() with { Description = "" };
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Title_over_max_length_is_rejected()
    {
        var request = Valid() with { Title = new string('a', 301) };
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Past_due_date_is_rejected()
    {
        var request = Valid() with { DueAtUtc = DateTimeOffset.UtcNow.AddDays(-2) };
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Null_due_date_is_allowed()
    {
        var request = Valid() with { DueAtUtc = null };
        Assert.True(_validator.Validate(request).IsValid);
    }
}

public sealed class UpdateNoteRequestValidatorTests
{
    private readonly UpdateNoteRequestValidator _validator = new();

    private static UpdateNoteRequest Valid() => new(
        Title: "عنوان محدّث",
        Description: "وصف محدّث",
        Category: NoteCategory.Operational,
        Severity: NoteSeverity.Low,
        SourceType: NoteSourceType.Manual,
        SourceReference: null,
        Classification: ClassificationLevel.Internal,
        OwnerDepartmentId: null,
        DueAtUtc: null,
        RowVersion: Convert.ToBase64String([1, 2, 3]));

    [Fact]
    public void Valid_request_passes() =>
        Assert.True(_validator.Validate(Valid()).IsValid);

    [Fact]
    public void Missing_rowversion_is_rejected()
    {
        var request = Valid() with { RowVersion = "" };
        Assert.False(_validator.Validate(request).IsValid);
    }
}

public sealed class AssignNoteRequestValidatorTests
{
    private readonly AssignNoteRequestValidator _validator = new();

    private static AssignNoteRequest Valid() => new(
        AssignedToUserId: Guid.NewGuid(),
        AssignedToDepartmentId: null,
        DueAtUtc: null,
        Reason: "تكليف أولي",
        RowVersion: Convert.ToBase64String([1, 2, 3]));

    [Fact]
    public void Valid_user_assignment_passes() =>
        Assert.True(_validator.Validate(Valid()).IsValid);

    [Fact]
    public void Valid_department_assignment_passes()
    {
        var request = Valid() with { AssignedToUserId = null, AssignedToDepartmentId = Guid.NewGuid() };
        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Neither_user_nor_department_is_rejected()
    {
        var request = Valid() with { AssignedToUserId = null, AssignedToDepartmentId = null };
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Both_user_and_department_is_rejected()
    {
        var request = Valid() with { AssignedToDepartmentId = Guid.NewGuid() };
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Missing_reason_is_rejected()
    {
        var request = Valid() with { Reason = " " };
        Assert.False(_validator.Validate(request).IsValid);
    }
}

public sealed class TransitionNoteRequestValidatorTests
{
    private readonly TransitionNoteRequestValidator _validator = new();

    [Fact]
    public void Reason_and_rowversion_required()
    {
        Assert.False(_validator.Validate(new TransitionNoteRequest("", "rv")).IsValid);
        Assert.False(_validator.Validate(new TransitionNoteRequest("سبب", "")).IsValid);
        Assert.True(_validator.Validate(new TransitionNoteRequest("سبب الإلغاء", "rv")).IsValid);
    }
}

public sealed class WorkflowActionRequestValidatorTests
{
    private readonly WorkflowActionRequestValidator _validator = new();

    [Fact]
    public void Reason_is_optional_but_rowversion_required()
    {
        Assert.True(_validator.Validate(new WorkflowActionRequest(null, "rv")).IsValid);
        Assert.False(_validator.Validate(new WorkflowActionRequest(null, "")).IsValid);
    }
}

public sealed class CloseNoteRequestValidatorTests
{
    private readonly CloseNoteRequestValidator _validator = new();

    [Fact]
    public void Reason_and_closure_summary_are_required()
    {
        Assert.False(_validator.Validate(new CloseNoteRequest("", "ملخص", "rv")).IsValid);
        Assert.False(_validator.Validate(new CloseNoteRequest("سبب", "", "rv")).IsValid);
        Assert.True(_validator.Validate(new CloseNoteRequest("سبب الإغلاق", "تم الإصلاح", "rv")).IsValid);
    }
}

public sealed class ReopenNoteRequestValidatorTests
{
    private readonly ReopenNoteRequestValidator _validator = new();

    [Fact]
    public void Reason_is_required()
    {
        Assert.False(_validator.Validate(new ReopenNoteRequest("", "rv")).IsValid);
        Assert.True(_validator.Validate(new ReopenNoteRequest("تكرر العطل", "rv")).IsValid);
    }
}

public sealed class NoteAttachmentEntityTypeTests
{
    [Fact]
    public void OperationalNote_is_an_allowed_attachment_entity_type() =>
        Assert.True(Baseera.Application.Attachments.AttachmentEntityTypes.IsAllowed("OperationalNote"));

    [Fact]
    public void OperationalNote_entity_type_match_is_case_insensitive() =>
        Assert.True(Baseera.Application.Attachments.AttachmentEntityTypes.IsAllowed("operationalnote"));
}
