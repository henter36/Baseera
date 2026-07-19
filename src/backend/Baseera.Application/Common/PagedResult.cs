namespace Baseera.Application.Common;

public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed class PagedQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }

    public int Skip => Math.Max(Page - 1, 0) * Math.Clamp(PageSize, 1, 200);
    public int Take => Math.Clamp(PageSize, 1, 200);
}

public static class TimeZones
{
    public static readonly TimeZoneInfo SaudiArabia =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Arab Standard Time" : "Asia/Riyadh");

    public static DateTimeOffset ToSaudi(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, SaudiArabia);
}
