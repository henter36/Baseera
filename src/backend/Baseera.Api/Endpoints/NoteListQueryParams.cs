namespace Baseera.Api.Endpoints;

using Baseera.Application.Notes;
using Baseera.Domain.Attachments;
using Baseera.Domain.Notes;

/// <summary>
/// Query-string binding surface for GET /api/v1/notes via <c>[AsParameters]</c>.
/// Non-nullable value types must be nullable here: Minimal API treats
/// AsParameters properties like handler parameters, so missing query keys
/// would otherwise yield HTTP 400 (property initializers are not applied).
/// Defaults match the public contract (Page=1, PageSize=20, flags false).
/// </summary>
public sealed class NoteListQueryParams
{
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? Search { get; set; }
    public NoteStatus? Status { get; set; }
    public NoteSeverity? Severity { get; set; }
    public Guid? NoteTypeId { get; set; }
    public NoteSourceType? SourceType { get; set; }
    public ClassificationLevel? Classification { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? FacilityUnitId { get; set; }
    public Guid? OwnerDepartmentId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public bool? OverdueOnly { get; set; }
    public bool? RequiresMyAction { get; set; }
    public bool? RequiresRouting { get; set; }
    public DateTimeOffset? DueFrom { get; set; }
    public DateTimeOffset? DueTo { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
    public string? SortBy { get; set; }
    public bool? SortDesc { get; set; }

    public NoteListQuery ToQuery() => new()
    {
        Page = Page ?? 1,
        PageSize = PageSize ?? 20,
        Search = Search,
        Status = Status,
        Severity = Severity,
        NoteTypeId = NoteTypeId,
        SourceType = SourceType,
        Classification = Classification,
        RegionId = RegionId,
        FacilityId = FacilityId,
        FacilityUnitId = FacilityUnitId,
        OwnerDepartmentId = OwnerDepartmentId,
        AssignedToUserId = AssignedToUserId,
        OverdueOnly = OverdueOnly ?? false,
        RequiresMyAction = RequiresMyAction ?? false,
        RequiresRouting = RequiresRouting ?? false,
        DueFrom = DueFrom,
        DueTo = DueTo,
        CreatedFrom = CreatedFrom,
        CreatedTo = CreatedTo,
        SortBy = SortBy,
        SortDesc = SortDesc ?? false
    };
}
