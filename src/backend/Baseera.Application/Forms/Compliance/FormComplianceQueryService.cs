namespace Baseera.Application.Forms.Compliance;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Application.Forms;
using Baseera.Application.Forms.Responses;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class FormComplianceQueryService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IOrganizationalScopeService orgScope,
    IAuditService audit,
    TimeProvider timeProvider,
    ILogger<FormComplianceQueryService> logger) : IFormComplianceQueryService
{
    private const int ExportLimit = 50000;

    public async Task<FormComplianceSummaryDto> GetSummaryAsync(
        FormComplianceQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureViewPermission();
        var normalized = Normalize(query);
        await EnsureExplicitScopeFiltersAsync(normalized, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow();
        var aggregate = await AggregateAsync(BuildFilteredSource(normalized, nowUtc), nowUtc, cancellationToken);
        var statusTotal = aggregate.NotStartedCount + aggregate.DraftCount + aggregate.SubmittedCount
            + aggregate.UnderReviewCount + aggregate.ReturnedCount + aggregate.ApprovedCount
            + aggregate.RejectedCount + aggregate.ClosedCount;
        var valid = statusTotal == aggregate.EligibleAssignmentCount;
        if (!valid)
        {
            logger.LogWarning(
                "Form compliance status reconciliation failed. Eligible={Eligible} StatusTotal={StatusTotal} CorrelationId={CorrelationId}",
                aggregate.EligibleAssignmentCount,
                statusTotal,
                currentUser.CorrelationId);
        }

        return new FormComplianceSummaryDto
        {
            TargetedAssignmentCount = aggregate.TargetedAssignmentCount,
            DistinctFacilityCount = aggregate.DistinctFacilityCount,
            UnavailableAssignmentCount = aggregate.UnavailableAssignmentCount,
            EligibleAssignmentCount = aggregate.EligibleAssignmentCount,
            CompletedCount = aggregate.CompletedCount,
            RemainingCount = FormComplianceRates.Remaining(aggregate.EligibleAssignmentCount, aggregate.CompletedCount),
            CompletionRate = FormComplianceRates.Rate(aggregate.CompletedCount, aggregate.EligibleAssignmentCount),
            NotStartedCount = aggregate.NotStartedCount,
            DraftCount = aggregate.DraftCount,
            SubmittedCount = aggregate.SubmittedCount,
            UnderReviewCount = aggregate.UnderReviewCount,
            ReturnedCount = aggregate.ReturnedCount,
            ApprovedCount = aggregate.ApprovedCount,
            RejectedCount = aggregate.RejectedCount,
            ClosedCount = aggregate.ClosedCount,
            OverdueCount = aggregate.OverdueCount,
            CompletedOnTimeCount = aggregate.CompletedOnTimeCount,
            CompletedLateCount = aggregate.CompletedLateCount,
            AverageCompletionMinutes = aggregate.AverageCompletionMinutes,
            UnknownCompletionTimestampCount = aggregate.UnknownCompletionTimestampCount,
            InvalidCompletionDurationCount = aggregate.InvalidCompletionDurationCount,
            StatusBucketTotal = statusTotal,
            StatusReconciliationValid = valid,
            GeneratedAtUtc = nowUtc
        };
    }

    public async Task<PagedResult<FormComplianceRegionRowDto>> GetRegionsAsync(
        FormComplianceQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureViewPermission();
        var normalized = Normalize(query);
        await EnsureExplicitScopeFiltersAsync(normalized, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow();
        var rows = BuildFilteredSource(normalized, nowUtc).Where(r => r.IsAvailable);
        var grouped = rows
            .GroupBy(r => new { r.RegionIdAtAssignment, r.RegionNameAtAssignment })
            .Select(g => new
            {
                g.Key.RegionIdAtAssignment,
                g.Key.RegionNameAtAssignment,
                Targeted = g.Count(),
                Eligible = g.Count(),
                Completed = g.Count(r => r.IsCompleted),
                Overdue = g.Count(r => r.IsOverdue),
                NotStarted = g.Count(r => r.ResponseStatus == null),
                Returned = g.Count(r => r.ResponseStatus == FormResponseStatus.Returned),
                AverageMinutes = g.Where(r => r.IsCompleted && r.CompletionAtUtc != null && r.CompletionAtUtc >= r.OpenAtUtc)
                    .Average(r => (double?)(r.CompletionAtUtc!.Value - r.OpenAtUtc).TotalMinutes)
            });
        var ordered = grouped
            .OrderBy(r => r.Eligible == 0 ? 101m : decimal.Divide(r.Completed * 100m, r.Eligible))
            .ThenByDescending(r => r.Overdue)
            .ThenBy(r => r.RegionNameAtAssignment);
        var total = await ordered.CountAsync(cancellationToken);
        var page = ToPage(normalized);
        var items = await ordered.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return Page(items.Select((r, i) => new FormComplianceRegionRowDto(
            r.RegionIdAtAssignment,
            r.RegionNameAtAssignment,
            r.Targeted,
            0,
            r.Eligible,
            r.Completed,
            FormComplianceRates.Remaining(r.Eligible, r.Completed),
            FormComplianceRates.Rate(r.Completed, r.Eligible),
            r.Overdue,
            r.NotStarted,
            r.Returned,
            r.AverageMinutes,
            ((page.Page - 1) * page.PageSize) + i + 1)).ToList(), page, total);
    }

    public async Task<PagedResult<FormComplianceFacilityRowDto>> GetFacilitiesAsync(
        FormComplianceQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureViewPermission();
        var normalized = Normalize(query);
        await EnsureExplicitScopeFiltersAsync(normalized, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow();
        var rows = BuildFilteredSource(normalized, nowUtc).Where(r => r.IsAvailable);
        var grouped = rows.GroupBy(r => new
        {
            r.FacilityId,
            r.FacilityCodeAtAssignment,
            r.FacilityNameAtAssignment,
            r.RegionIdAtAssignment,
            r.RegionNameAtAssignment
        }).Select(g => new
        {
            g.Key.FacilityId,
            g.Key.FacilityCodeAtAssignment,
            g.Key.FacilityNameAtAssignment,
            g.Key.RegionIdAtAssignment,
            g.Key.RegionNameAtAssignment,
            CycleCount = g.Select(r => r.CycleId).Distinct().Count(),
            Eligible = g.Count(),
            Completed = g.Count(r => r.IsCompleted),
            Overdue = g.Count(r => r.IsOverdue),
            LatestDue = g.Max(r => (DateTimeOffset?)r.EffectiveDueAtUtc),
            ResponsibleUserId = g.OrderByDescending(r => r.LastSavedAtUtc ?? r.SubmittedAtUtc)
                .Select(r => r.LastSavedByUserId ?? r.SubmittedByUserId)
                .FirstOrDefault(),
            ResponsibleUserName = g.OrderByDescending(r => r.LastSavedAtUtc ?? r.SubmittedAtUtc)
                .Select(r => r.LastSavedByUserName ?? r.SubmittedByUserName)
                .FirstOrDefault()
        });
        var ordered = normalized.Sort?.Trim().Equals("overdue", StringComparison.OrdinalIgnoreCase) == true
            ? grouped.OrderByDescending(r => r.Overdue).ThenBy(r => r.FacilityNameAtAssignment)
            : grouped.OrderBy(r => r.FacilityNameAtAssignment);
        var page = ToPage(normalized);
        var total = await ordered.CountAsync(cancellationToken);
        var items = await ordered.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return Page(items.Select(r => new FormComplianceFacilityRowDto(
            r.FacilityId,
            r.FacilityCodeAtAssignment,
            r.FacilityNameAtAssignment,
            r.RegionIdAtAssignment,
            r.RegionNameAtAssignment,
            r.CycleCount,
            r.Eligible,
            r.Completed,
            FormComplianceRates.Remaining(r.Eligible, r.Completed),
            FormComplianceRates.Rate(r.Completed, r.Eligible),
            r.Overdue,
            r.LatestDue,
            r.ResponsibleUserId,
            r.ResponsibleUserName,
            ["open-response", "open-review", "view-history"])).ToList(), page, total);
    }

    public async Task<PagedResult<FormComplianceCycleRowDto>> GetCyclesAsync(
        FormComplianceQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureViewPermission();
        var normalized = Normalize(query);
        await EnsureExplicitScopeFiltersAsync(normalized, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow();
        var rows = BuildFilteredSource(normalized, nowUtc);
        var grouped = rows.GroupBy(r => new
        {
            r.CycleId,
            r.CampaignId,
            r.CampaignCode,
            r.CampaignNameAr,
            r.SequenceNumber,
            r.OccurrenceKey,
            r.ScheduledOccurrenceUtc,
            r.OpenAtUtc,
            r.DueAtUtc,
            r.CloseAtUtc,
            r.CycleStatus,
            r.CompletionBasis
        }).Select(g => new
        {
            g.Key.CycleId,
            g.Key.CampaignId,
            g.Key.CampaignCode,
            g.Key.CampaignNameAr,
            g.Key.SequenceNumber,
            g.Key.OccurrenceKey,
            g.Key.ScheduledOccurrenceUtc,
            g.Key.OpenAtUtc,
            g.Key.DueAtUtc,
            g.Key.CloseAtUtc,
            g.Key.CycleStatus,
            g.Key.CompletionBasis,
            Targeted = g.Count(),
            Eligible = g.Count(r => r.IsAvailable),
            Completed = g.Count(r => r.IsAvailable && r.IsCompleted),
            Overdue = g.Count(r => r.IsAvailable && r.IsOverdue),
            AverageMinutes = g.Where(r => r.IsAvailable && r.IsCompleted && r.CompletionAtUtc != null && r.CompletionAtUtc >= r.OpenAtUtc)
                .Average(r => (double?)(r.CompletionAtUtc!.Value - r.OpenAtUtc).TotalMinutes)
        }).OrderByDescending(r => r.ScheduledOccurrenceUtc).ThenBy(r => r.CampaignNameAr);
        var page = ToPage(normalized);
        var total = await grouped.CountAsync(cancellationToken);
        var items = await grouped.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        var cycleIds = items.Select(i => i.CycleId).ToHashSet();
        var previousRates = await LoadPreviousCycleRatesAsync(rows, cycleIds, cancellationToken);
        return Page(items.Select(r =>
        {
            var rate = FormComplianceRates.Rate(r.Completed, r.Eligible);
            previousRates.TryGetValue(r.CycleId, out var previousRate);
            return new FormComplianceCycleRowDto(
                r.CycleId,
                r.CampaignId,
                r.CampaignCode,
                r.CampaignNameAr,
                r.SequenceNumber,
                r.OccurrenceKey,
                r.ScheduledOccurrenceUtc,
                r.OpenAtUtc,
                r.DueAtUtc,
                r.CloseAtUtc,
                r.CycleStatus,
                r.CompletionBasis,
                r.Targeted,
                r.Eligible,
                r.Completed,
                FormComplianceRates.Remaining(r.Eligible, r.Completed),
                rate,
                r.Overdue,
                r.AverageMinutes,
                previousRate,
                rate.HasValue && previousRate.HasValue ? rate.Value - previousRate.Value : null);
        }).ToList(), page, total);
    }

    public async Task<PagedResult<FormCompliancePendingItemDto>> GetPendingAsync(
        FormComplianceQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureViewPermission();
        var normalized = Normalize(query with { IsCompleted = false, IsAvailable = true });
        await EnsureExplicitScopeFiltersAsync(normalized, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow();
        var rows = BuildFilteredSource(normalized, nowUtc)
            .OrderByDescending(r => r.IsOverdue)
            .ThenBy(r => r.EffectiveDueAtUtc)
            .ThenBy(r => r.FacilityNameAtAssignment);
        var page = ToPage(normalized);
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return Page(items.Select(r => new FormCompliancePendingItemDto(
            r.AssignmentId,
            r.CampaignId,
            r.CampaignNameAr,
            r.CycleId,
            r.OccurrenceKey,
            r.FacilityId,
            r.FacilityNameAtAssignment,
            r.RegionIdAtAssignment,
            r.RegionNameAtAssignment,
            r.ResponseId,
            r.ResponseStatus,
            FormResponseWorkStatusResolver.Resolve(r.ResponseStatus, r.IsOverdue),
            r.IsOverdue,
            r.OpenAtUtc,
            r.EffectiveDueAtUtc,
            r.IsOverdue ? Math.Max(0, (int)Math.Floor((nowUtc - r.EffectiveDueAtUtc).TotalDays)) : null,
            r.LastSavedAtUtc,
            r.SubmittedAtUtc,
            r.LastSavedByUserId ?? r.SubmittedByUserId,
            r.LastSavedByUserName ?? r.SubmittedByUserName,
            ["open-response", "open-review", "view-history"])).ToList(), page, total);
    }

    public async Task<IReadOnlyList<FormComplianceTrendPointDto>> GetTrendAsync(
        FormComplianceQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureViewPermission();
        var groupBy = query.GroupBy ?? FormComplianceTrendGroupBy.Cycle;
        var normalized = Normalize(query);
        await EnsureExplicitScopeFiltersAsync(normalized, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow();
        var rows = BuildFilteredSource(normalized, nowUtc).Where(r => r.IsAvailable);
        return groupBy == FormComplianceTrendGroupBy.Day
            ? await BuildDayTrendAsync(rows, cancellationToken)
            : await BuildCycleTrendAsync(rows, cancellationToken);
    }

    public async Task<FormComplianceExportResult> ExportCsvAsync(
        FormComplianceQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureExportPermission();
        var view = query.View ?? FormComplianceExportView.Facilities;
        var withLimit = query with { Page = 1, PageSize = ExportLimit };
        var csv = new StringBuilder("\uFEFF");
        var rowCount = view switch
        {
            FormComplianceExportView.Cycles => await AppendCycleCsvAsync(csv, withLimit, cancellationToken),
            FormComplianceExportView.Pending => await AppendPendingCsvAsync(csv, withLimit, cancellationToken),
            _ => await AppendFacilityCsvAsync(csv, withLimit, cancellationToken)
        };
        if (rowCount >= ExportLimit)
        {
            throw new InvalidOperationException("EXPORT_ROW_LIMIT_EXCEEDED: تجاوز التصدير 50,000 صف. يرجى تضييق الفلاتر.");
        }

        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormComplianceExported",
            Module = FormAccessHelper.ModuleName,
            EntityType = "FormComplianceDashboard",
            NewValues = new
            {
                UserId = currentUser.UserId,
                Scope = orgScope.SummarizeScopes(),
                FiltersHash = HashFilters(query),
                View = view.ToString(),
                RowCount = rowCount,
                GeneratedAtUtc = timeProvider.GetUtcNow()
            }
        }, cancellationToken);
        return new FormComplianceExportResult(
            $"form-compliance-{view.ToString().ToLowerInvariant()}.csv",
            "text/csv; charset=utf-8",
            Encoding.UTF8.GetBytes(csv.ToString()),
            rowCount);
    }

    private IQueryable<FormComplianceSourceRow> BuildFilteredSource(FormComplianceQuery query, DateTimeOffset nowUtc)
    {
        var facilityIds = orgScope.FilterFacilities(db.Facilities).Select(f => f.Id);
        var source = db.FormFacilityAssignments
            .AsNoTracking()
            .Where(a => facilityIds.Contains(a.FacilityId))
            .SelectMany(
                a => db.FormResponses.Where(r => r.AssignmentId == a.Id).DefaultIfEmpty(),
                (a, response) => new { Assignment = a, Response = response })
            .Select(x => new FormComplianceSourceRow
            {
                AssignmentId = x.Assignment.Id,
                CampaignId = x.Assignment.CampaignId,
                FormDefinitionId = x.Assignment.Campaign.FormDefinitionId,
                CycleId = x.Assignment.CycleId,
                FacilityId = x.Assignment.FacilityId,
                RegionIdAtAssignment = x.Assignment.RegionIdAtAssignment,
                FacilityCodeAtAssignment = x.Assignment.FacilityCodeAtAssignment,
                FacilityNameAtAssignment = x.Assignment.FacilityNameArAtAssignment,
                RegionNameAtAssignment = x.Assignment.RegionNameArAtAssignment,
                CampaignCode = x.Assignment.Campaign.Code,
                CampaignNameAr = x.Assignment.Campaign.NameAr,
                IsAvailable = x.Assignment.IsAvailable,
                UnavailableReason = x.Assignment.UnavailableReason,
                CycleStatus = x.Assignment.Cycle.Status,
                CompletionBasis = x.Assignment.Campaign.ResponsePolicy == null
                    ? FormCompletionBasis.Submitted
                    : x.Assignment.Campaign.ResponsePolicy.CompletionBasis,
                ResponseStatus = x.Response == null ? null : x.Response.Status,
                ResponseId = x.Response == null ? null : x.Response.Id,
                OpenAtUtc = x.Assignment.Cycle.OpenAtUtc,
                DueAtUtc = x.Assignment.Cycle.DueAtUtc,
                CloseAtUtc = x.Assignment.Cycle.CloseAtUtc,
                ScheduledOccurrenceUtc = x.Assignment.Cycle.ScheduledOccurrenceUtc,
                SequenceNumber = x.Assignment.Cycle.SequenceNumber,
                OccurrenceKey = x.Assignment.Cycle.OccurrenceKey,
                EffectiveDueAtUtc = x.Response != null && x.Response.DueAtUtcOverride != null
                    ? x.Response.DueAtUtcOverride.Value
                    : x.Assignment.Cycle.DueAtUtc,
                CompletionAtUtc = (x.Assignment.Campaign.ResponsePolicy == null
                        || x.Assignment.Campaign.ResponsePolicy.CompletionBasis == FormCompletionBasis.Submitted)
                    ? (x.Response == null ? null : x.Response.SubmittedAtUtc)
                    : (x.Response == null ? null : x.Response.ApprovedAtUtc),
                LastSavedAtUtc = x.Response == null ? null : x.Response.LastSavedAtUtc,
                SubmittedAtUtc = x.Response == null ? null : x.Response.SubmittedAtUtc,
                LastSavedByUserId = x.Response == null ? null : x.Response.LastSavedByUserId,
                SubmittedByUserId = x.Response == null ? null : x.Response.SubmittedByUserId,
                LastSavedByUserName = x.Response != null && x.Response.LastSavedByUser != null ? x.Response.LastSavedByUser.DisplayNameAr : null,
                SubmittedByUserName = x.Response != null && x.Response.SubmittedByUser != null ? x.Response.SubmittedByUser.DisplayNameAr : null,
                IsCompleted = x.Response != null
                    && ((x.Assignment.Campaign.ResponsePolicy == null
                            || x.Assignment.Campaign.ResponsePolicy.CompletionBasis == FormCompletionBasis.Submitted)
                        && (x.Response.Status == FormResponseStatus.Submitted
                            || x.Response.Status == FormResponseStatus.UnderReview
                            || x.Response.Status == FormResponseStatus.Approved
                            || x.Response.Status == FormResponseStatus.Closed)
                        || (x.Assignment.Campaign.ResponsePolicy != null
                            && x.Assignment.Campaign.ResponsePolicy.CompletionBasis == FormCompletionBasis.Approved
                            && (x.Response.Status == FormResponseStatus.Approved
                                || x.Response.Status == FormResponseStatus.Closed))),
                IsOverdue = x.Response != null
                    ? !(((x.Assignment.Campaign.ResponsePolicy == null
                                || x.Assignment.Campaign.ResponsePolicy.CompletionBasis == FormCompletionBasis.Submitted)
                            && (x.Response.Status == FormResponseStatus.Submitted
                                || x.Response.Status == FormResponseStatus.UnderReview
                                || x.Response.Status == FormResponseStatus.Approved
                                || x.Response.Status == FormResponseStatus.Closed))
                        || (x.Assignment.Campaign.ResponsePolicy != null
                            && x.Assignment.Campaign.ResponsePolicy.CompletionBasis == FormCompletionBasis.Approved
                            && (x.Response.Status == FormResponseStatus.Approved
                                || x.Response.Status == FormResponseStatus.Closed)))
                        && nowUtc > (x.Response.DueAtUtcOverride ?? x.Assignment.Cycle.DueAtUtc)
                    : nowUtc > x.Assignment.Cycle.DueAtUtc
            });

        return ApplyFilters(source, query);
    }

    private static IQueryable<FormComplianceSourceRow> ApplyFilters(
        IQueryable<FormComplianceSourceRow> source,
        FormComplianceQuery query)
    {
        if (query.FromUtc.HasValue) source = source.Where(r => r.ScheduledOccurrenceUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue) source = source.Where(r => r.ScheduledOccurrenceUtc <= query.ToUtc.Value);
        if (query.FormDefinitionId.HasValue) source = source.Where(r => r.FormDefinitionId == query.FormDefinitionId);
        if (query.CampaignId.HasValue) source = source.Where(r => r.CampaignId == query.CampaignId);
        if (query.CycleId.HasValue) source = source.Where(r => r.CycleId == query.CycleId);
        if (query.RegionId.HasValue) source = source.Where(r => r.RegionIdAtAssignment == query.RegionId);
        if (query.FacilityId.HasValue) source = source.Where(r => r.FacilityId == query.FacilityId);
        if (query.CycleStatus.HasValue) source = source.Where(r => r.CycleStatus == query.CycleStatus);
        else source = source.Where(r => r.CycleStatus != FormCycleStatus.Cancelled);
        if (query.CompletionBasis.HasValue) source = source.Where(r => r.CompletionBasis == query.CompletionBasis);
        if (query.ResponseStatus.HasValue) source = source.Where(r => r.ResponseStatus == query.ResponseStatus);
        if (query.IsCompleted.HasValue) source = source.Where(r => r.IsCompleted == query.IsCompleted);
        if (query.IsOverdue.HasValue) source = source.Where(r => r.IsOverdue == query.IsOverdue);
        if (query.IsAvailable.HasValue) source = source.Where(r => r.IsAvailable == query.IsAvailable);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            source = source.Where(r =>
                r.CampaignNameAr.Contains(term)
                || r.CampaignCode.Contains(term)
                || r.FacilityNameAtAssignment.Contains(term)
                || r.FacilityCodeAtAssignment.Contains(term)
                || r.RegionNameAtAssignment.Contains(term));
        }

        return source;
    }

    private async Task<FormComplianceMetricAggregate> AggregateAsync(
        IQueryable<FormComplianceSourceRow> rows,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var targeted = await rows.CountAsync(cancellationToken);
        var distinctFacilities = await rows.Select(r => r.FacilityId).Distinct().CountAsync(cancellationToken);
        var unavailable = await rows.CountAsync(r => !r.IsAvailable, cancellationToken);
        var eligibleRows = rows.Where(r => r.IsAvailable);
        var eligible = await eligibleRows.CountAsync(cancellationToken);
        var completed = await eligibleRows.CountAsync(r => r.IsCompleted, cancellationToken);
        var completionRows = eligibleRows.Where(r => r.IsCompleted);
        var validDurations = completionRows.Where(r => r.CompletionAtUtc != null && r.CompletionAtUtc >= r.OpenAtUtc);
        return new FormComplianceMetricAggregate(
            targeted,
            distinctFacilities,
            unavailable,
            eligible,
            completed,
            await eligibleRows.CountAsync(r => r.ResponseStatus == null, cancellationToken),
            await eligibleRows.CountAsync(r => r.ResponseStatus == FormResponseStatus.Draft, cancellationToken),
            await eligibleRows.CountAsync(r => r.ResponseStatus == FormResponseStatus.Submitted, cancellationToken),
            await eligibleRows.CountAsync(r => r.ResponseStatus == FormResponseStatus.UnderReview, cancellationToken),
            await eligibleRows.CountAsync(r => r.ResponseStatus == FormResponseStatus.Returned, cancellationToken),
            await eligibleRows.CountAsync(r => r.ResponseStatus == FormResponseStatus.Approved, cancellationToken),
            await eligibleRows.CountAsync(r => r.ResponseStatus == FormResponseStatus.Rejected, cancellationToken),
            await eligibleRows.CountAsync(r => r.ResponseStatus == FormResponseStatus.Closed, cancellationToken),
            await eligibleRows.CountAsync(r => r.IsOverdue, cancellationToken),
            await completionRows.CountAsync(r => r.CompletionAtUtc != null && r.CompletionAtUtc <= r.EffectiveDueAtUtc, cancellationToken),
            await completionRows.CountAsync(r => r.CompletionAtUtc != null && r.CompletionAtUtc > r.EffectiveDueAtUtc, cancellationToken),
            await validDurations.AverageAsync(r => (double?)(r.CompletionAtUtc!.Value - r.OpenAtUtc).TotalMinutes, cancellationToken),
            await completionRows.CountAsync(r => r.CompletionAtUtc == null, cancellationToken),
            await completionRows.CountAsync(r => r.CompletionAtUtc != null && r.CompletionAtUtc < r.OpenAtUtc, cancellationToken));
    }

    private async Task<IReadOnlyList<FormComplianceTrendPointDto>> BuildCycleTrendAsync(
        IQueryable<FormComplianceSourceRow> rows,
        CancellationToken cancellationToken)
    {
        var data = await rows.GroupBy(r => new { r.CycleId, r.ScheduledOccurrenceUtc })
            .Select(g => new
            {
                g.Key.ScheduledOccurrenceUtc,
                Eligible = g.Count(),
                Completed = g.Count(r => r.IsCompleted),
                Overdue = g.Count(r => r.IsOverdue),
                AverageMinutes = g.Where(r => r.IsCompleted && r.CompletionAtUtc != null && r.CompletionAtUtc >= r.OpenAtUtc)
                    .Average(r => (double?)(r.CompletionAtUtc!.Value - r.OpenAtUtc).TotalMinutes)
            })
            .OrderBy(r => r.ScheduledOccurrenceUtc)
            .ToListAsync(cancellationToken);
        return data.Select(r => new FormComplianceTrendPointDto(
            r.ScheduledOccurrenceUtc,
            null,
            r.Eligible,
            r.Completed,
            FormComplianceRates.Rate(r.Completed, r.Eligible),
            r.Overdue,
            r.AverageMinutes,
            null,
            null,
            null)).ToList();
    }

    private async Task<IReadOnlyList<FormComplianceTrendPointDto>> BuildDayTrendAsync(
        IQueryable<FormComplianceSourceRow> rows,
        CancellationToken cancellationToken)
    {
        var eligible = await rows.CountAsync(cancellationToken);
        var daily = await rows.Where(r => r.IsCompleted && r.CompletionAtUtc != null)
            .GroupBy(r => r.CompletionAtUtc!.Value.AddHours(3).Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(r => r.Date)
            .ToListAsync(cancellationToken);
        var cumulative = 0;
        return daily.Select(r =>
        {
            cumulative += r.Count;
            return new FormComplianceTrendPointDto(
                null,
                DateOnly.FromDateTime(r.Date),
                eligible,
                cumulative,
                FormComplianceRates.Rate(cumulative, eligible),
                0,
                null,
                r.Count,
                cumulative,
                FormComplianceRates.Rate(cumulative, eligible));
        }).ToList();
    }

    private async Task<Dictionary<Guid, decimal?>> LoadPreviousCycleRatesAsync(
        IQueryable<FormComplianceSourceRow> scopedRows,
        HashSet<Guid> cycleIds,
        CancellationToken cancellationToken)
    {
        var current = await scopedRows
            .Where(r => cycleIds.Contains(r.CycleId))
            .Select(r => new { r.CycleId, r.CampaignId, r.SequenceNumber })
            .Distinct()
            .ToListAsync(cancellationToken);
        var result = new Dictionary<Guid, decimal?>();
        foreach (var row in current)
        {
            var previous = scopedRows.Where(r => r.CampaignId == row.CampaignId && r.SequenceNumber == row.SequenceNumber - 1 && r.IsAvailable);
            var eligible = await previous.CountAsync(cancellationToken);
            var completed = await previous.CountAsync(r => r.IsCompleted, cancellationToken);
            result[row.CycleId] = FormComplianceRates.Rate(completed, eligible);
        }

        return result;
    }

    private async Task<int> AppendFacilityCsvAsync(
        StringBuilder csv,
        FormComplianceQuery query,
        CancellationToken cancellationToken)
    {
        csv.AppendLine("رمز الموقع,الموقع,المنطقة,الدورات,المؤهل,المكتمل,المتبقي,نسبة الالتزام,المتأخر,آخر موعد,المسؤول");
        var page = await GetFacilitiesAsync(query, cancellationToken);
        foreach (var row in page.Items)
        {
            AppendCsv(csv, row.FacilityCodeAtAssignment, row.FacilityNameAtAssignment, row.RegionNameAtAssignment,
                row.CycleCount, row.EligibleAssignmentCount, row.CompletedCount, row.RemainingCount,
                row.CompletionRate, row.OverdueCount, row.LatestEffectiveDueAtUtc, row.ResponsibleUserName ?? "غير محدد");
        }

        return page.Items.Count;
    }

    private async Task<int> AppendCycleCsvAsync(
        StringBuilder csv,
        FormComplianceQuery query,
        CancellationToken cancellationToken)
    {
        csv.AppendLine("رمز الحملة,الحملة,الدورة,تاريخ الفتح,تاريخ الاستحقاق,الحالة,سياسة الإكمال,المستهدف,المؤهل,المكتمل,المتبقي,نسبة الالتزام,المتأخر");
        var page = await GetCyclesAsync(query, cancellationToken);
        foreach (var row in page.Items)
        {
            AppendCsv(csv, row.CampaignCode, row.CampaignNameAr, row.OccurrenceKey, row.OpenAtUtc,
                row.DueAtUtc, row.CycleStatus, row.CompletionBasis, row.TargetedAssignmentCount,
                row.EligibleAssignmentCount, row.CompletedCount, row.RemainingCount, row.CompletionRate, row.OverdueCount);
        }

        return page.Items.Count;
    }

    private async Task<int> AppendPendingCsvAsync(
        StringBuilder csv,
        FormComplianceQuery query,
        CancellationToken cancellationToken)
    {
        csv.AppendLine("الموقع,المنطقة,الحملة,الدورة,الحالة,الموعد,أيام التأخر,آخر حفظ,المسؤول");
        var page = await GetPendingAsync(query, cancellationToken);
        foreach (var row in page.Items)
        {
            AppendCsv(csv, row.FacilityNameAtAssignment, row.RegionNameAtAssignment, row.CampaignNameAr,
                row.OccurrenceKey, row.WorkStatus, row.EffectiveDueAtUtc, row.DaysOverdue,
                row.LastSavedAtUtc, row.ResponsibleUserName ?? "غير محدد");
        }

        return page.Items.Count;
    }

    private static void AppendCsv(StringBuilder csv, params object?[] values)
    {
        csv.AppendLine(string.Join(",", values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (text.Length > 0 && "=+-@".Contains(text[0], StringComparison.Ordinal))
        {
            text = "'" + text;
        }

        return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static string HashFilters(FormComplianceQuery query)
    {
        var payload = string.Join("|", query.FromUtc, query.ToUtc, query.FormDefinitionId, query.CampaignId,
            query.CycleId, query.RegionId, query.FacilityId, query.CycleStatus, query.CompletionBasis,
            query.ResponseStatus, query.IsCompleted, query.IsOverdue, query.IsAvailable, query.View);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private async Task EnsureExplicitScopeFiltersAsync(FormComplianceQuery query, CancellationToken cancellationToken)
    {
        if (query.FacilityId.HasValue && !orgScope.CanAccessFacility(query.FacilityId.Value))
        {
            throw new KeyNotFoundException("الموقع غير موجود.");
        }

        if (query.RegionId.HasValue)
        {
            var exists = await orgScope.FilterFacilities(db.Facilities)
                .AnyAsync(f => f.RegionId == query.RegionId.Value, cancellationToken);
            if (!exists && !orgScope.CanAccessRegion(query.RegionId.Value))
            {
                throw new KeyNotFoundException("المنطقة غير موجودة.");
            }
        }
    }

    private static FormComplianceQuery Normalize(FormComplianceQuery query)
    {
        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc.Value > query.ToUtc.Value)
        {
            throw new ArgumentException("الفترة غير صالحة: FromUtc يجب أن يسبق ToUtc.");
        }

        var page = Math.Max(1, query.Page ?? 1);
        var rawSize = query.PageSize ?? 20;
        var maxPageSize = query.View.HasValue ? ExportLimit : 100;
        return query with
        {
            Page = page,
            PageSize = Math.Clamp(rawSize <= 0 ? 20 : rawSize, 1, maxPageSize),
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim()
        };
    }

    private static FormCompliancePage ToPage(FormComplianceQuery query) =>
        new(query.Page ?? 1, query.PageSize ?? 20, query.Search);

    private static PagedResult<T> Page<T>(IReadOnlyList<T> items, FormCompliancePage page, int total) =>
        new() { Items = items, Page = page.Page, PageSize = page.PageSize, TotalCount = total };

    private void EnsureViewPermission()
    {
        if (!currentUser.HasPermission(PermissionCodes.FormsViewComplianceDashboard))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية عرض لوحة التزام النماذج.");
        }
    }

    private void EnsureExportPermission()
    {
        if (!currentUser.HasPermission(PermissionCodes.FormsExportComplianceDashboard))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تصدير لوحة التزام النماذج.");
        }
    }
}
