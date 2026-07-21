namespace Baseera.Api.Endpoints;

using Baseera.Application.Dashboard;
using Baseera.Domain.Notes;

public sealed class OperationalDashboardQueryParams
{
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
    public int? PeriodDays { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? FacilityUnitId { get; set; }
    public Guid? NoteTypeId { get; set; }
    public NoteSeverity? Severity { get; set; }
    public NoteStatus? Status { get; set; }
    public OperationalDashboardBreakdownDimension? BreakdownBy { get; set; }
    public OperationalDashboardPriorityQueue? Queue { get; set; }

    public OperationalDashboardQuery ToQuery() => new()
    {
        FromUtc = FromUtc,
        ToUtc = ToUtc,
        PeriodDays = PeriodDays,
        RegionId = RegionId,
        FacilityId = FacilityId,
        FacilityUnitId = FacilityUnitId,
        NoteTypeId = NoteTypeId,
        Severity = Severity,
        Status = Status,
        BreakdownBy = BreakdownBy,
        Queue = Queue
    };
}
