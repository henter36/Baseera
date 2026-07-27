namespace Baseera.Api.Endpoints;

using Baseera.Application.RiskManagement;
using Baseera.Domain.RiskManagement;

public sealed class RiskListQueryParams
{
    public string? Search { get; set; }
    public RiskStatus? Status { get; set; }
    public RiskRatingSeverity? Severity { get; set; }
    public RiskTrend? Trend { get; set; }
    public Guid? OwnerWorkforceMemberId { get; set; }
    public bool? WithoutOwner { get; set; }
    public bool? WithoutTreatment { get; set; }
    public Guid? CategoryId { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }

    public RiskListFilters ToFilters() => new()
    {
        Search = Search,
        Status = Status,
        Severity = Severity,
        Trend = Trend,
        OwnerWorkforceMemberId = OwnerWorkforceMemberId,
        WithoutOwner = WithoutOwner,
        WithoutTreatment = WithoutTreatment,
        CategoryId = CategoryId,
        Page = Page ?? 1,
        PageSize = PageSize ?? 50
    };
}
