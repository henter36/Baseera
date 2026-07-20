namespace Baseera.Api.Endpoints;

using Baseera.Application.Notes;
using Baseera.Domain.Common;
using Baseera.Domain.Notes;

public sealed class NoteRoutingRuleQueryParams
{
    public Guid? NoteTypeId { get; set; }
    public ScopeType? ScopeType { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? FacilityUnitId { get; set; }
    public bool? IsActive { get; set; }
    public NoteRoutingProcessingTargetType? ProcessingTargetType { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }

    public NoteRoutingRuleQuery ToQuery() => new()
    {
        NoteTypeId = NoteTypeId,
        ScopeType = ScopeType,
        RegionId = RegionId,
        FacilityId = FacilityId,
        FacilityUnitId = FacilityUnitId,
        IsActive = IsActive,
        ProcessingTargetType = ProcessingTargetType,
        Page = Page ?? 1,
        PageSize = PageSize ?? 20
    };
}
