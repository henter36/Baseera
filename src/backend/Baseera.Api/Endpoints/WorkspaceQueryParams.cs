namespace Baseera.Api.Endpoints;

using Baseera.Application.Workspaces;

public sealed class WorkspaceQueryParams
{
    public WorkspaceLevel? Level { get; init; }
    public Guid? RegionId { get; init; }
    public Guid? FacilityId { get; init; }
    public Guid? EntityId { get; init; }
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtc { get; init; }
    public string? Locale { get; init; }
    public string? TimeZone { get; init; }

    public WorkspaceRequest ToRequest(string workspaceKey)
    {
        return new WorkspaceRequest(
            workspaceKey,
            Level,
            RegionId,
            FacilityId,
            EntityId,
            FromUtc,
            ToUtc,
            Locale,
            TimeZone);
    }
}
