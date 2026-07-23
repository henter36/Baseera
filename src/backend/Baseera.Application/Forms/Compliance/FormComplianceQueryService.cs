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
    private static readonly TimeSpan ReportingOffset = TimeZones.ToSaudi(DateTimeOffset.UnixEpoch).Offset;

    public async Task<FormComplianceSummaryDto> GetSummaryAsync(
        FormComplianceQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureViewPermission();
        var normalized = Normalize(query, isExport: false);
        await EnsureExplicitScopeFiltersAsync(normalized, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow();
        var aggregate = await AggregateAsync(BuildFilteredSource(normalized, nowUtc), cancellationToken);
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
        => await GetRegionsPageAsync(query, isExport: false, cancellationToken);

    private async Task<PagedResult<FormComplianceRegionRowDto>> GetRegionsPageAsync(
        FormComplianceQuery query,
        bool isExport,
        CancellationToken cancellationToken)
    {
        EnsureViewPermission();
        var normalized = Normalize(query, isExport);
        await EnsureExplicitScopeFiltersAsync(normalized, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow();
        var rows = BuildFilteredSource(normalized, nowUtc);
        var grouped = rows
            .GroupBy(r => new { r.RegionIdAtAssignment, r.RegionNameAtAssignment })
            .Select(g => new
            {
                g.Key.RegionIdAtAssignment,
                g.Key.RegionNameAtAssignment,
                Targeted = g.Count(),
                Eligible = g.Count(r => r.IsAvailable),
                Unavailable = g.Count(r => !r.IsAvailable),
                Completed = g.Count(r => r.IsAvailable && r.IsCompleted),
                Overdue = g.Count(r => r.IsAvailable && r.IsOverdue),
                NotStarted = g.Count(r => r.IsAvailable && r.ResponseStatus == null),
                Returned = g.Count(r => r.IsAvailable && r.ResponseStatus == FormResponseStatus.Returned),
                AverageMinutes = g
                    .Where(r => r.IsAvailable && r.IsCompleted && r.CompletionAtUtc.HasValue && (r.CompletionAtUtc ?? r.OpenAtUtc) >= r.OpenAtUtc)
                    .Average(r => (double?)((r.CompletionAtUtc ?? r.OpenAtUtc) - r.OpenAtUtc).TotalMinutes)
            });
        var ordered = grouped
            .OrderBy(r => r.Eligible == 0 ? 101m : decimal.Divide(r.Completed * 100m, r.Eligible))
            .ThenByDescending(r => r.Overdue)
            .ThenBy(r => r.RegionNameAtAssignment);
        var total = await ordered.CountAsync(cancellationToken);
        var page = ToPage(normalized);
        var items = await ordered.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return Page(items.Select((r, i) => new FormComplianceRegionRowDto
        {
            RegionIdAtAssignment = r.RegionIdAtAssignment,
            RegionNameAtAssignment = r.RegionNameAtAssignment,
            TargetedAssignmentCount = r.Targeted,
            UnavailableAssignmentCount = r.Unavailable,
            EligibleAssignmentCount = r.Eligible,
            CompletedCount = r.Completed,
            RemainingCount = FormComplianceRates.Remaining(r.Eligible, r.Completed),
            CompletionRate = FormComplianceRates.Rate(r.Completed, r.Eligible),
            OverdueCount = r.Overdue,
            NotStartedCount = r.NotStarted,
            ReturnedCount = r.Returned,
            AverageCompletionMinutes = r.AverageMinutes,
            Rank = ((page.Page - 1) * page.PageSize) + i + 1
        }).ToList(), page, total);
    }

    public async Task<PagedResult<FormComplianceFacilityRowDto>> GetFacilitiesAsync(
        FormComplianceQuery query,
        CancellationToken cancellationToken = default)
        => await GetFacilitiesPageAsync(query, isExport: false, cancellationToken);

    private async Task<PagedResult<FormComplianceFacilityRowDto>> GetFacilitiesPageAsync(
        FormComplianceQuery query,
        bool isExport,
        CancellationToken cancellationToken)
    {
        EnsureViewPermission();
        var normalized = Normalize(query, isExport);
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
            Responsible = g.OrderByDescending(r => r.LastSavedAtUtc ?? r.SubmittedAtUtc ?? DateTimeOffset.MinValue)
                .ThenByDescending(r => r.ResponseId ?? r.AssignmentId)
                .Select(r => new
                {
                    Id = r.LastSavedByUserId ?? r.SubmittedByUserId,
                    Name = r.LastSavedByUserId != null ? r.LastSavedByUserName : r.SubmittedByUserName
                })
                .FirstOrDefault()
        });
        var ordered = normalized.Sort?.Trim().Equals("overdue", StringComparison.OrdinalIgnoreCase) == true
            ? grouped.OrderByDescending(r => r.Overdue).ThenBy(r => r.FacilityNameAtAssignment)
            : grouped.OrderBy(r => r.FacilityNameAtAssignment);
        var page = ToPage(normalized);
        var total = await ordered.CountAsync(cancellationToken);
        var items = await ordered.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return Page(items.Select(r => new FormComplianceFacilityRowDto
        {
            FacilityId = r.FacilityId,
            FacilityCodeAtAssignment = r.FacilityCodeAtAssignment,
            FacilityNameAtAssignment = r.FacilityNameAtAssignment,
            RegionIdAtAssignment = r.RegionIdAtAssignment,
            RegionNameAtAssignment = r.RegionNameAtAssignment,
            CycleCount = r.CycleCount,
            EligibleAssignmentCount = r.Eligible,
            CompletedCount = r.Completed,
            RemainingCount = FormComplianceRates.Remaining(r.Eligible, r.Completed),
            CompletionRate = FormComplianceRates.Rate(r.Completed, r.Eligible),
            OverdueCount = r.Overdue,
            LatestEffectiveDueAtUtc = r.LatestDue,
            ResponsibleUserId = r.Responsible == null ? null : r.Responsible.Id,
            ResponsibleUserName = r.Responsible == null ? null : r.Responsible.Name,
            AllowedActions = ["open-response", "open-review", "view-history"]
        }).ToList(), page, total);
    }

    public async Task<PagedResult<FormComplianceCycleRowDto>> GetCyclesAsync(
        FormComplianceQuery query,
        CancellationToken cancellationToken = default)
        => await GetCyclesPageAsync(query, isExport: false, cancellationToken);

    private async Task<PagedResult<FormComplianceCycleRowDto>> GetCyclesPageAsync(
        FormComplianceQuery query,
        bool isExport,
        CancellationToken cancellationToken)
    {
        EnsureViewPermission();
        var normalized = Normalize(query, isExport);
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
            AverageMinutes = g
                .Where(r => r.IsAvailable && r.IsCompleted && r.CompletionAtUtc.HasValue && (r.CompletionAtUtc ?? r.OpenAtUtc) >= r.OpenAtUtc)
                .Average(r => (double?)((r.CompletionAtUtc ?? r.OpenAtUtc) - r.OpenAtUtc).TotalMinutes)
        }).OrderByDescending(r => r.ScheduledOccurrenceUtc).ThenBy(r => r.CampaignNameAr);
        var page = ToPage(normalized);
        var total = await grouped.CountAsync(cancellationToken);
        var items = await grouped.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        var lookups = items
            .Select(i => new PreviousCycleLookup(i.CycleId, i.CampaignId, i.SequenceNumber - 1))
            .Where(i => i.PreviousSequenceNumber > 0)
            .ToList();
        var previousRates = await LoadPreviousCycleRatesAsync(rows, lookups, cancellationToken);
        return Page(items.Select(r =>
        {
            var rate = FormComplianceRates.Rate(r.Completed, r.Eligible);
            previousRates.TryGetValue(r.CycleId, out var previousRate);
            return new FormComplianceCycleRowDto
            {
                CycleId = r.CycleId,
                CampaignId = r.CampaignId,
                CampaignCode = r.CampaignCode,
                CampaignNameAr = r.CampaignNameAr,
                SequenceNumber = r.SequenceNumber,
                OccurrenceKey = r.OccurrenceKey,
                ScheduledOccurrenceUtc = r.ScheduledOccurrenceUtc,
                OpenAtUtc = r.OpenAtUtc,
                DueAtUtc = r.DueAtUtc,
                CloseAtUtc = r.CloseAtUtc,
                CycleStatus = r.CycleStatus,
                CompletionBasis = r.CompletionBasis,
                TargetedAssignmentCount = r.Targeted,
                EligibleAssignmentCount = r.Eligible,
                CompletedCount = r.Completed,
                RemainingCount = FormComplianceRates.Remaining(r.Eligible, r.Completed),
                CompletionRate = rate,
                OverdueCount = r.Overdue,
                AverageCompletionMinutes = r.AverageMinutes,
                PreviousCycleCompletionRate = previousRate,
                CompletionRateDelta = rate.HasValue && previousRate.HasValue ? rate.Value - previousRate.Value : null
            };
        }).ToList(), page, total);
    }

    public async Task<PagedResult<FormCompliancePendingItemDto>> GetPendingAsync(
        FormComplianceQuery query,
        CancellationToken cancellationToken = default)
        => await GetPendingPageAsync(query, isExport: false, cancellationToken);

    private async Task<PagedResult<FormCompliancePendingItemDto>> GetPendingPageAsync(
        FormComplianceQuery query,
        bool isExport,
        CancellationToken cancellationToken)
    {
        EnsureViewPermission();
        var normalized = Normalize(query with { IsCompleted = false, IsAvailable = true }, isExport);
        await EnsureExplicitScopeFiltersAsync(normalized, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow();
        var rows = BuildFilteredSource(normalized, nowUtc)
            .OrderByDescending(r => r.IsOverdue)
            .ThenBy(r => r.EffectiveDueAtUtc)
            .ThenBy(r => r.FacilityNameAtAssignment);
        var page = ToPage(normalized);
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return Page(items.Select(r => new FormCompliancePendingItemDto
        {
            AssignmentId = r.AssignmentId,
            CampaignId = r.CampaignId,
            CampaignNameAr = r.CampaignNameAr,
            CycleId = r.CycleId,
            OccurrenceKey = r.OccurrenceKey,
            FacilityId = r.FacilityId,
            FacilityNameAtAssignment = r.FacilityNameAtAssignment,
            RegionIdAtAssignment = r.RegionIdAtAssignment,
            RegionNameAtAssignment = r.RegionNameAtAssignment,
            ResponseId = r.ResponseId,
            ResponseStatus = r.ResponseStatus,
            WorkStatus = FormResponseWorkStatusResolver.Resolve(r.ResponseStatus, r.IsOverdue),
            IsOverdue = r.IsOverdue,
            OpenAtUtc = r.OpenAtUtc,
            EffectiveDueAtUtc = r.EffectiveDueAtUtc,
            DaysOverdue = r.IsOverdue ? Math.Max(0, (int)Math.Floor((nowUtc - r.EffectiveDueAtUtc).TotalDays)) : null,
            LastSavedAtUtc = r.LastSavedAtUtc,
            SubmittedAtUtc = r.SubmittedAtUtc,
            ResponsibleUserId = r.LastSavedByUserId ?? r.SubmittedByUserId,
            ResponsibleUserName = r.LastSavedByUserId != null ? r.LastSavedByUserName : r.SubmittedByUserName,
            AllowedActions = ["open-response", "open-review", "view-history"]
        }).ToList(), page, total);
    }

    public async Task<IReadOnlyList<FormComplianceTrendPointDto>> GetTrendAsync(
        FormComplianceQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureViewPermission();
        var groupBy = query.GroupBy ?? FormComplianceTrendGroupBy.Cycle;
        var normalized = Normalize(query, isExport: false);
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
            FormComplianceExportView.Regions => await AppendRegionCsvAsync(csv, withLimit, cancellationToken),
            FormComplianceExportView.Cycles => await AppendCycleCsvAsync(csv, withLimit, cancellationToken),
            FormComplianceExportView.Pending => await AppendPendingCsvAsync(csv, withLimit, cancellationToken),
            _ => await AppendFacilityCsvAsync(csv, withLimit, cancellationToken)
        };
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
        var assignments = BuildScopedAssignmentsQuery();
        var filteredAssignments = ApplyAssignmentFilters(assignments, query);
        var projectedRows = BuildComplianceProjection(filteredAssignments, nowUtc);
        return ApplyCalculatedFilters(projectedRows, query);
    }

    private IQueryable<FormFacilityAssignment> BuildScopedAssignmentsQuery()
    {
        var facilityIds = orgScope.FilterFacilities(db.Facilities).Select(f => f.Id);
        return db.FormFacilityAssignments
            .AsNoTracking()
            .Where(a => facilityIds.Contains(a.FacilityId));
    }

    private static IQueryable<FormFacilityAssignment> ApplyAssignmentFilters(
        IQueryable<FormFacilityAssignment> source,
        FormComplianceQuery query)
    {
        source = ApplyAssignmentDateFilters(source, query);
        source = ApplyAssignmentIdentityFilters(source, query);
        source = ApplyAssignmentStatusFilters(source, query);
        return ApplyAssignmentSearchFilter(source, query);
    }

    private static IQueryable<FormFacilityAssignment> ApplyAssignmentDateFilters(
        IQueryable<FormFacilityAssignment> source,
        FormComplianceQuery query)
    {
        if (query.FromUtc.HasValue) source = source.Where(r => r.Cycle.ScheduledOccurrenceUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue) source = source.Where(r => r.Cycle.ScheduledOccurrenceUtc <= query.ToUtc.Value);
        return source;
    }

    private static IQueryable<FormFacilityAssignment> ApplyAssignmentIdentityFilters(
        IQueryable<FormFacilityAssignment> source,
        FormComplianceQuery query)
    {
        if (query.FormDefinitionId.HasValue) source = source.Where(r => r.Campaign.FormDefinitionId == query.FormDefinitionId);
        if (query.CampaignId.HasValue) source = source.Where(r => r.CampaignId == query.CampaignId);
        if (query.CycleId.HasValue) source = source.Where(r => r.CycleId == query.CycleId);
        if (query.RegionId.HasValue) source = source.Where(r => r.RegionIdAtAssignment == query.RegionId);
        if (query.FacilityId.HasValue) source = source.Where(r => r.FacilityId == query.FacilityId);
        return source;
    }

    private static IQueryable<FormFacilityAssignment> ApplyAssignmentStatusFilters(
        IQueryable<FormFacilityAssignment> source,
        FormComplianceQuery query)
    {
        source = query.CycleStatus.HasValue
            ? source.Where(r => r.Cycle.Status == query.CycleStatus)
            : source.Where(r => r.Cycle.Status != FormCycleStatus.Cancelled);
        if (query.CompletionBasis.HasValue)
        {
            source = source.Where(r =>
                (r.Campaign.ResponsePolicy == null ? FormCompletionBasis.Submitted : r.Campaign.ResponsePolicy.CompletionBasis)
                == query.CompletionBasis);
        }

        if (query.IsAvailable.HasValue) source = source.Where(r => r.IsAvailable == query.IsAvailable);
        return source;
    }

    private static IQueryable<FormFacilityAssignment> ApplyAssignmentSearchFilter(
        IQueryable<FormFacilityAssignment> source,
        FormComplianceQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            return source;
        }

        var term = query.Search.Trim();
        return source.Where(r =>
            r.Campaign.NameAr.Contains(term)
            || r.Campaign.Code.Contains(term)
            || r.FacilityNameArAtAssignment.Contains(term)
            || r.FacilityCodeAtAssignment.Contains(term)
            || r.RegionNameArAtAssignment.Contains(term));
    }

    private IQueryable<FormComplianceSourceRow> BuildComplianceProjection(
        IQueryable<FormFacilityAssignment> assignments,
        DateTimeOffset nowUtc)
    {
        var joined = BuildComplianceJoin(assignments);
        var facts = BuildComplianceFacts(joined);
        var responses = BuildResponseProjection(facts);
        return BuildComplianceSourceRows(responses, nowUtc);
    }

    private IQueryable<FormComplianceJoinRow> BuildComplianceJoin(
        IQueryable<FormFacilityAssignment> assignments) =>
        assignments.LeftJoin(
            db.FormResponses,
            assignment => assignment.Id,
            response => response.AssignmentId,
            (assignment, response) => new FormComplianceJoinRow
            {
                Assignment = assignment,
                Response = response
            });

    private static IQueryable<FormComplianceFactRow> BuildComplianceFacts(
        IQueryable<FormComplianceJoinRow> source)
    {
        return
            from row in source
            let assignment = row.Assignment
            let response = row.Response
            let completionBasis = assignment.Campaign.ResponsePolicy == null
                ? FormCompletionBasis.Submitted
                : assignment.Campaign.ResponsePolicy.CompletionBasis
            let responseStatus = response == null ? (FormResponseStatus?)null : response.Status
            let effectiveDueAtUtc = response != null && response.DueAtUtcOverride != null
                ? response.DueAtUtcOverride.Value
                : assignment.Cycle.DueAtUtc
            let completionAtUtc = completionBasis == FormCompletionBasis.Submitted
                ? response == null ? null : response.SubmittedAtUtc
                : response == null ? null : response.ApprovedAtUtc
            let submittedCompletion =
                responseStatus == FormResponseStatus.Submitted
                || responseStatus == FormResponseStatus.UnderReview
                || responseStatus == FormResponseStatus.Approved
                || responseStatus == FormResponseStatus.Closed
            let approvedCompletion =
                responseStatus == FormResponseStatus.Approved
                || responseStatus == FormResponseStatus.Closed
            let isCompleted = completionBasis == FormCompletionBasis.Submitted
                ? submittedCompletion
                : approvedCompletion
            select new FormComplianceFactRow
            {
                Assignment = assignment,
                Response = response,
                CompletionBasis = completionBasis,
                ResponseStatus = responseStatus,
                EffectiveDueAtUtc = effectiveDueAtUtc,
                CompletionAtUtc = completionAtUtc,
                IsCompleted = isCompleted
            };
    }

    private static IQueryable<FormComplianceResponseProjection> BuildResponseProjection(
        IQueryable<FormComplianceFactRow> source) =>
        source.Select(row => new FormComplianceResponseProjection
        {
            FactRow = row,
            ResponseId = row.Response == null ? null : row.Response.Id,
            LastSavedAtUtc = row.Response == null ? null : row.Response.LastSavedAtUtc,
            SubmittedAtUtc = row.Response == null ? null : row.Response.SubmittedAtUtc,
            LastSavedByUserId = row.Response == null ? null : row.Response.LastSavedByUserId,
            SubmittedByUserId = row.Response == null ? null : row.Response.SubmittedByUserId,
            LastSavedByUserName = row.Response == null || row.Response.LastSavedByUser == null
                ? null
                : row.Response.LastSavedByUser.DisplayNameAr,
            SubmittedByUserName = row.Response == null || row.Response.SubmittedByUser == null
                ? null
                : row.Response.SubmittedByUser.DisplayNameAr
        });

    private static IQueryable<FormComplianceSourceRow> BuildComplianceSourceRows(
        IQueryable<FormComplianceResponseProjection> source,
        DateTimeOffset nowUtc) =>
        source.Select(row => new FormComplianceSourceRow
            {
                AssignmentId = row.FactRow.Assignment.Id,
                CampaignId = row.FactRow.Assignment.CampaignId,
                FormDefinitionId = row.FactRow.Assignment.Campaign.FormDefinitionId,
                CycleId = row.FactRow.Assignment.CycleId,
                FacilityId = row.FactRow.Assignment.FacilityId,
                RegionIdAtAssignment = row.FactRow.Assignment.RegionIdAtAssignment,
                FacilityCodeAtAssignment = row.FactRow.Assignment.FacilityCodeAtAssignment,
                FacilityNameAtAssignment = row.FactRow.Assignment.FacilityNameArAtAssignment,
                RegionNameAtAssignment = row.FactRow.Assignment.RegionNameArAtAssignment,
                CampaignCode = row.FactRow.Assignment.Campaign.Code,
                CampaignNameAr = row.FactRow.Assignment.Campaign.NameAr,
                IsAvailable = row.FactRow.Assignment.IsAvailable,
                UnavailableReason = row.FactRow.Assignment.UnavailableReason,
                CycleStatus = row.FactRow.Assignment.Cycle.Status,
                CompletionBasis = row.FactRow.CompletionBasis,
                ResponseStatus = row.FactRow.ResponseStatus,
                ResponseId = row.ResponseId,
                OpenAtUtc = row.FactRow.Assignment.Cycle.OpenAtUtc,
                DueAtUtc = row.FactRow.Assignment.Cycle.DueAtUtc,
                CloseAtUtc = row.FactRow.Assignment.Cycle.CloseAtUtc,
                ScheduledOccurrenceUtc = row.FactRow.Assignment.Cycle.ScheduledOccurrenceUtc,
                SequenceNumber = row.FactRow.Assignment.Cycle.SequenceNumber,
                OccurrenceKey = row.FactRow.Assignment.Cycle.OccurrenceKey,
                EffectiveDueAtUtc = row.FactRow.EffectiveDueAtUtc,
                CompletionAtUtc = row.FactRow.CompletionAtUtc,
                LastSavedAtUtc = row.LastSavedAtUtc,
                SubmittedAtUtc = row.SubmittedAtUtc,
                LastSavedByUserId = row.LastSavedByUserId,
                SubmittedByUserId = row.SubmittedByUserId,
                LastSavedByUserName = row.LastSavedByUserName,
                SubmittedByUserName = row.SubmittedByUserName,
                IsCompleted = row.FactRow.IsCompleted,
                IsOverdue = row.FactRow.Assignment.IsAvailable
                    && !row.FactRow.IsCompleted
                    && nowUtc > row.FactRow.EffectiveDueAtUtc
            });

    private static IQueryable<FormComplianceSourceRow> ApplyCalculatedFilters(
        IQueryable<FormComplianceSourceRow> source,
        FormComplianceQuery query)
    {
        if (query.ResponseStatus.HasValue) source = source.Where(r => r.ResponseStatus == query.ResponseStatus);
        if (query.IsCompleted.HasValue) source = source.Where(r => r.IsCompleted == query.IsCompleted);
        if (query.IsOverdue.HasValue) source = source.Where(r => r.IsOverdue == query.IsOverdue);
        return source;
    }

    private static async Task<FormComplianceMetricAggregate> AggregateAsync(
        IQueryable<FormComplianceSourceRow> rows,
        CancellationToken cancellationToken)
    {
        var distinctFacilities = await rows.Select(r => r.FacilityId).Distinct().CountAsync(cancellationToken);
        var buckets = await LoadStatusBucketsAsync(rows, cancellationToken);
        var timing = await LoadTimingAggregateAsync(rows, cancellationToken);
        var eligibleBuckets = buckets.Where(b => b.IsAvailable).ToList();

        return new FormComplianceMetricAggregate(
            buckets.Sum(b => b.Count),
            distinctFacilities,
            buckets.Where(b => !b.IsAvailable).Sum(b => b.Count),
            eligibleBuckets.Sum(b => b.Count),
            eligibleBuckets.Where(b => b.IsCompleted).Sum(b => b.Count),
            CountStatus(eligibleBuckets, null),
            CountStatus(eligibleBuckets, FormResponseStatus.Draft),
            CountStatus(eligibleBuckets, FormResponseStatus.Submitted),
            CountStatus(eligibleBuckets, FormResponseStatus.UnderReview),
            CountStatus(eligibleBuckets, FormResponseStatus.Returned),
            CountStatus(eligibleBuckets, FormResponseStatus.Approved),
            CountStatus(eligibleBuckets, FormResponseStatus.Rejected),
            CountStatus(eligibleBuckets, FormResponseStatus.Closed),
            eligibleBuckets.Where(b => b.IsOverdue).Sum(b => b.Count),
            timing.CompletedOnTime,
            timing.CompletedLate,
            timing.AverageMinutes,
            timing.UnknownCompletionTimestamp,
            timing.InvalidCompletionDuration);
    }

    private static Task<List<ComplianceStatusBucket>> LoadStatusBucketsAsync(
        IQueryable<FormComplianceSourceRow> rows,
        CancellationToken cancellationToken) =>
        rows
            .GroupBy(r => new { r.IsAvailable, r.ResponseStatus, r.IsCompleted, r.IsOverdue })
            .Select(g => new ComplianceStatusBucket(
                g.Key.IsAvailable,
                g.Key.ResponseStatus,
                g.Key.IsCompleted,
                g.Key.IsOverdue,
                g.Count()))
            .ToListAsync(cancellationToken);

    private static async Task<ComplianceTimingAggregate> LoadTimingAggregateAsync(
        IQueryable<FormComplianceSourceRow> rows,
        CancellationToken cancellationToken)
    {
        var result = await rows
            .Where(r => r.IsAvailable && r.IsCompleted)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                CompletedOnTime = g.Count(r => r.CompletionAtUtc != null && r.CompletionAtUtc <= r.EffectiveDueAtUtc),
                CompletedLate = g.Count(r => r.CompletionAtUtc != null && r.CompletionAtUtc > r.EffectiveDueAtUtc),
                AverageMinutes = g
                    .Where(r => r.CompletionAtUtc != null && r.CompletionAtUtc >= r.OpenAtUtc)
                    .Average(r => (double?)((r.CompletionAtUtc ?? r.OpenAtUtc) - r.OpenAtUtc).TotalMinutes),
                UnknownCompletionTimestamp = g.Count(r => r.CompletionAtUtc == null),
                InvalidCompletionDuration = g.Count(r => r.CompletionAtUtc != null && r.CompletionAtUtc < r.OpenAtUtc)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return result is null
            ? new ComplianceTimingAggregate(0, 0, null, 0, 0)
            : new ComplianceTimingAggregate(
                result.CompletedOnTime,
                result.CompletedLate,
                result.AverageMinutes,
                result.UnknownCompletionTimestamp,
                result.InvalidCompletionDuration);
    }

    private static int CountStatus(
        IEnumerable<ComplianceStatusBucket> buckets,
        FormResponseStatus? status) =>
        buckets.Where(b => b.ResponseStatus == status).Sum(b => b.Count);

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
                    .Average(r => (double?)((r.CompletionAtUtc ?? r.OpenAtUtc) - r.OpenAtUtc).TotalMinutes)
            })
            .OrderBy(r => r.ScheduledOccurrenceUtc)
            .ToListAsync(cancellationToken);
        return data.Select(r => new FormComplianceTrendPointDto
        {
            OccurrenceUtc = r.ScheduledOccurrenceUtc,
            DateLocal = null,
            EligibleAssignmentCount = r.Eligible,
            CompletedCount = r.Completed,
            CompletionRate = FormComplianceRates.Rate(r.Completed, r.Eligible),
            OverdueCount = r.Overdue,
            AverageCompletionMinutes = r.AverageMinutes,
            CompletedThatDay = null,
            CumulativeCompleted = null,
            CumulativeCompletionRate = null
        }).ToList();
    }

    private async Task<IReadOnlyList<FormComplianceTrendPointDto>> BuildDayTrendAsync(
        IQueryable<FormComplianceSourceRow> rows,
        CancellationToken cancellationToken)
    {
        var eligible = await rows.CountAsync(cancellationToken);
        var daily = await rows.Where(r => r.IsCompleted && r.CompletionAtUtc != null)
            .GroupBy(r => (r.CompletionAtUtc ?? r.OpenAtUtc).Add(ReportingOffset).Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(r => r.Date)
            .ToListAsync(cancellationToken);
        var cumulative = 0;
        return daily.Select(r =>
        {
            cumulative += r.Count;
            return new FormComplianceTrendPointDto
            {
                OccurrenceUtc = null,
                DateLocal = DateOnly.FromDateTime(r.Date),
                EligibleAssignmentCount = eligible,
                CompletedCount = cumulative,
                CompletionRate = FormComplianceRates.Rate(cumulative, eligible),
                OverdueCount = 0,
                AverageCompletionMinutes = null,
                CompletedThatDay = r.Count,
                CumulativeCompleted = cumulative,
                CumulativeCompletionRate = FormComplianceRates.Rate(cumulative, eligible)
            };
        }).ToList();
    }

    private static async Task<IReadOnlyDictionary<Guid, decimal?>> LoadPreviousCycleRatesAsync(
        IQueryable<FormComplianceSourceRow> scopedRows,
        IReadOnlyCollection<PreviousCycleLookup> lookups,
        CancellationToken cancellationToken)
    {
        var result = lookups.ToDictionary(i => i.CurrentCycleId, _ => (decimal?)null);
        if (lookups.Count == 0)
        {
            return result;
        }

        var campaignIds = lookups.Select(i => i.CampaignId).Distinct().ToHashSet();
        var previousSequences = lookups.Select(i => i.PreviousSequenceNumber).Distinct().ToHashSet();
        var previousCycles = await scopedRows
            .Where(r => campaignIds.Contains(r.CampaignId) && previousSequences.Contains(r.SequenceNumber))
            .Select(r => new { r.CampaignId, r.SequenceNumber, r.CycleId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var previousCycleIds = previousCycles.Select(r => r.CycleId).Distinct().ToHashSet();
        var grouped = await scopedRows
            .Where(r => previousCycleIds.Contains(r.CycleId))
            .GroupBy(r => r.CycleId)
            .Select(g => new
            {
                CycleId = g.Key,
                Eligible = g.Count(r => r.IsAvailable),
                Completed = g.Count(r => r.IsAvailable && r.IsCompleted)
            })
            .ToListAsync(cancellationToken);
        var rates = grouped.ToDictionary(
            r => r.CycleId,
            r => FormComplianceRates.Rate(r.Completed, r.Eligible));
        foreach (var lookup in lookups)
        {
            var previousCycle = previousCycles.FirstOrDefault(r =>
                r.CampaignId == lookup.CampaignId &&
                r.SequenceNumber == lookup.PreviousSequenceNumber);
            if (previousCycle is not null && rates.TryGetValue(previousCycle.CycleId, out var rate))
            {
                result[lookup.CurrentCycleId] = rate;
            }
        }

        return result;
    }

    private async Task<int> AppendFacilityCsvAsync(
        StringBuilder csv,
        FormComplianceQuery query,
        CancellationToken cancellationToken)
    {
        csv.AppendLine("رمز الموقع,الموقع,المنطقة,الدورات,المؤهل,المكتمل,المتبقي,نسبة الالتزام,المتأخر,آخر موعد,المسؤول");
        var page = await GetFacilitiesPageAsync(query, isExport: true, cancellationToken);
        EnsureExportRowCount(page.TotalCount);
        foreach (var row in page.Items)
        {
            AppendCsv(csv, row.FacilityCodeAtAssignment, row.FacilityNameAtAssignment, row.RegionNameAtAssignment,
                row.CycleCount, row.EligibleAssignmentCount, row.CompletedCount, row.RemainingCount,
                row.CompletionRate, row.OverdueCount, row.LatestEffectiveDueAtUtc, row.ResponsibleUserName ?? "غير محدد");
        }

        return page.Items.Count;
    }

    private async Task<int> AppendRegionCsvAsync(
        StringBuilder csv,
        FormComplianceQuery query,
        CancellationToken cancellationToken)
    {
        csv.AppendLine("المنطقة,المستهدف,غير المتاح,المؤهل,المكتمل,المتبقي,نسبة الالتزام,المتأخر,لم يبدأ,المعاد,متوسط زمن الإكمال,الترتيب");
        var page = await GetRegionsPageAsync(query, isExport: true, cancellationToken);
        EnsureExportRowCount(page.TotalCount);
        foreach (var row in page.Items)
        {
            AppendCsv(csv, row.RegionNameAtAssignment, row.TargetedAssignmentCount,
                row.UnavailableAssignmentCount, row.EligibleAssignmentCount, row.CompletedCount,
                row.RemainingCount, row.CompletionRate, row.OverdueCount, row.NotStartedCount,
                row.ReturnedCount, row.AverageCompletionMinutes, row.Rank);
        }

        return page.Items.Count;
    }

    private async Task<int> AppendCycleCsvAsync(
        StringBuilder csv,
        FormComplianceQuery query,
        CancellationToken cancellationToken)
    {
        csv.AppendLine("رمز الحملة,الحملة,الدورة,تاريخ الفتح,تاريخ الاستحقاق,الحالة,سياسة الإكمال,المستهدف,المؤهل,المكتمل,المتبقي,نسبة الالتزام,المتأخر");
        var page = await GetCyclesPageAsync(query, isExport: true, cancellationToken);
        EnsureExportRowCount(page.TotalCount);
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
        var page = await GetPendingPageAsync(query, isExport: true, cancellationToken);
        EnsureExportRowCount(page.TotalCount);
        foreach (var row in page.Items)
        {
            AppendCsv(csv, row.FacilityNameAtAssignment, row.RegionNameAtAssignment, row.CampaignNameAr,
                row.OccurrenceKey, row.WorkStatus, row.EffectiveDueAtUtc, row.DaysOverdue,
                row.LastSavedAtUtc, row.ResponsibleUserName ?? "غير محدد");
        }

        return page.Items.Count;
    }

    private static void EnsureExportRowCount(int totalCount)
    {
        if (totalCount > ExportLimit)
        {
            throw new InvalidOperationException("EXPORT_ROW_LIMIT_EXCEEDED: تجاوز التصدير 50,000 صف. يرجى تضييق الفلاتر.");
        }
    }

    private static void AppendCsv(StringBuilder csv, params object?[] values)
    {
        csv.AppendLine(string.Join(",", values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(object? value)
    {
        var text = Convert.ToString(ToCsvValue(value), CultureInfo.InvariantCulture) ?? string.Empty;
        if (text.Length > 0 && "=+-@".Contains(text[0], StringComparison.Ordinal))
        {
            text = "'" + text;
        }

        return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static object? ToCsvValue(object? value) =>
        value switch
        {
            DateTimeOffset dateTime => TimeZones.ToSaudi(dateTime).ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            _ => value
        };

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

    private static FormComplianceQuery Normalize(FormComplianceQuery query, bool isExport)
    {
        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc.Value > query.ToUtc.Value)
        {
            throw new ArgumentException("الفترة غير صالحة: FromUtc يجب أن يسبق ToUtc.");
        }

        var page = Math.Max(1, query.Page ?? 1);
        var rawSize = query.PageSize ?? 20;
        var maxPageSize = isExport ? ExportLimit : 100;
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
