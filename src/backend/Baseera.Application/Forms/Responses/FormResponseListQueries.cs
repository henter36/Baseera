namespace Baseera.Application.Forms.Responses;

using Baseera.Domain.Forms;

internal static class FormResponseListQueries
{
    internal sealed class WorkspaceRow
    {
        public FormFacilityAssignment Assignment { get; init; } = null!;
        public FormCycle Cycle { get; init; } = null!;
        public FormCampaign Campaign { get; init; } = null!;
        public FormCampaignResponsePolicy Policy { get; init; } = null!;
        public FormResponse? Response { get; init; }
    }

    internal sealed class InboxRow
    {
        public FormResponse Response { get; init; } = null!;
        public FormFacilityAssignment Assignment { get; init; } = null!;
        public FormCycle Cycle { get; init; } = null!;
        public FormCampaign Campaign { get; init; } = null!;
        public FormCampaignResponsePolicy Policy { get; init; } = null!;
    }

    public static FormResponseWorkspaceQuery Normalize(FormResponseWorkspaceQuery query)
    {
        var page = Math.Max(1, query.Page ?? 1);
        var rawSize = query.PageSize ?? 20;
        var pageSize = Math.Clamp(rawSize <= 0 ? 20 : rawSize, 1, 100);
        return query with { Page = page, PageSize = pageSize };
    }

    public static FormResponseReviewInboxQuery Normalize(FormResponseReviewInboxQuery query)
    {
        var page = Math.Max(1, query.Page ?? 1);
        var rawSize = query.PageSize ?? 20;
        var pageSize = Math.Clamp(rawSize <= 0 ? 20 : rawSize, 1, 100);
        return query with { Page = page, PageSize = pageSize };
    }

    public static void EnsureKnownWorkStatus(string? workStatus)
    {
        if (string.IsNullOrWhiteSpace(workStatus)) return;
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "current", "upcoming", "overdue", "returned", "submitted", "completed"
        };
        if (!known.Contains(workStatus.Trim()))
        {
            throw new ArgumentException("قيمة workStatus غير صالحة.");
        }
    }

    public static void EnsureKnownReviewStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return;
        if (!Enum.TryParse<FormResponseStatus>(status, ignoreCase: true, out _))
        {
            throw new ArgumentException("قيمة status غير صالحة.");
        }
    }

    public static IQueryable<WorkspaceRow> ApplyWorkspaceFilters(
        IQueryable<WorkspaceRow> query,
        FormResponseWorkspaceQuery filters,
        DateTimeOffset nowUtc)
    {
        if (filters.CampaignId.HasValue)
            query = query.Where(x => x.Assignment.CampaignId == filters.CampaignId);
        if (filters.CycleId.HasValue)
            query = query.Where(x => x.Assignment.CycleId == filters.CycleId);
        if (filters.FacilityId.HasValue)
            query = query.Where(x => x.Assignment.FacilityId == filters.FacilityId);
        if (filters.RegionId.HasValue)
            query = query.Where(x => x.Assignment.RegionIdAtAssignment == filters.RegionId);
        if (filters.DueFrom.HasValue)
            query = query.Where(x => x.Cycle.DueAtUtc >= filters.DueFrom);
        if (filters.DueTo.HasValue)
            query = query.Where(x => x.Cycle.DueAtUtc <= filters.DueTo);
        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var term = filters.Search.Trim();
            query = query.Where(x =>
                x.Campaign.NameAr.Contains(term)
                || x.Campaign.Code.Contains(term)
                || x.Assignment.FacilityNameArAtAssignment.Contains(term));
        }

        return ApplyWorkStatusFilter(query, filters.WorkStatus, nowUtc);
    }

    public static IQueryable<WorkspaceRow> ApplyWorkStatusFilter(
        IQueryable<WorkspaceRow> query,
        string? workStatus,
        DateTimeOffset nowUtc)
    {
        var key = string.IsNullOrWhiteSpace(workStatus) ? "current" : workStatus.Trim().ToLowerInvariant();
        return key switch
        {
            "upcoming" => FilterWorkspaceUpcoming(query, nowUtc),
            "overdue" => FilterWorkspaceOverdue(query, nowUtc),
            "returned" => FilterWorkspaceReturned(query),
            "submitted" => FilterWorkspaceSubmitted(query),
            "completed" => FilterWorkspaceCompleted(query),
            _ => FilterWorkspaceCurrent(query, nowUtc)
        };
    }

    private static IQueryable<WorkspaceRow> FilterWorkspaceUpcoming(
        IQueryable<WorkspaceRow> query,
        DateTimeOffset nowUtc) =>
        query.Where(x => nowUtc < x.Cycle.OpenAtUtc);

    private static IQueryable<WorkspaceRow> FilterWorkspaceReturned(IQueryable<WorkspaceRow> query) =>
        query.Where(x => x.Response != null && x.Response.Status == FormResponseStatus.Returned);

    private static IQueryable<WorkspaceRow> FilterWorkspaceSubmitted(IQueryable<WorkspaceRow> query) =>
        query.Where(x =>
            x.Response != null
            && (x.Response.Status == FormResponseStatus.Submitted
                || x.Response.Status == FormResponseStatus.UnderReview));

    private static IQueryable<WorkspaceRow> WhereWorkspaceCompleted(IQueryable<WorkspaceRow> query) =>
        query.Where(x =>
            (x.Policy.CompletionBasis == FormCompletionBasis.Submitted
                && x.Response != null
                && (x.Response.Status == FormResponseStatus.Submitted
                    || x.Response.Status == FormResponseStatus.UnderReview
                    || x.Response.Status == FormResponseStatus.Approved
                    || x.Response.Status == FormResponseStatus.Closed))
            || (x.Policy.CompletionBasis == FormCompletionBasis.Approved
                && x.Response != null
                && (x.Response.Status == FormResponseStatus.Approved
                    || x.Response.Status == FormResponseStatus.Closed)));

    private static IQueryable<WorkspaceRow> WhereWorkspaceIncomplete(IQueryable<WorkspaceRow> query) =>
        query.Where(x =>
            !((x.Policy.CompletionBasis == FormCompletionBasis.Submitted
                    && x.Response != null
                    && (x.Response.Status == FormResponseStatus.Submitted
                        || x.Response.Status == FormResponseStatus.UnderReview
                        || x.Response.Status == FormResponseStatus.Approved
                        || x.Response.Status == FormResponseStatus.Closed))
                || (x.Policy.CompletionBasis == FormCompletionBasis.Approved
                    && x.Response != null
                    && (x.Response.Status == FormResponseStatus.Approved
                        || x.Response.Status == FormResponseStatus.Closed))));

    private static IQueryable<WorkspaceRow> FilterWorkspaceCompleted(IQueryable<WorkspaceRow> query) =>
        WhereWorkspaceCompleted(query);

    private static IQueryable<WorkspaceRow> FilterWorkspaceOverdue(
        IQueryable<WorkspaceRow> query,
        DateTimeOffset nowUtc) =>
        WhereWorkspaceIncomplete(query).Where(x =>
            nowUtc > (x.Response != null && x.Response.DueAtUtcOverride != null
                ? x.Response.DueAtUtcOverride.Value
                : x.Cycle.DueAtUtc));

    private static IQueryable<WorkspaceRow> FilterWorkspaceCurrent(
        IQueryable<WorkspaceRow> query,
        DateTimeOffset nowUtc) =>
        WhereWorkspaceIncomplete(query).Where(x =>
            nowUtc >= x.Cycle.OpenAtUtc
            && nowUtc <= x.Cycle.CloseAtUtc
            && (x.Response == null
                || (x.Response.Status != FormResponseStatus.Approved
                    && x.Response.Status != FormResponseStatus.Rejected
                    && x.Response.Status != FormResponseStatus.Closed)));

    public static IQueryable<InboxRow> ApplyInboxFilters(
        IQueryable<InboxRow> query,
        FormResponseReviewInboxQuery filters,
        DateTimeOffset nowUtc)
    {
        if (filters.CampaignId.HasValue)
            query = query.Where(x => x.Response.CampaignId == filters.CampaignId);
        if (filters.CycleId.HasValue)
            query = query.Where(x => x.Response.CycleId == filters.CycleId);
        if (filters.RegionId.HasValue)
            query = query.Where(x => x.Assignment.RegionIdAtAssignment == filters.RegionId);
        if (filters.FacilityId.HasValue)
            query = query.Where(x => x.Response.FacilityId == filters.FacilityId);
        if (filters.ReviewLevel.HasValue)
            query = query.Where(x => x.Response.CurrentReviewLevel == filters.ReviewLevel);
        if (filters.SubmittedFrom.HasValue)
            query = query.Where(x => x.Response.SubmittedAtUtc >= filters.SubmittedFrom);
        if (filters.SubmittedTo.HasValue)
            query = query.Where(x => x.Response.SubmittedAtUtc <= filters.SubmittedTo);
        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var term = filters.Search.Trim();
            query = query.Where(x =>
                x.Campaign.NameAr.Contains(term)
                || x.Assignment.FacilityNameArAtAssignment.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filters.Status)
            && Enum.TryParse<FormResponseStatus>(filters.Status, true, out var status))
        {
            query = query.Where(x => x.Response.Status == status);
        }

        return ApplyInboxOverdueFilter(query, filters.Overdue, nowUtc);
    }

    private static IQueryable<InboxRow> ApplyInboxOverdueFilter(
        IQueryable<InboxRow> query,
        bool? overdue,
        DateTimeOffset nowUtc)
    {
        if (overdue == true)
        {
            return FilterInboxOverdue(query, nowUtc);
        }

        if (overdue == false)
        {
            return FilterInboxNotOverdue(query, nowUtc);
        }

        return query;
    }

    private static IQueryable<InboxRow> WhereInboxCompleted(IQueryable<InboxRow> query) =>
        query.Where(x =>
            (x.Policy.CompletionBasis == FormCompletionBasis.Submitted
                && (x.Response.Status == FormResponseStatus.Submitted
                    || x.Response.Status == FormResponseStatus.UnderReview
                    || x.Response.Status == FormResponseStatus.Approved
                    || x.Response.Status == FormResponseStatus.Closed))
            || (x.Policy.CompletionBasis == FormCompletionBasis.Approved
                && (x.Response.Status == FormResponseStatus.Approved
                    || x.Response.Status == FormResponseStatus.Closed)));

    private static IQueryable<InboxRow> WhereInboxIncomplete(IQueryable<InboxRow> query) =>
        query.Where(x =>
            !((x.Policy.CompletionBasis == FormCompletionBasis.Submitted
                    && (x.Response.Status == FormResponseStatus.Submitted
                        || x.Response.Status == FormResponseStatus.UnderReview
                        || x.Response.Status == FormResponseStatus.Approved
                        || x.Response.Status == FormResponseStatus.Closed))
                || (x.Policy.CompletionBasis == FormCompletionBasis.Approved
                    && (x.Response.Status == FormResponseStatus.Approved
                        || x.Response.Status == FormResponseStatus.Closed))));

    private static IQueryable<InboxRow> FilterInboxOverdue(
        IQueryable<InboxRow> query,
        DateTimeOffset nowUtc) =>
        WhereInboxIncomplete(query).Where(x =>
            nowUtc > (x.Response.DueAtUtcOverride ?? x.Cycle.DueAtUtc));

    private static IQueryable<InboxRow> FilterInboxNotOverdue(
        IQueryable<InboxRow> query,
        DateTimeOffset nowUtc) =>
        WhereInboxCompleted(query).Union(
            WhereInboxIncomplete(query).Where(x =>
                nowUtc <= (x.Response.DueAtUtcOverride ?? x.Cycle.DueAtUtc)));
}
