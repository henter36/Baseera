namespace Baseera.Api.Endpoints;

using Baseera.Application.CorrectiveActions;
using Baseera.Domain.Attachments;
using Baseera.Domain.CorrectiveActions;

public sealed class CorrectiveActionListQueryParams
{
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? Search { get; set; }
    public Guid? NoteId { get; set; }
    public CorrectiveActionStatus? Status { get; set; }
    public CorrectiveActionPriority? Priority { get; set; }
    public ClassificationLevel? Classification { get; set; }
    public Guid? OwnerDepartmentId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? FacilityUnitId { get; set; }
    public bool? OverdueOnly { get; set; }
    public int? DueSoonDays { get; set; }
    public DateTimeOffset? DueFrom { get; set; }
    public DateTimeOffset? DueTo { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
    public string? SortBy { get; set; }
    public bool? SortDesc { get; set; }

    public CorrectiveActionListQuery ToQuery() => new()
    {
        Page = Page ?? 1,
        PageSize = PageSize ?? 20,
        Search = Search,
        NoteId = NoteId,
        Status = Status,
        Priority = Priority,
        Classification = Classification,
        OwnerDepartmentId = OwnerDepartmentId,
        AssignedToUserId = AssignedToUserId,
        RegionId = RegionId,
        FacilityId = FacilityId,
        FacilityUnitId = FacilityUnitId,
        OverdueOnly = OverdueOnly ?? false,
        DueSoonDays = DueSoonDays,
        DueFrom = DueFrom,
        DueTo = DueTo,
        CreatedFrom = CreatedFrom,
        CreatedTo = CreatedTo,
        SortBy = SortBy,
        SortDesc = SortDesc ?? false
    };
}
