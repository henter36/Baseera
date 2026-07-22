namespace Baseera.Api.Endpoints;

using Baseera.Application.Forms;
using Baseera.Domain.Attachments;
using Baseera.Domain.Forms;

public sealed class FormListQueryParams
{
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? Search { get; set; }
    public FormDefinitionStatus? Status { get; set; }
    public ClassificationLevel? Classification { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? FacilityId { get; set; }
    public string? SortBy { get; set; }
    public bool? SortDesc { get; set; }

    public FormListQuery ToQuery() => new()
    {
        Page = Page ?? 1,
        PageSize = PageSize ?? 20,
        Search = Search,
        Status = Status,
        Classification = Classification,
        RegionId = RegionId,
        FacilityId = FacilityId,
        SortBy = SortBy,
        SortDesc = SortDesc ?? false
    };
}
