namespace Baseera.Application.Forms.Responses;

public sealed class FormResponseConflictException(FormResponseConflictDto payload) : Exception("تعارض في نسخة الرد")
{
    public FormResponseConflictDto Payload { get; } = payload;
}

public sealed class FormResponseValidationException(IReadOnlyList<FormResponseValidationIssueDto> issues)
    : Exception("فشل التحقق من الرد")
{
    public IReadOnlyList<FormResponseValidationIssueDto> Issues { get; } = issues;
}
