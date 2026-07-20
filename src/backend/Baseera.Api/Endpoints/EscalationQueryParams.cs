namespace Baseera.Api.Endpoints;

using Baseera.Application.Escalations;
using Baseera.Domain.Escalations;

public sealed class EscalationPolicyQueryParams
{
    public string? Search { get; set; }
    public EscalationTargetType? TargetType { get; set; }
    public bool? IsEnabled { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? SortBy { get; set; }
    public bool? SortDesc { get; set; }

    public EscalationPolicyQuery ToQuery() => new()
    {
        Search = Search,
        TargetType = TargetType,
        IsEnabled = IsEnabled,
        Page = Page ?? 1,
        PageSize = PageSize ?? 20,
        SortBy = SortBy,
        SortDesc = SortDesc ?? false
    };
}

public sealed class EscalationOccurrenceQueryParams
{
    public string? Search { get; set; }
    public EscalationTargetType? TargetType { get; set; }
    public EscalationOccurrenceStatus? Status { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? SortBy { get; set; }
    public bool? SortDesc { get; set; }

    public EscalationOccurrenceQuery ToQuery() => new()
    {
        Search = Search,
        TargetType = TargetType,
        Status = Status,
        Page = Page ?? 1,
        PageSize = PageSize ?? 20,
        SortBy = SortBy,
        SortDesc = SortDesc ?? true
    };
}

public sealed class NotificationQueryParams
{
    public NotificationStatus? Status { get; set; }
    public EscalationTargetType? TargetType { get; set; }
    public int? Priority { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? SortBy { get; set; }
    public bool? SortDesc { get; set; }

    public NotificationQuery ToQuery() => new()
    {
        Status = Status,
        TargetType = TargetType,
        Priority = Priority,
        CreatedFrom = CreatedFrom,
        CreatedTo = CreatedTo,
        Page = Page ?? 1,
        PageSize = PageSize ?? 20,
        SortBy = SortBy,
        SortDesc = SortDesc ?? true
    };
}
