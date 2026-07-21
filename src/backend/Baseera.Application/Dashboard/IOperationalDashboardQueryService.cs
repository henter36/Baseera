namespace Baseera.Application.Dashboard;

public interface IOperationalDashboardQueryService
{
    Task<OperationalDashboardSummaryDto> GetSummaryAsync(
        OperationalDashboardQuery query,
        CancellationToken cancellationToken = default);

    Task<OperationalDashboardTrendsDto> GetTrendsAsync(
        OperationalDashboardQuery query,
        CancellationToken cancellationToken = default);

    Task<OperationalDashboardBreakdownsDto> GetBreakdownsAsync(
        OperationalDashboardQuery query,
        CancellationToken cancellationToken = default);

    Task<OperationalDashboardPriorityQueuesDto> GetPriorityQueuesAsync(
        OperationalDashboardQuery query,
        CancellationToken cancellationToken = default);
}
