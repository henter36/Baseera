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
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 100);
        return query with { Page = page, PageSize = pageSize };
    }

    public static FormResponseReviewInboxQuery Normalize(FormResponseReviewInboxQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 100);
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
            "upcoming" => query.Where(x => nowUtc < x.Cycle.OpenAtUtc),
            "overdue" => query.Where(x =>
                !((x.Policy.CompletionBasis == FormCompletionBasis.Submitted
                        && x.Response != null
                        && (x.Response.Status == FormResponseStatus.Submitted
                            || x.Response.Status == FormResponseStatus.UnderReview
                            || x.Response.Status == FormResponseStatus.Approved
                            || x.Response.Status == FormResponseStatus.Closed))
                    || (x.Policy.CompletionBasis == FormCompletionBasis.Approved
                        && x.Response != null
                        && (x.Response.Status == FormResponseStatus.Approved
                            || x.Response.Status == FormResponseStatus.Closed)))
                && nowUtc > (x.Response != null && x.Response.DueAtUtcOverride != null
                    ? x.Response.DueAtUtcOverride.Value
                    : x.Cycle.DueAtUtc)),
            "returned" => query.Where(x => x.Response != null && x.Response.Status == FormResponseStatus.Returned),
            "submitted" => query.Where(x =>
                x.Response != null
                && (x.Response.Status == FormResponseStatus.Submitted
                    || x.Response.Status == FormResponseStatus.UnderReview)),
            "completed" => query.Where(x =>
                (x.Policy.CompletionBasis == FormCompletionBasis.Submitted
                    && x.Response != null
                    && (x.Response.Status == FormResponseStatus.Submitted
                        || x.Response.Status == FormResponseStatus.UnderReview
                        || x.Response.Status == FormResponseStatus.Approved
                        || x.Response.Status == FormResponseStatus.Closed))
                || (x.Policy.CompletionBasis == FormCompletionBasis.Approved
                    && x.Response != null
                    && (x.Response.Status == FormResponseStatus.Approved
                        || x.Response.Status == FormResponseStatus.Closed))),
            _ => query.Where(x =>
                nowUtc >= x.Cycle.OpenAtUtc
                && nowUtc <= x.Cycle.CloseAtUtc
                && !((x.Policy.CompletionBasis == FormCompletionBasis.Submitted
                        && x.Response != null
                        && (x.Response.Status == FormResponseStatus.Submitted
                            || x.Response.Status == FormResponseStatus.UnderReview
                            || x.Response.Status == FormResponseStatus.Approved
                            || x.Response.Status == FormResponseStatus.Closed))
                    || (x.Policy.CompletionBasis == FormCompletionBasis.Approved
                        && x.Response != null
                        && (x.Response.Status == FormResponseStatus.Approved
                            || x.Response.Status == FormResponseStatus.Closed)))
                && (x.Response == null
                    || (x.Response.Status != FormResponseStatus.Approved
                        && x.Response.Status != FormResponseStatus.Rejected
                        && x.Response.Status != FormResponseStatus.Closed)))
        };
    }

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

        if (filters.Overdue == true)
        {
            query = query.Where(x =>
                !((x.Policy.CompletionBasis == FormCompletionBasis.Submitted
                        && (x.Response.Status == FormResponseStatus.Submitted
                            || x.Response.Status == FormResponseStatus.UnderReview
                            || x.Response.Status == FormResponseStatus.Approved
                            || x.Response.Status == FormResponseStatus.Closed))
                    || (x.Policy.CompletionBasis == FormCompletionBasis.Approved
                        && (x.Response.Status == FormResponseStatus.Approved
                            || x.Response.Status == FormResponseStatus.Closed)))
                && nowUtc > (x.Response.DueAtUtcOverride ?? x.Cycle.DueAtUtc));
        }
        else if (filters.Overdue == false)
        {
            query = query.Where(x =>
                ((x.Policy.CompletionBasis == FormCompletionBasis.Submitted
                        && (x.Response.Status == FormResponseStatus.Submitted
                            || x.Response.Status == FormResponseStatus.UnderReview
                            || x.Response.Status == FormResponseStatus.Approved
                            || x.Response.Status == FormResponseStatus.Closed))
                    || (x.Policy.CompletionBasis == FormCompletionBasis.Approved
                        && (x.Response.Status == FormResponseStatus.Approved
                            || x.Response.Status == FormResponseStatus.Closed)))
                || nowUtc <= (x.Response.DueAtUtcOverride ?? x.Cycle.DueAtUtc));
        }

        return query;
    }
}
