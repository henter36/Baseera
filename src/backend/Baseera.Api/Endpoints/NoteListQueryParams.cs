namespace Baseera.Api.Endpoints;

using Baseera.Application.Notes;
using Baseera.Domain.Attachments;
using Baseera.Domain.Notes;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Query-string binding surface for GET /api/v1/notes.
/// Kept in the API layer because <c>[AsParameters]</c> on the Application
/// <see cref="NoteListQuery"/> type failed model binding (HTTP 400) in integration tests.
/// Property names match the existing public query contract exactly.
/// </summary>
public sealed class NoteListQueryParams
{
    [FromQuery] public int Page { get; set; } = 1;
    [FromQuery] public int PageSize { get; set; } = 20;
    [FromQuery] public string? Search { get; set; }
    [FromQuery] public NoteStatus? Status { get; set; }
    [FromQuery] public NoteSeverity? Severity { get; set; }
    [FromQuery] public NoteCategory? Category { get; set; }
    [FromQuery] public NoteSourceType? SourceType { get; set; }
    [FromQuery] public ClassificationLevel? Classification { get; set; }
    [FromQuery] public Guid? RegionId { get; set; }
    [FromQuery] public Guid? FacilityId { get; set; }
    [FromQuery] public Guid? FacilityUnitId { get; set; }
    [FromQuery] public Guid? OwnerDepartmentId { get; set; }
    [FromQuery] public Guid? AssignedToUserId { get; set; }
    [FromQuery] public bool OverdueOnly { get; set; }
    [FromQuery] public DateTimeOffset? DueFrom { get; set; }
    [FromQuery] public DateTimeOffset? DueTo { get; set; }
    [FromQuery] public DateTimeOffset? CreatedFrom { get; set; }
    [FromQuery] public DateTimeOffset? CreatedTo { get; set; }
    [FromQuery] public string? SortBy { get; set; }
    [FromQuery] public bool SortDesc { get; set; }

    public NoteListQuery ToQuery() => new()
    {
        Page = Page,
        PageSize = PageSize,
        Search = Search,
        Status = Status,
        Severity = Severity,
        Category = Category,
        SourceType = SourceType,
        Classification = Classification,
        RegionId = RegionId,
        FacilityId = FacilityId,
        FacilityUnitId = FacilityUnitId,
        OwnerDepartmentId = OwnerDepartmentId,
        AssignedToUserId = AssignedToUserId,
        OverdueOnly = OverdueOnly,
        DueFrom = DueFrom,
        DueTo = DueTo,
        CreatedFrom = CreatedFrom,
        CreatedTo = CreatedTo,
        SortBy = SortBy,
        SortDesc = SortDesc
    };
}
