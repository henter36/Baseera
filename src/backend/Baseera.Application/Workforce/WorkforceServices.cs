namespace Baseera.Application.Workforce;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Baseera.Domain.Workforce;
using Microsoft.EntityFrameworkCore;

public interface IWorkforceReadinessQueryService
{
    Task<WorkforceWorkspacePayload> GetWorkspacePayloadAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<WorkforceSummaryDto> GetSummaryAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkforceCoverageRowDto>> GetCoverageAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkforceUnitCoverageDto>> GetUnitsAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkforceRoleDefinitionDto>> GetRolesAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkforceMemberListItemDto>> ListMembersAsync(Guid facilityId, string? search, int pageSize, CancellationToken cancellationToken);
    Task<WorkforceMemberDetailDto?> GetMemberAsync(Guid facilityId, Guid memberId, CancellationToken cancellationToken);
    Task<WorkforceDataQualityDto> GetDataQualityAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<WorkforceQualificationListDto> ListQualificationsAsync(Guid facilityId, int page, int pageSize, CancellationToken cancellationToken);
}

public interface IWorkforceMemberCommandService
{
    Task<Guid> CreateMemberAsync(Guid facilityId, WorkforceMemberCreateRequest request, CancellationToken cancellationToken);
    Task UpdateMemberAsync(Guid facilityId, Guid memberId, WorkforceMemberUpdateRequest request, CancellationToken cancellationToken);
    Task<Guid> CreateAssignmentAsync(Guid facilityId, WorkforceAssignmentRequest request, CancellationToken cancellationToken);
    Task<Guid> CreateQualificationAsync(Guid facilityId, WorkforceQualificationRequest request, CancellationToken cancellationToken);
}

public interface IStaffingRequirementService
{
    Task<IReadOnlyList<StaffingRequirementDto>> ListRequirementsAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<Guid> RecordRequirementAsync(Guid facilityId, StaffingRequirementRequest request, CancellationToken cancellationToken);
}

public interface IDutyRosterService
{
    Task<IReadOnlyList<DutyRosterDto>> ListRostersAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<Guid> CreateRosterAsync(Guid facilityId, DutyRosterCreateRequest request, CancellationToken cancellationToken);
    Task<Guid> AddAssignmentAsync(Guid facilityId, Guid rosterId, DutyRosterAssignmentRequest request, CancellationToken cancellationToken);
    Task PublishAsync(Guid facilityId, Guid rosterId, CancellationToken cancellationToken);
}

public interface IWorkforceAvailabilityService
{
    Task<Guid> RecordAvailabilityAsync(Guid facilityId, WorkforceAvailabilityRequest request, CancellationToken cancellationToken);
}

public interface IWorkforceImportService
{
    Task<WorkforceImportResult> PreviewAsync(Guid facilityId, WorkforceImportPreviewRequest request, CancellationToken cancellationToken);
    Task<WorkforceImportResult> ConfirmAsync(Guid facilityId, WorkforceImportPreviewRequest request, CancellationToken cancellationToken);
}

public interface IWorkforceMemberQueryService
{
    Task<IReadOnlyList<WorkforceMemberListItemDto>> ListMembersAsync(Guid facilityId, string? search, int pageSize, CancellationToken cancellationToken);
    Task<WorkforceMemberDetailDto?> GetMemberAsync(Guid facilityId, Guid memberId, CancellationToken cancellationToken);
}

public interface IWorkforceReconciliationService
{
    Task<WorkforceReconciliationListDto> ListReconciliationAsync(Guid facilityId, int page, int pageSize, CancellationToken cancellationToken);
    Task ResolveReconciliationAsync(Guid facilityId, string itemId, WorkforceReconciliationResolveRequest request, CancellationToken cancellationToken);
    Task<WorkforceReconciliationResult> ReconcileAsync(Guid facilityId, CancellationToken cancellationToken);
}

public interface IWorkforceExportService
{
    Task<(string FileName, string ContentType, byte[] Content)> ExportAsync(Guid facilityId, string? search, int pageSize, CancellationToken cancellationToken);
}

public interface IWorkforceCriticalPositionQueryService
{
    Task<IReadOnlyList<WorkforceCriticalPositionDto>> ListCriticalPositionsAsync(Guid facilityId, CancellationToken cancellationToken);
}

public sealed class WorkforceReadinessOptions
{
    public int StaleVerificationDays { get; init; } = 30;
    public int MemberPageSizeLimit { get; init; } = 100;
}

public sealed partial class WorkforceReadinessService(
    IBaseeraDbContext db,
    IOrganizationalScopeService scope,
    ICurrentUser currentUser,
    IAuditService audit,
    IWorkforceSourceResolver sourceResolver,
    TimeProvider timeProvider)
    : IWorkforceReadinessQueryService,
      IWorkforceMemberQueryService,
      IWorkforceMemberCommandService,
      IStaffingRequirementService,
      IDutyRosterService,
      IWorkforceAvailabilityService,
      IWorkforceImportService,
      IWorkforceReconciliationService,
      IWorkforceExportService,
      IWorkforceCriticalPositionQueryService
{
    private const string SeverityCriticalValue = "critical";
    private const string SeverityHighValue = "high";
    private const string SeverityMediumValue = "medium";
    private const string FacilityNotFoundMessage = "السجن غير موجود.";

    private static readonly RosterAssignmentStatus[] CountedRosterStatuses =
    [
        RosterAssignmentStatus.Present,
        RosterAssignmentStatus.Confirmed,
        RosterAssignmentStatus.Late
    ];

    private readonly WorkforceReadinessOptions options = new();

    public async Task<WorkforceWorkspacePayload> GetWorkspacePayloadAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceViewSummary);
        // Workspace path: one summary computation (no snapshot write) + one coverage query.
        // Units/data-quality are derived in-memory to keep Facility Workspace query budget stable.
        var summary = await GetSummaryAsync(facilityId, cancellationToken, persistSnapshot: false);
        var coverage = currentUser.HasPermission(PermissionCodes.WorkforceViewCoverage)
            ? await GetCoverageAsync(facilityId, cancellationToken)
            : Array.Empty<WorkforceCoverageRowDto>();
        var units = AggregateUnitsFromCoverage(coverage);
        var roles = currentUser.HasPermission(PermissionCodes.WorkforceViewMembers)
            ? await GetRolesAsync(facilityId, cancellationToken)
            : Array.Empty<WorkforceRoleDefinitionDto>();
        var dataQuality = await GetDataQualityAsync(facilityId, cancellationToken);
        return new WorkforceWorkspacePayload
        {
            Summary = summary,
            Coverage = coverage,
            Units = units,
            Roles = roles,
            DataQuality = dataQuality
        };
    }

    public Task<WorkforceSummaryDto> GetSummaryAsync(Guid facilityId, CancellationToken cancellationToken) =>
        GetSummaryAsync(facilityId, cancellationToken, persistSnapshot: true);

    private async Task<WorkforceSummaryDto> GetSummaryAsync(
        Guid facilityId,
        CancellationToken cancellationToken,
        bool persistSnapshot,
        bool enforcePermission = true)
    {
        if (enforcePermission)
        {
            Require(PermissionCodes.WorkforceViewSummary);
        }

        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var members = await MembersInFacility(facilityId)
            .Select(m => new SummaryMemberRow(m.Id, m.EmploymentStatus, m.IsOperational, m.LastVerifiedAtUtc, m.EmployeeNumber, m.DisplayName, m.JobTitle))
            .ToListAsync(cancellationToken);

        var memberIds = members.Select(m => m.Id).ToList();
        var requirements = await ActiveRequirements(facilityId, now).ToListAsync(cancellationToken);
        var required = requirements.Sum(r => r.RequiredHeadcount);
        var minimumSafe = requirements.Sum(r => r.MinimumSafeHeadcount);

        var publishedAssignments = await db.DutyRosterAssignments
            .AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.DutyRoster.FacilityId == facilityId
                && a.DutyRoster.Status == DutyRosterStatuses.Published
                && a.DutyRoster.DutyDate == today)
            .Select(a => new SummaryRosterRow(a.Id, a.WorkforceMemberId, a.RoleDefinitionId, a.Status, a.ReplacementForAssignmentId))
            .ToListAsync(cancellationToken);

        var activeAssignments = await db.WorkforceAssignments
            .AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.FacilityId == facilityId
                && a.EffectiveFromUtc <= now
                && (a.EffectiveToUtc == null || a.EffectiveToUtc > now))
            .Select(a => new SummaryAssignmentRow(a.WorkforceMemberId, a.RoleDefinitionId))
            .ToListAsync(cancellationToken);

        var availability = await db.WorkforceAvailabilityEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted
                && memberIds.Contains(e.WorkforceMemberId)
                && e.StartsAtUtc <= now
                && (e.EndsAtUtc == null || e.EndsAtUtc > now))
            .Select(e => new SummaryAvailabilityRow(e.WorkforceMemberId, e.AvailabilityType, e.AffectsOperationalAvailability, e.SourceType))
            .ToListAsync(cancellationToken);

        var qualifications = await db.WorkforceQualifications
            .AsNoTracking()
            .Where(q => !q.IsDeleted && memberIds.Contains(q.WorkforceMemberId))
            .Select(q => new SummaryQualificationRow(q.WorkforceMemberId, q.RoleDefinitionId, q.Status, q.ExpiresAtUtc))
            .ToListAsync(cancellationToken);

        var critical = await db.CriticalPositionRequirements
            .AsNoTracking()
            .Where(c => !c.IsDeleted
                && c.FacilityId == facilityId
                && c.EffectiveFromUtc <= now
                && (c.EffectiveToUtc == null || c.EffectiveToUtc > now))
            .Select(c => new SummaryCriticalRow(c.RoleDefinitionId, c.RequiredPrimaryCount, c.RequiredAlternateCount))
            .ToListAsync(cancellationToken);

        var memberStats = AccumulateMemberSummaryStats(
            members,
            new SummarySourceRows(
                publishedAssignments,
                activeAssignments,
                availability,
                qualifications,
                now,
                sourceResolver,
                options.StaleVerificationDays));

        var coverage = WorkforceReadinessPolicy.Calculate(new WorkforceCoverageInputs(
            required,
            minimumSafe,
            activeAssignments.Select(a => a.WorkforceMemberId).Distinct().Count(),
            memberStats.Scheduled,
            memberStats.Present,
            memberStats.OperationallyAvailable,
            memberStats.Qualified,
            memberStats.Unqualified,
            members.Count(m => m.EmploymentStatus == EmploymentStatus.Suspended),
            memberStats.OnLeave,
            memberStats.InTraining,
            memberStats.Restricted,
            memberStats.Overtime,
            requirements.Count > 0,
            publishedAssignments.Count > 0 || activeAssignments.Count > 0));

        var criticalAtRisk = CountCriticalPositionsAtRisk(critical, publishedAssignments);
        var warnings = BuildSummaryWarnings(members.Count, required, memberStats.Stale, memberStats.Missing, criticalAtRisk, coverage.Status);
        var fatigue = BuildSummaryFatigueIndicators(memberStats.Overtime, publishedAssignments, critical, memberStats.NearestExpiry, now);

        var summary = new WorkforceSummaryDto
        {
            FacilityId = facilityId,
            TotalMembers = members.Count,
            OperationallyEligible = memberStats.OperationallyEligible,
            Required = required,
            MinimumSafe = minimumSafe,
            Scheduled = memberStats.Scheduled,
            Present = memberStats.Present,
            OperationallyAvailable = memberStats.OperationallyAvailable,
            OnLeave = memberStats.OnLeave,
            InTraining = memberStats.InTraining,
            Restricted = memberStats.Restricted,
            Gap = coverage.Gap,
            SafeGap = coverage.SafeGap,
            CoverageRate = coverage.CoverageRate,
            QualificationCoverage = coverage.QualificationCoverage,
            CoverageStatus = coverage.Status,
            CriticalPositionsAtRisk = criticalAtRisk,
            StaleRecords = memberStats.Stale,
            MissingDataRecords = memberStats.Missing,
            FreshnessStatus = WorkforceReadinessPolicy.ResolveFreshness(members.Count, memberStats.Stale),
            ConfidenceLevel = WorkforceReadinessPolicy.ResolveConfidence(members.Count, warnings.Count),
            IsPartial = warnings.Count > 0,
            Warnings = warnings,
            FatigueIndicators = fatigue,
            GeneratedAtUtc = now,
            DataEffectiveAtUtc = members.Max(m => m.LastVerifiedAtUtc)
        };

        if (persistSnapshot)
        {
            await UpsertFacilitySnapshotAsync(facilityId, summary, cancellationToken);
        }

        return summary;
    }


    public async Task<IReadOnlyList<WorkforceCoverageRowDto>> GetCoverageAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceViewCoverage);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var requirements = await ActiveRequirements(facilityId, now)
            .Select(r => new
            {
                r.RoleDefinitionId,
                RoleCode = r.RoleDefinition.Code,
                RoleNameAr = r.RoleDefinition.NameAr,
                r.FacilityUnitId,
                UnitNameAr = r.FacilityUnit == null ? null : r.FacilityUnit.NameAr,
                r.ShiftDefinitionId,
                ShiftCode = r.ShiftDefinition == null ? null : r.ShiftDefinition.Code,
                r.RequiredHeadcount,
                r.MinimumSafeHeadcount
            })
            .ToListAsync(cancellationToken);

        var rosterCounts = await db.DutyRosterAssignments
            .AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.DutyRoster.FacilityId == facilityId
                && a.DutyRoster.Status == DutyRosterStatuses.Published
                && a.DutyRoster.DutyDate == today)
            .GroupBy(a => new { a.RoleDefinitionId, a.DutyRoster.FacilityUnitId, a.DutyRoster.ShiftDefinitionId })
            .Select(g => new
            {
                g.Key.RoleDefinitionId,
                g.Key.FacilityUnitId,
                g.Key.ShiftDefinitionId,
                Scheduled = g.Where(a => a.Status != RosterAssignmentStatus.Cancelled && a.Status != RosterAssignmentStatus.Replaced)
                    .Select(a => a.WorkforceMemberId)
                    .Distinct()
                    .Count(),
                Present = g.Where(a => CountedRosterStatuses.Contains(a.Status))
                    .Select(a => a.WorkforceMemberId)
                    .Distinct()
                    .Count()
            })
            .ToListAsync(cancellationToken);

        return requirements.Select(requirement =>
        {
            var match = rosterCounts.FirstOrDefault(r =>
                r.RoleDefinitionId == requirement.RoleDefinitionId
                && r.FacilityUnitId == requirement.FacilityUnitId
                && r.ShiftDefinitionId == requirement.ShiftDefinitionId);
            var scheduled = match?.Scheduled ?? 0;
            var present = match?.Present ?? 0;
            var available = present > 0 ? present : scheduled;
            var calc = WorkforceReadinessPolicy.Calculate(new WorkforceCoverageInputs(
                requirement.RequiredHeadcount,
                requirement.MinimumSafeHeadcount,
                scheduled,
                scheduled,
                present,
                available,
                available,
                Math.Max(0, scheduled - available),
                0, 0, 0, 0, 0,
                true,
                scheduled > 0 || present > 0));
            return new WorkforceCoverageRowDto
            {
                RoleDefinitionId = requirement.RoleDefinitionId,
                RoleCode = requirement.RoleCode,
                RoleNameAr = requirement.RoleNameAr,
                FacilityUnitId = requirement.FacilityUnitId,
                UnitNameAr = requirement.UnitNameAr,
                ShiftDefinitionId = requirement.ShiftDefinitionId,
                ShiftCode = requirement.ShiftCode,
                Required = requirement.RequiredHeadcount,
                MinimumSafe = requirement.MinimumSafeHeadcount,
                Scheduled = scheduled,
                Present = present,
                OperationallyAvailable = available,
                Gap = calc.Gap,
                SafeGap = calc.SafeGap,
                CoverageRate = calc.CoverageRate,
                CoverageStatus = calc.Status
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<WorkforceUnitCoverageDto>> GetUnitsAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceViewCoverage);
        var coverage = await GetCoverageAsync(facilityId, cancellationToken);
        return AggregateUnitsFromCoverage(coverage);
    }

    private static IReadOnlyList<WorkforceUnitCoverageDto> AggregateUnitsFromCoverage(
        IReadOnlyList<WorkforceCoverageRowDto> coverage) =>
        coverage
            .GroupBy(row => new { row.FacilityUnitId, UnitNameAr = row.UnitNameAr ?? "غير محدد" })
            .Select(group =>
            {
                var required = group.Sum(r => r.Required);
                var available = group.Sum(r => r.OperationallyAvailable);
                var calc = WorkforceReadinessPolicy.Calculate(new WorkforceCoverageInputs(
                    required,
                    group.Sum(r => r.MinimumSafe),
                    available,
                    group.Sum(r => r.Scheduled),
                    group.Sum(r => r.Present),
                    available,
                    available,
                    0, 0, 0, 0, 0, 0,
                    required > 0 || group.Any(),
                    group.Any(r => r.Scheduled > 0 || r.Present > 0)));
                return new WorkforceUnitCoverageDto
                {
                    FacilityUnitId = group.Key.FacilityUnitId,
                    UnitNameAr = group.Key.UnitNameAr,
                    Required = required,
                    OperationallyAvailable = available,
                    Gap = calc.Gap,
                    CoverageRate = calc.CoverageRate,
                    CoverageStatus = calc.Status
                };
            })
            .OrderBy(row => row.UnitNameAr)
            .ToList();

    public async Task<IReadOnlyList<WorkforceRoleDefinitionDto>> GetRolesAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceViewMembers);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        return await db.WorkforceRoleDefinitions
            .AsNoTracking()
            .Where(role => !role.IsDeleted && role.OrganizationId == facility.OrganizationId)
            .OrderBy(role => role.Code)
            .Select(role => new WorkforceRoleDefinitionDto
            {
                Id = role.Id,
                Code = role.Code,
                NameAr = role.NameAr,
                NameEn = role.NameEn,
                Category = role.Category,
                Criticality = role.Criticality,
                RequiresCertification = role.RequiresCertification,
                IsShiftBased = role.IsShiftBased,
                IsSensitive = role.IsSensitive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkforceMemberListItemDto>> ListMembersAsync(
        Guid facilityId,
        string? search,
        int pageSize,
        CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceViewMembers);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var bounded = Math.Clamp(pageSize, 1, options.MemberPageSizeLimit);
        var query = MembersInFacility(facilityId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(m => m.DisplayName.Contains(term) || m.EmployeeNumber.Contains(term) || m.JobTitle.Contains(term));
        }

        var rows = await query
            .OrderBy(m => m.EmployeeNumber)
            .Take(bounded)
            .Select(m => new
            {
                m.Id,
                m.EmployeeNumber,
                m.DisplayName,
                m.EmploymentStatus,
                m.JobTitle,
                m.PrimarySpecialty,
                m.CurrentOperationalUnitId,
                UnitNameAr = m.CurrentOperationalUnit == null ? null : m.CurrentOperationalUnit.NameAr,
                m.IsOperational,
                m.IsSensitiveRole,
                m.LastVerifiedAtUtc
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new WorkforceMemberListItemDto
        {
            Id = row.Id,
            EmployeeNumber = row.EmployeeNumber,
            DisplayName = row.DisplayName,
            EmploymentStatus = row.EmploymentStatus,
            JobTitle = row.JobTitle,
            PrimarySpecialty = row.PrimarySpecialty,
            CurrentOperationalUnitId = row.CurrentOperationalUnitId,
            CurrentOperationalUnitNameAr = row.UnitNameAr,
            IsOperational = row.IsOperational,
            IsSensitiveRole = row.IsSensitiveRole,
            LastVerifiedAtUtc = row.LastVerifiedAtUtc,
            DataQualityIssues = BuildMemberDataQuality(row.EmployeeNumber, row.DisplayName, row.EmploymentStatus, row.LastVerifiedAtUtc)
        }).ToList();
    }

    public async Task<WorkforceMemberDetailDto?> GetMemberAsync(Guid facilityId, Guid memberId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceViewMembers);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var member = await MembersInFacility(facilityId)
            .Where(m => m.Id == memberId)
            .Select(m => new
            {
                m.Id,
                m.EmployeeNumber,
                m.DisplayName,
                m.EmploymentStatus,
                m.JobTitle,
                m.PrimarySpecialty,
                m.CurrentOperationalUnitId,
                UnitNameAr = m.CurrentOperationalUnit == null ? null : m.CurrentOperationalUnit.NameAr,
                m.IsOperational,
                m.IsSensitiveRole,
                m.LastVerifiedAtUtc,
                m.RowVersion
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (member is null)
        {
            return null;
        }

        var assignments = await db.WorkforceAssignments
            .AsNoTracking()
            .Where(a => !a.IsDeleted && a.WorkforceMemberId == memberId && a.FacilityId == facilityId)
            .Select(a => new WorkforceAssignmentDto
            {
                Id = a.Id,
                RoleDefinitionId = a.RoleDefinitionId,
                RoleCode = a.RoleDefinition.Code,
                RoleNameAr = a.RoleDefinition.NameAr,
                FacilityUnitId = a.FacilityUnitId,
                AssignmentType = a.AssignmentType,
                EffectiveFromUtc = a.EffectiveFromUtc,
                EffectiveToUtc = a.EffectiveToUtc,
                IsPrimary = a.IsPrimary
            })
            .ToListAsync(cancellationToken);

        var qualifications = await db.WorkforceQualifications
            .AsNoTracking()
            .Where(q => !q.IsDeleted && q.WorkforceMemberId == memberId)
            .Select(q => new WorkforceQualificationDto
            {
                Id = q.Id,
                QualificationType = q.QualificationType,
                RoleDefinitionId = q.RoleDefinitionId,
                Name = q.Name,
                ExpiresAtUtc = q.ExpiresAtUtc,
                Status = q.Status
            })
            .ToListAsync(cancellationToken);

        var canViewSensitive = WorkforceAccessPolicy.CanViewSensitiveRestrictions(currentUser.Permissions);
        var availabilityRows = await db.WorkforceAvailabilityEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted && e.WorkforceMemberId == memberId)
            .OrderByDescending(e => e.StartsAtUtc)
            .Take(50)
            .Select(e => new { e.Id, e.AvailabilityType, e.StartsAtUtc, e.EndsAtUtc, e.AffectsOperationalAvailability, e.RestrictionCodesCsv })
            .ToListAsync(cancellationToken);

        var availability = availabilityRows.Select(e => new WorkforceAvailabilityDto
        {
            Id = e.Id,
            AvailabilityType = e.AvailabilityType,
            StartsAtUtc = e.StartsAtUtc,
            EndsAtUtc = e.EndsAtUtc,
            AffectsOperationalAvailability = e.AffectsOperationalAvailability,
            RestrictionCodes = canViewSensitive ? ParseRestrictionCodes(e.RestrictionCodesCsv) : null
        }).ToList();

        IReadOnlyList<string>? restrictionCodes = null;
        if (canViewSensitive)
        {
            restrictionCodes = availability
                .SelectMany(a => a.RestrictionCodes ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        return new WorkforceMemberDetailDto
        {
            Member = new WorkforceMemberListItemDto
            {
                Id = member.Id,
                EmployeeNumber = member.EmployeeNumber,
                DisplayName = member.DisplayName,
                EmploymentStatus = member.EmploymentStatus,
                JobTitle = member.JobTitle,
                PrimarySpecialty = member.PrimarySpecialty,
                CurrentOperationalUnitId = member.CurrentOperationalUnitId,
                CurrentOperationalUnitNameAr = member.UnitNameAr,
                IsOperational = member.IsOperational,
                IsSensitiveRole = member.IsSensitiveRole,
                LastVerifiedAtUtc = member.LastVerifiedAtUtc,
                RowVersion = Convert.ToBase64String(member.RowVersion),
                DataQualityIssues = BuildMemberDataQuality(member.EmployeeNumber, member.DisplayName, member.EmploymentStatus, member.LastVerifiedAtUtc)
            },
            Assignments = assignments,
            Qualifications = qualifications,
            Availability = availability,
            RestrictionCodes = restrictionCodes
        };
    }

    public async Task<WorkforceQualificationListDto> ListQualificationsAsync(
        Guid facilityId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceViewMembers);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 100);
        var memberIds = MembersInFacility(facilityId).Select(m => m.Id);
        var query = db.WorkforceQualifications
            .AsNoTracking()
            .Where(q => !q.IsDeleted && memberIds.Contains(q.WorkforceMemberId));
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(q => q.WorkforceMember.EmployeeNumber)
            .ThenBy(q => q.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new WorkforceQualificationListItemDto
            {
                Id = q.Id,
                MemberId = q.WorkforceMemberId,
                MemberDisplayName = q.WorkforceMember.DisplayName,
                QualificationType = q.QualificationType,
                RoleDefinitionId = q.RoleDefinitionId,
                RoleCode = q.RoleDefinition == null ? null : q.RoleDefinition.Code,
                Name = q.Name,
                ExpiresAtUtc = q.ExpiresAtUtc,
                Status = q.Status
            })
            .ToListAsync(cancellationToken);

        return new WorkforceQualificationListDto
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<WorkforceDataQualityDto> GetDataQualityAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceViewSummary);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var staleBefore = now.AddDays(-options.StaleVerificationDays);
        var members = await MembersInFacility(facilityId)
            .Select(m => new DataQualityMemberRow(
                m.Id,
                m.EmployeeNumber,
                m.ExternalPersonnelId,
                m.EmploymentStatus,
                m.HomeFacilityId,
                m.CurrentOperationalFacilityId,
                m.CurrentOperationalUnitId,
                m.LastVerifiedAtUtc,
                m.UserId))
            .ToListAsync(cancellationToken);
        var counts = await CountDataQualityIssuesAsync(facilityId, today, now, staleBefore, members, cancellationToken);
        var issues = BuildDataQualityIssues(counts);

        var warnings = issues.Select(i => $"{i.TitleAr} ({i.Count})").ToList();
        return new WorkforceDataQualityDto
        {
            TotalMembers = members.Count,
            MissingEmployeeNumber = counts.MissingNumber,
            UnknownEmploymentStatus = counts.UnknownStatus,
            MissingHomeOrOperationalFacility = counts.MissingFacility,
            StaleVerification = counts.Stale,
            OpenImportIssues = counts.DuplicateExternal,
            Warnings = warnings,
            Issues = issues
        };
    }

    private async Task<DataQualityIssueCounts> CountDataQualityIssuesAsync(
        Guid facilityId,
        DateOnly today,
        DateTimeOffset now,
        DateTimeOffset staleBefore,
        IReadOnlyList<DataQualityMemberRow> members,
        CancellationToken cancellationToken)
    {
        var counts = CountMemberDataQualityIssues(facilityId, staleBefore, members);
        counts.ExpiredAssignments = await CountExpiredAssignmentsAsync(facilityId, now, cancellationToken);
        counts.MissingApproval = await CountRequirementsWithoutApprovalAsync(facilityId, now, cancellationToken);
        counts.RosterWithoutCommander = await CountRostersWithoutCommanderAsync(facilityId, today, cancellationToken);
        counts.LeaveWhileRostered = await CountLeaveWhileRosteredAsync(facilityId, today, now, cancellationToken);
        counts.RetiredOnRoster = await CountRetiredOnRosterAsync(facilityId, today, cancellationToken);
        counts.MissingQualification = await CountMissingQualificationAsync(facilityId, now, cancellationToken);
        counts.MissingRole = await CountMembersWithoutActiveRoleAsync(facilityId, now, cancellationToken);
        counts.ShiftWithoutMinimum = await CountShiftsWithoutMinimumAsync(facilityId, now, cancellationToken);
        counts.ExpiredQualification = await CountExpiredQualificationsAsync(facilityId, now, cancellationToken);
        counts.OverlappingRoster = await CountOverlappingRosterAsync(facilityId, today, cancellationToken);
        counts.GapWithoutOwner = await HasOpenStaffingGapAsync(facilityId, now, cancellationToken) ? 1 : 0;
        counts.InvalidUserLink = await CountInvalidUserLinksAsync(facilityId, cancellationToken);
        counts.UnknownAvailability = await CountUnknownAvailabilityAsync(facilityId, now, cancellationToken);
        return counts;
    }

    private static DataQualityIssueCounts CountMemberDataQualityIssues(
        Guid facilityId,
        DateTimeOffset staleBefore,
        IReadOnlyList<DataQualityMemberRow> members) =>
        new()
        {
            MissingNumber = members.Count(m => string.IsNullOrWhiteSpace(m.EmployeeNumber)),
            UnknownStatus = members.Count(m => m.EmploymentStatus == EmploymentStatus.Unknown),
            MissingFacility = members.Count(m => m.CurrentOperationalFacilityId is null),
            MissingUnit = members.Count(m => m.CurrentOperationalFacilityId == facilityId && m.CurrentOperationalUnitId is null),
            Stale = members.Count(m => m.LastVerifiedAtUtc is null || m.LastVerifiedAtUtc < staleBefore),
            DuplicateExternal = members
                .Where(m => !string.IsNullOrWhiteSpace(m.ExternalPersonnelId))
                .GroupBy(m => m.ExternalPersonnelId, StringComparer.OrdinalIgnoreCase)
                .Count(g => g.Count() > 1),
            DuplicateEmployee = members
                .Where(m => !string.IsNullOrWhiteSpace(m.EmployeeNumber))
                .GroupBy(m => m.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
                .Count(g => g.Count() > 1)
        };

    private async Task<int> CountExpiredAssignmentsAsync(Guid facilityId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.WorkforceAssignments.AsNoTracking()
            .CountAsync(a => !a.IsDeleted && a.FacilityId == facilityId && a.EffectiveToUtc != null && a.EffectiveToUtc <= now, cancellationToken);

    private async Task<int> CountRequirementsWithoutApprovalAsync(Guid facilityId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.StaffingRequirements.AsNoTracking()
            .CountAsync(r => !r.IsDeleted && r.FacilityId == facilityId
                && r.EffectiveFromUtc <= now && (r.EffectiveToUtc == null || r.EffectiveToUtc > now)
                && (r.ApprovalReference == null || r.ApprovalReference == ""), cancellationToken);

    private async Task<int> CountRostersWithoutCommanderAsync(Guid facilityId, DateOnly today, CancellationToken cancellationToken) =>
        await db.DutyRosters.AsNoTracking()
            .CountAsync(r => !r.IsDeleted && r.FacilityId == facilityId && r.DutyDate == today
                && r.Status == DutyRosterStatuses.Published
                && !r.Assignments.Any(a => !a.IsDeleted && a.RoleDefinition.Category == WorkforceRoleCategory.Command), cancellationToken);

    private async Task<int> CountLeaveWhileRosteredAsync(Guid facilityId, DateOnly today, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.DutyRosterAssignments.AsNoTracking()
            .CountAsync(a => !a.IsDeleted
                && a.DutyRoster.FacilityId == facilityId
                && a.DutyRoster.DutyDate == today
                && a.DutyRoster.Status == DutyRosterStatuses.Published
                && db.WorkforceAvailabilityEvents.Any(e => !e.IsDeleted
                    && e.WorkforceMemberId == a.WorkforceMemberId
                    && e.StartsAtUtc <= now
                    && (e.EndsAtUtc == null || e.EndsAtUtc > now)
                    && (e.AvailabilityType == AvailabilityType.AnnualLeave
                        || e.AvailabilityType == AvailabilityType.SickLeave
                        || e.AvailabilityType == AvailabilityType.EmergencyLeave)), cancellationToken);

    private async Task<int> CountRetiredOnRosterAsync(Guid facilityId, DateOnly today, CancellationToken cancellationToken) =>
        await db.DutyRosterAssignments.AsNoTracking()
            .CountAsync(a => !a.IsDeleted
                && a.DutyRoster.FacilityId == facilityId
                && a.DutyRoster.DutyDate == today
                && a.DutyRoster.Status == DutyRosterStatuses.Published
                && (a.WorkforceMember.EmploymentStatus == EmploymentStatus.Retired
                    || a.WorkforceMember.EmploymentStatus == EmploymentStatus.Terminated), cancellationToken);

    private async Task<int> CountMissingQualificationAsync(Guid facilityId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.WorkforceAssignments.AsNoTracking()
            .CountAsync(a => !a.IsDeleted && a.FacilityId == facilityId
                && a.EffectiveFromUtc <= now && (a.EffectiveToUtc == null || a.EffectiveToUtc > now)
                && a.RoleDefinition.RequiresCertification
                && !db.WorkforceQualifications.Any(q => !q.IsDeleted
                    && q.WorkforceMemberId == a.WorkforceMemberId
                    && q.RoleDefinitionId == a.RoleDefinitionId
                    && (q.Status == QualificationStatus.Valid || q.Status == QualificationStatus.ExpiringSoon)
                    && (q.ExpiresAtUtc == null || q.ExpiresAtUtc > now)), cancellationToken);

    private async Task<int> CountMembersWithoutActiveRoleAsync(Guid facilityId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.WorkforceMembers.AsNoTracking()
            .CountAsync(m => !m.IsDeleted
                && (m.CurrentOperationalFacilityId == facilityId || m.HomeFacilityId == facilityId)
                && !db.WorkforceAssignments.Any(a => !a.IsDeleted && a.WorkforceMemberId == m.Id
                    && a.FacilityId == facilityId
                    && a.EffectiveFromUtc <= now
                    && (a.EffectiveToUtc == null || a.EffectiveToUtc > now)), cancellationToken);

    private async Task<int> CountShiftsWithoutMinimumAsync(Guid facilityId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.ShiftDefinitions.AsNoTracking()
            .CountAsync(s => !s.IsDeleted && s.FacilityId == facilityId
                && !db.StaffingRequirements.Any(r => !r.IsDeleted && r.FacilityId == facilityId
                    && r.ShiftDefinitionId == s.Id
                    && r.MinimumSafeHeadcount > 0
                    && r.EffectiveFromUtc <= now
                    && (r.EffectiveToUtc == null || r.EffectiveToUtc > now)), cancellationToken);

    private async Task<int> CountExpiredQualificationsAsync(Guid facilityId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.WorkforceQualifications.AsNoTracking()
            .CountAsync(q => !q.IsDeleted
                && q.ExpiresAtUtc != null && q.ExpiresAtUtc <= now
                && (q.WorkforceMember.CurrentOperationalFacilityId == facilityId
                    || q.WorkforceMember.HomeFacilityId == facilityId), cancellationToken);

    private async Task<int> CountOverlappingRosterAsync(Guid facilityId, DateOnly today, CancellationToken cancellationToken) =>
        await db.DutyRosterAssignments.AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.DutyRoster.FacilityId == facilityId
                && a.DutyRoster.DutyDate == today
                && a.DutyRoster.Status == DutyRosterStatuses.Published)
            .GroupBy(a => a.WorkforceMemberId)
            .Where(g => g.Count() > 1)
            .CountAsync(cancellationToken);

    private async Task<bool> HasOpenStaffingGapAsync(Guid facilityId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.StaffingRequirements.AsNoTracking()
            .AnyAsync(r => !r.IsDeleted && r.FacilityId == facilityId
                && r.RequiredHeadcount > 0
                && r.EffectiveFromUtc <= now
                && (r.EffectiveToUtc == null || r.EffectiveToUtc > now), cancellationToken);

    private async Task<int> CountInvalidUserLinksAsync(Guid facilityId, CancellationToken cancellationToken) =>
        await db.WorkforceMembers.AsNoTracking()
            .CountAsync(m => !m.IsDeleted
                && (m.CurrentOperationalFacilityId == facilityId || m.HomeFacilityId == facilityId)
                && m.UserId != null
                && !db.Users.Any(u => u.Id == m.UserId), cancellationToken);

    private async Task<int> CountUnknownAvailabilityAsync(Guid facilityId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.WorkforceMembers.AsNoTracking()
            .CountAsync(m => !m.IsDeleted
                && m.CurrentOperationalFacilityId == facilityId
                && m.EmploymentStatus == EmploymentStatus.Active
                && m.IsOperational
                && !db.WorkforceAvailabilityEvents.Any(e => !e.IsDeleted
                    && e.WorkforceMemberId == m.Id
                    && e.StartsAtUtc <= now
                    && (e.EndsAtUtc == null || e.EndsAtUtc > now)), cancellationToken);

    private static List<WorkforceDataQualityIssueDto> BuildDataQualityIssues(DataQualityIssueCounts counts)
    {
        var issues = new List<WorkforceDataQualityIssueDto>();
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.MissingEmployeeNumber, "أرقام موظفين مفقودة", counts.MissingNumber, SeverityHighValue, "يصعب مطابقة المصدر الخارجي.", "استكمال رقم الموظف.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.MissingOperationalFacility, "منشأة تشغيلية مفقودة", counts.MissingFacility, SeverityCriticalValue, "لا يدخل العضو في نطاق المنشأة التشغيلي.", "ربط العضو بمنشأة تشغيلية.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.MissingOperationalUnit, "وحدة تشغيلية مفقودة", counts.MissingUnit, SeverityMediumValue, "يصعب توزيع التغطية على الوحدات.", "تعيين وحدة تشغيلية.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.MissingOperationalRole, "دور تشغيلي مفقود", counts.MissingRole, SeverityHighValue, "لا يدخل في تغطية الأدوار.", "إنشاء تكليف دور فعال.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.UnknownEmploymentStatus, "حالة توظيف غير معروفة", counts.UnknownStatus, SeverityHighValue, "لا يمكن احتساب الجاهزية بثقة.", "تحديث حالة التوظيف.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.UnknownAvailability, "توفر مجهول", counts.UnknownAvailability, SeverityMediumValue, "Unknown لا يُحسب Available.", "تحديث التوفر.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.ExpiredAssignment, "تكليفات منتهية ما زالت ظاهرة", counts.ExpiredAssignments, SeverityMediumValue, "تضخيم التغطية الظاهرة.", "أرشفة التكليف المنتهي.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.ConflictingAssignments, "تكليفات متعارضة", counts.OverlappingRoster, SeverityCriticalValue, "عد مزدوج/تعارض حضور.", "إزالة التعارض.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.RosterWithoutCommander, "مناوبة بدون قائد", counts.RosterWithoutCommander, SeverityHighValue, "فجوة قيادة في الوردية.", "تعيين دور قيادي.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.ShiftWithoutMinimumRequirement, "مناوبة بلا حد أدنى آمن", counts.ShiftWithoutMinimum, SeverityMediumValue, "لا يمكن الحكم على السلامة.", "تعريف MinimumSafe.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.MissingQualification, "مؤهل مطلوب مفقود", counts.MissingQualification, SeverityHighValue, "تغطية غير مؤهلة.", "تسجيل/تجديد المؤهل.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.ExpiredQualification, "مؤهل منتهٍ", counts.ExpiredQualification, SeverityHighValue, "تغطية غير مؤهلة.", "تجديد أو إيقاف الدور.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.StaleSource, "سجلات مصدر متقادمة", counts.Stale, SeverityMediumValue, "ثقة البيانات منخفضة.", "إعادة التحقق من المصدر.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.DuplicateExternalId, "معرّف خارجي مكرر", counts.DuplicateExternal, SeverityHighValue, "ازدواجية سجلات الأفراد.", "دمج أو تصحيح المعرف.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.DuplicateEmployeeNumber, "رقم موظف مكرر داخل المنشأة", counts.DuplicateEmployee, SeverityCriticalValue, "ازدواج هوية تشغيلية.", "دمج السجلات.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.InvalidUserLink, "ربط مستخدم غير صحيح", counts.InvalidUserLink, SeverityHighValue, "فشل ربط الهوية.", "تصحيح UserId.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.RetiredMemberInActiveRoster, "متقاعد/منتهٍ في المناوبة", counts.RetiredOnRoster, SeverityCriticalValue, "جدولة غير صالحة.", "إزالة من الجدول.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.MemberOnLeaveButScheduled, "إجازة أثناء المناوبة", counts.LeaveWhileRostered, SeverityHighValue, "فجوة حضور متوقعة.", "تعيين بديل.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.ConflictingRosterAssignments, "خانات مناوبة متعارضة", counts.OverlappingRoster, SeverityCriticalValue, "عد مزدوج/تعارض حضور.", "إزالة التعارض.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.CoverageGapWithoutResponsibleOwner, "فجوة بلا مسؤول", counts.GapWithoutOwner, SeverityMediumValue, "لا يوجد مالك للمتابعة.", "تعيين مسؤول تغطية.");
        AddDataQualityIssue(issues, WorkforceOperationalCatalog.DataQuality.RequirementWithoutApprovalReference, "احتياج بلا مرجع اعتماد", counts.MissingApproval, SeverityMediumValue, "baseline غير موثق.", "إضافة مرجع الاعتماد.");
        return issues;
    }

    private static void AddDataQualityIssue(
        List<WorkforceDataQualityIssueDto> issues,
        string code,
        string titleAr,
        int count,
        string severity,
        string impactAr,
        string actionAr)
    {
        if (count <= 0)
        {
            return;
        }

        issues.Add(new WorkforceDataQualityIssueDto
        {
            Code = code,
            TitleAr = titleAr,
            Count = count,
            Severity = severity,
            ImpactAr = impactAr,
            SuggestedActionAr = actionAr,
            OwnerAr = "عمليات المنشأة",
            DrillDownHint = code
        });
    }

    public async Task<Guid> CreateMemberAsync(Guid facilityId, WorkforceMemberCreateRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceManageMembers);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        ValidateMemberCreate(request);
        if (request.CurrentOperationalUnitId.HasValue)
        {
            await EnsureUnitInFacilityAsync(facilityId, request.CurrentOperationalUnitId.Value, cancellationToken);
        }

        if (request.HomeFacilityId.HasValue)
        {
            await EnsureFacilityInOrganizationAndScopeAsync(facility.OrganizationId, request.HomeFacilityId.Value, cancellationToken);
        }

        if (request.SupervisorWorkforceMemberId.HasValue)
        {
            await EnsureMemberInFacilityAsync(facilityId, request.SupervisorWorkforceMemberId.Value, cancellationToken);
        }

        var employeeNumber = WorkforceAccessPolicy.NormalizeEmployeeNumber(request.EmployeeNumber);
        var exists = await db.WorkforceMembers.AnyAsync(
            m => m.OrganizationId == facility.OrganizationId && m.EmployeeNumber == employeeNumber && !m.IsDeleted,
            cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("رقم الموظف مستخدم مسبقًا داخل المنظمة.");
        }

        var member = new WorkforceMember
        {
            OrganizationId = facility.OrganizationId,
            ExternalPersonnelId = string.IsNullOrWhiteSpace(request.ExternalPersonnelId) ? null : request.ExternalPersonnelId.Trim(),
            DisplayName = request.DisplayName.Trim(),
            EmployeeNumber = employeeNumber,
            EmploymentStatus = request.EmploymentStatus,
            RankOrGrade = request.RankOrGrade,
            JobTitle = request.JobTitle.Trim(),
            PrimarySpecialty = request.PrimarySpecialty.Trim(),
            AdministrativeOrganizationId = facility.OrganizationId,
            HomeFacilityId = request.HomeFacilityId ?? facilityId,
            CurrentOperationalFacilityId = facilityId,
            CurrentOperationalUnitId = request.CurrentOperationalUnitId,
            SupervisorWorkforceMemberId = request.SupervisorWorkforceMemberId,
            IsOperational = request.IsOperational,
            IsSensitiveRole = request.IsSensitiveRole,
            SourceType = request.SourceType,
            SourceReference = request.SourceReference,
            LastVerifiedAtUtc = timeProvider.GetUtcNow()
        };
        db.Add(member);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (WorkforceAccessPolicy.IsWorkforceMembersUniqueViolation(ex))
        {
            throw new InvalidOperationException("رقم الموظف مستخدم مسبقًا داخل المنظمة.", ex);
        }

        await AuditAsync("WorkforceMemberCreated", "WorkforceMember", member.Id, cancellationToken);
        return member.Id;
    }

    public async Task UpdateMemberAsync(Guid facilityId, Guid memberId, WorkforceMemberUpdateRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceManageMembers);
        if (string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.JobTitle) || string.IsNullOrWhiteSpace(request.PrimarySpecialty))
        {
            throw new WorkforceValidationException("الاسم المعروض والمسمى الوظيفي والتخصص مطلوبة.");
        }

        var member = await LoadMemberForUpdateAsync(facilityId, memberId, cancellationToken);
        if (request.RowVersion is { Length: > 0 })
        {
            WorkforceAccessPolicy.EnsureRowVersion(member.RowVersion, request.RowVersion);
        }

        if (request.SupervisorWorkforceMemberId == member.Id)
        {
            throw new InvalidOperationException("لا يمكن تعيين العضو مشرفًا على نفسه.");
        }

        if (request.CurrentOperationalUnitId.HasValue)
        {
            await EnsureUnitInFacilityAsync(facilityId, request.CurrentOperationalUnitId.Value, cancellationToken);
        }

        if (request.SupervisorWorkforceMemberId.HasValue)
        {
            await EnsureMemberInFacilityAsync(facilityId, request.SupervisorWorkforceMemberId.Value, cancellationToken);
        }

        member.DisplayName = request.DisplayName.Trim();
        member.EmploymentStatus = request.EmploymentStatus;
        member.RankOrGrade = request.RankOrGrade;
        member.JobTitle = request.JobTitle.Trim();
        member.PrimarySpecialty = request.PrimarySpecialty.Trim();
        member.CurrentOperationalFacilityId = facilityId;
        member.CurrentOperationalUnitId = request.CurrentOperationalUnitId;
        member.SupervisorWorkforceMemberId = request.SupervisorWorkforceMemberId;
        member.IsOperational = request.IsOperational;
        member.IsSensitiveRole = request.IsSensitiveRole;
        member.LastVerifiedAtUtc = timeProvider.GetUtcNow();
        member.UpdatedAtUtc = timeProvider.GetUtcNow();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("تعارض في النسخة المتزامنة لسجل العضو.");
        }

        await AuditAsync("WorkforceMemberUpdated", "WorkforceMember", member.Id, cancellationToken);
    }

    public async Task<Guid> CreateAssignmentAsync(Guid facilityId, WorkforceAssignmentRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceManageAssignments);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        await EnsureMemberInFacilityAsync(facilityId, request.WorkforceMemberId, cancellationToken);
        await EnsureRoleExistsAsync(facilityId, request.RoleDefinitionId, cancellationToken);
        if (request.FacilityUnitId.HasValue)
        {
            await EnsureUnitInFacilityAsync(facilityId, request.FacilityUnitId.Value, cancellationToken);
        }

        var assignment = await db.ExecuteInTransactionAsync(async ct =>
        {
            var existing = await db.WorkforceAssignments
                .Where(a => !a.IsDeleted && a.WorkforceMemberId == request.WorkforceMemberId)
                .Select(a => new { a.IsPrimary, a.EffectiveFromUtc, a.EffectiveToUtc })
                .ToListAsync(ct);
            if (WorkforceAssignmentPolicy.HasConflictingPrimaryAssignment(
                    existing.Select(a => (a.IsPrimary, a.EffectiveFromUtc, a.EffectiveToUtc)),
                    request.EffectiveFromUtc,
                    request.EffectiveToUtc,
                    request.IsPrimary))
            {
                throw new InvalidOperationException("يوجد تكليف أساسي متداخل لنفس العضو.");
            }

            var created = new WorkforceAssignment
            {
                WorkforceMemberId = request.WorkforceMemberId,
                FacilityId = facilityId,
                FacilityUnitId = request.FacilityUnitId,
                RoleDefinitionId = request.RoleDefinitionId,
                AssignmentType = request.AssignmentType,
                EffectiveFromUtc = request.EffectiveFromUtc,
                EffectiveToUtc = request.EffectiveToUtc,
                IsPrimary = request.IsPrimary,
                SourceReference = request.SourceReference,
                Reason = request.Reason,
                ApprovedBy = currentUser.DisplayName
            };
            db.Add(created);
            await db.SaveChangesAsync(ct);
            return created;
        }, cancellationToken, System.Data.IsolationLevel.Serializable);
        await AuditAsync("WorkforceAssignmentCreated", "WorkforceAssignment", assignment.Id, cancellationToken);
        return assignment.Id;
    }

    public async Task<Guid> CreateQualificationAsync(Guid facilityId, WorkforceQualificationRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceManageQualifications);
        await EnsureMemberInFacilityAsync(facilityId, request.WorkforceMemberId, cancellationToken);
        if (request.RoleDefinitionId.HasValue)
        {
            await EnsureRoleExistsAsync(facilityId, request.RoleDefinitionId.Value, cancellationToken);
        }

        var qualification = new WorkforceQualification
        {
            WorkforceMemberId = request.WorkforceMemberId,
            QualificationType = request.QualificationType,
            RoleDefinitionId = request.RoleDefinitionId,
            Name = request.Name.Trim(),
            IssuedAtUtc = request.IssuedAtUtc,
            ExpiresAtUtc = request.ExpiresAtUtc,
            Issuer = request.Issuer,
            Reference = request.Reference,
            Status = request.Status,
            VerifiedAtUtc = timeProvider.GetUtcNow(),
            VerifiedBy = currentUser.DisplayName
        };
        db.Add(qualification);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("WorkforceQualificationCreated", "WorkforceQualification", qualification.Id, cancellationToken);
        return qualification.Id;
    }

    public async Task<IReadOnlyList<StaffingRequirementDto>> ListRequirementsAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceViewCoverage);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        return await ActiveRequirements(facilityId, now)
            .Select(r => new StaffingRequirementDto
            {
                Id = r.Id,
                FacilityUnitId = r.FacilityUnitId,
                RoleDefinitionId = r.RoleDefinitionId,
                RoleCode = r.RoleDefinition.Code,
                ShiftDefinitionId = r.ShiftDefinitionId,
                RequiredHeadcount = r.RequiredHeadcount,
                MinimumSafeHeadcount = r.MinimumSafeHeadcount,
                EffectiveFromUtc = r.EffectiveFromUtc,
                EffectiveToUtc = r.EffectiveToUtc,
                SourceReference = r.SourceReference
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> RecordRequirementAsync(Guid facilityId, StaffingRequirementRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceManageRequirements);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        if (request.RequiredHeadcount < 0 || request.MinimumSafeHeadcount < 0 || request.MinimumSafeHeadcount > request.RequiredHeadcount)
        {
            throw new ArgumentException("قيم الاحتياج غير صحيحة.");
        }

        await EnsureRoleExistsAsync(facilityId, request.RoleDefinitionId, cancellationToken);
        if (request.FacilityUnitId.HasValue)
        {
            await EnsureUnitInFacilityAsync(facilityId, request.FacilityUnitId.Value, cancellationToken);
        }

        var requirement = new StaffingRequirement
        {
            OrganizationId = facility.OrganizationId,
            FacilityId = facilityId,
            FacilityUnitId = request.FacilityUnitId,
            RoleDefinitionId = request.RoleDefinitionId,
            ShiftDefinitionId = request.ShiftDefinitionId,
            RequiredHeadcount = request.RequiredHeadcount,
            MinimumSafeHeadcount = request.MinimumSafeHeadcount,
            EffectiveFromUtc = request.EffectiveFromUtc,
            EffectiveToUtc = request.EffectiveToUtc,
            SourceReference = request.SourceReference.Trim(),
            ApprovalReference = request.ApprovalReference,
            Notes = request.Notes
        };
        db.Add(requirement);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("StaffingRequirementChanged", "StaffingRequirement", requirement.Id, cancellationToken);
        return requirement.Id;
    }

    public async Task<IReadOnlyList<DutyRosterDto>> ListRostersAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceViewCoverage);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        return await db.DutyRosters
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.FacilityId == facilityId)
            .OrderByDescending(r => r.DutyDate)
            .Take(50)
            .Select(r => new DutyRosterDto
            {
                Id = r.Id,
                FacilityUnitId = r.FacilityUnitId,
                ShiftDefinitionId = r.ShiftDefinitionId,
                DutyDate = r.DutyDate,
                Status = r.Status,
                PublishedAtUtc = r.PublishedAtUtc,
                AssignmentCount = r.Assignments.Count(a => !a.IsDeleted)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreateRosterAsync(Guid facilityId, DutyRosterCreateRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceManageRosters);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        if (request.FacilityUnitId.HasValue)
        {
            await EnsureUnitInFacilityAsync(facilityId, request.FacilityUnitId.Value, cancellationToken);
        }

        var shiftExists = await db.ShiftDefinitions.AnyAsync(
            s => s.Id == request.ShiftDefinitionId && s.FacilityId == facilityId && !s.IsDeleted,
            cancellationToken);
        if (!shiftExists)
        {
            throw new KeyNotFoundException("الوردية غير موجودة.");
        }

        var roster = new DutyRoster
        {
            FacilityId = facilityId,
            FacilityUnitId = request.FacilityUnitId,
            ShiftDefinitionId = request.ShiftDefinitionId,
            DutyDate = request.DutyDate,
            Status = DutyRosterStatuses.Draft
        };
        db.Add(roster);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("DutyRosterCreated", "DutyRoster", roster.Id, cancellationToken);
        return roster.Id;
    }

    public async Task<Guid> AddAssignmentAsync(Guid facilityId, Guid rosterId, DutyRosterAssignmentRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceManageRosters);
        var roster = await db.DutyRosters
            .Where(r => r.Id == rosterId && r.FacilityId == facilityId && !r.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("جدول المناوبة غير موجود.");
        if (roster.Status == DutyRosterStatuses.Published)
        {
            throw new InvalidOperationException("لا يمكن تعديل جدول منشور.");
        }

        await EnsureMemberInFacilityAsync(facilityId, request.WorkforceMemberId, cancellationToken);
        await EnsureRoleExistsAsync(facilityId, request.RoleDefinitionId, cancellationToken);
        var duplicateActive = await db.DutyRosterAssignments.AnyAsync(a =>
            !a.IsDeleted
            && a.DutyRosterId == rosterId
            && a.WorkforceMemberId == request.WorkforceMemberId
            && a.Status != RosterAssignmentStatus.Cancelled
            && a.Status != RosterAssignmentStatus.Replaced,
            cancellationToken);
        if (duplicateActive)
        {
            throw new InvalidOperationException("يوجد تعيين فعال لنفس العضو في جدول المناوبة.");
        }

        var assignment = new DutyRosterAssignment
        {
            DutyRosterId = rosterId,
            WorkforceMemberId = request.WorkforceMemberId,
            RoleDefinitionId = request.RoleDefinitionId,
            Status = request.Status,
            ReplacementForAssignmentId = request.ReplacementForAssignmentId,
            Notes = request.Notes
        };
        db.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("DutyRosterAssignmentCreated", "DutyRosterAssignment", assignment.Id, cancellationToken);
        return assignment.Id;
    }

    public async Task PublishAsync(Guid facilityId, Guid rosterId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceManageRosters);
        var roster = await db.DutyRosters
            .Where(r => r.Id == rosterId && r.FacilityId == facilityId && !r.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("جدول المناوبة غير موجود.");
        if (roster.Status == DutyRosterStatuses.Published)
        {
            return;
        }

        roster.Status = DutyRosterStatuses.Published;
        roster.PublishedAtUtc = timeProvider.GetUtcNow();
        roster.PublishedBy = currentUser.DisplayName;
        await db.SaveChangesAsync(cancellationToken);
        await GetSummaryAsync(facilityId, cancellationToken, persistSnapshot: true, enforcePermission: false);
        await AuditAsync("DutyRosterPublished", "DutyRoster", roster.Id, cancellationToken);
    }

    public async Task<Guid> RecordAvailabilityAsync(Guid facilityId, WorkforceAvailabilityRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceRecordAvailability);
        await EnsureMemberInFacilityAsync(facilityId, request.WorkforceMemberId, cancellationToken);
        if (request.EndsAtUtc.HasValue && request.EndsAtUtc <= request.StartsAtUtc)
        {
            throw new ArgumentException("نطاق التوفر غير صحيح.");
        }

        var availability = new WorkforceAvailabilityEvent
        {
            WorkforceMemberId = request.WorkforceMemberId,
            AvailabilityType = request.AvailabilityType,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc,
            AffectsOperationalAvailability = request.AffectsOperationalAvailability,
            SourceType = request.SourceType,
            SourceReference = request.SourceReference,
            ReasonCode = request.ReasonCode,
            RestrictionCodesCsv = request.RestrictionCodes is { Count: > 0 }
                ? string.Join(',', request.RestrictionCodes.Select(c => c.ToString()))
                : null,
            RecordedAtUtc = timeProvider.GetUtcNow(),
            RecordedBy = currentUser.DisplayName
        };
        db.Add(availability);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("WorkforceAvailabilityRecorded", "WorkforceAvailabilityEvent", availability.Id, cancellationToken);
        return availability.Id;
    }

    public async Task<WorkforceImportResult> PreviewAsync(Guid facilityId, WorkforceImportPreviewRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceImport);
        ValidateImportRequest(request);
        var context = await BuildImportContextAsync(facilityId, request, cancellationToken);
        var validation = ValidateImportRows(context, request);
        EnsureImportBatchCounts(validation, WorkforceImportBatchStatuses.Previewed, 0, null);
        return ToImportResult(validation, 0);
    }

    public async Task<WorkforceImportResult> ConfirmAsync(Guid facilityId, WorkforceImportPreviewRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceImport);
        ValidateImportRequest(request);
        var sourceSystem = request.SourceSystem.Trim();
        var sourceReference = request.SourceReference.Trim();
        var fileHash = request.FileHash.Trim();
        var existing = await FindConfirmedImportAsync(facilityId, request.ImportKind, sourceSystem, sourceReference, fileHash, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var context = await BuildImportContextAsync(facilityId, request, cancellationToken);
        var validation = ValidateImportRows(context, request);
        if (validation.ValidRows.Count == 0 && request.ImportKind == WorkforceImportKind.PersonnelMaster)
        {
            EnsureImportBatchCounts(validation, WorkforceImportBatchStatuses.Previewed, 0, null);
            return ToImportResult(validation, 0);
        }

        if (validation.RejectedRows == validation.TotalRows && validation.ValidRows.Count == 0)
        {
            EnsureImportBatchCounts(validation, WorkforceImportBatchStatuses.Previewed, 0, null);
            return ToImportResult(validation, 0);
        }

        return await ApplyImportAsync(context, validation, cancellationToken);
    }

    private IQueryable<WorkforceMember> MembersInFacility(Guid facilityId) =>
        db.WorkforceMembers
            .AsNoTracking()
            .Where(m => !m.IsDeleted
                && (m.CurrentOperationalFacilityId == facilityId || m.HomeFacilityId == facilityId));

    private IQueryable<StaffingRequirement> ActiveRequirements(Guid facilityId, DateTimeOffset asOfUtc) =>
        db.StaffingRequirements
            .AsNoTracking()
            .Where(r => !r.IsDeleted
                && r.FacilityId == facilityId
                && r.EffectiveFromUtc <= asOfUtc
                && (r.EffectiveToUtc == null || r.EffectiveToUtc > asOfUtc));

    private async Task UpsertFacilitySnapshotAsync(Guid facilityId, WorkforceSummaryDto summary, CancellationToken cancellationToken)
    {
        var capturedDateUtc = DateOnly.FromDateTime(summary.GeneratedAtUtc.UtcDateTime);
        await db.ExecuteInTransactionAsync(async ct =>
        {
            var latest = await db.WorkforceReadinessSnapshots
                .Where(s => s.FacilityId == facilityId
                    && s.FacilityUnitId == null
                    && s.ShiftDefinitionId == null
                    && s.RoleDefinitionId == null
                    && s.CapturedDateUtc == capturedDateUtc)
                .FirstOrDefaultAsync(ct);

            if (latest is null)
            {
                latest = new WorkforceReadinessSnapshot
                {
                    FacilityId = facilityId,
                    CapturedDateUtc = capturedDateUtc
                };
                db.Add(latest);
            }

            ApplySnapshot(summary, capturedDateUtc, latest);
            await db.SaveChangesAsync(ct);
            return 0;
        }, cancellationToken, System.Data.IsolationLevel.Serializable);
    }

    private static void ApplySnapshot(
        WorkforceSummaryDto summary,
        DateOnly capturedDateUtc,
        WorkforceReadinessSnapshot snapshot)
    {
        snapshot.CapturedAtUtc = summary.GeneratedAtUtc;
        snapshot.CapturedDateUtc = capturedDateUtc;
        snapshot.Required = summary.Required;
        snapshot.MinimumSafe = summary.MinimumSafe;
        snapshot.Assigned = summary.OperationallyEligible;
        snapshot.Scheduled = summary.Scheduled;
        snapshot.Present = summary.Present;
        snapshot.OperationallyAvailable = summary.OperationallyAvailable;
        snapshot.Gap = summary.Gap;
        snapshot.SafeGap = summary.SafeGap;
        snapshot.CoverageRate = summary.CoverageRate;
        snapshot.QualificationCoverage = summary.QualificationCoverage;
        snapshot.Freshness = summary.FreshnessStatus;
        snapshot.Confidence = summary.ConfidenceLevel;
        snapshot.SourceStatus = summary.CoverageStatus.ToString();
        snapshot.CoverageStatus = summary.CoverageStatus;
        snapshot.OnLeave = summary.OnLeave;
        snapshot.InTraining = summary.InTraining;
        snapshot.Restricted = summary.Restricted;
    }

    private async Task<FacilityScopeInfo> EnsureFacilityVisibleAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        if (!scope.CanAccessFacility(facilityId))
        {
            throw new KeyNotFoundException(FacilityNotFoundMessage);
        }

        return await db.Facilities
            .AsNoTracking()
            .Where(f => f.Id == facilityId && f.IsActive)
            .Select(f => new FacilityScopeInfo(f.Id, f.Region.OrganizationId, f.RegionId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException(FacilityNotFoundMessage);
    }

    private async Task EnsureUnitInFacilityAsync(Guid facilityId, Guid unitId, CancellationToken cancellationToken)
    {
        var exists = await db.FacilityUnits
            .AsNoTracking()
            .AnyAsync(unit => unit.Id == unitId && unit.FacilityId == facilityId && unit.IsActive, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException("الوحدة غير موجودة.");
        }
    }

    private async Task EnsureFacilityInOrganizationAndScopeAsync(Guid organizationId, Guid facilityId, CancellationToken cancellationToken)
    {
        if (!scope.CanAccessFacility(facilityId))
        {
            throw new KeyNotFoundException(FacilityNotFoundMessage);
        }

        var valid = await db.Facilities
            .AsNoTracking()
            .AnyAsync(f => f.Id == facilityId && f.IsActive && f.Region.OrganizationId == organizationId, cancellationToken);
        if (!valid)
        {
            throw new KeyNotFoundException(FacilityNotFoundMessage);
        }
    }

    private async Task EnsureMemberInFacilityAsync(Guid facilityId, Guid memberId, CancellationToken cancellationToken)
    {
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var exists = await MembersInFacility(facilityId).AnyAsync(m => m.Id == memberId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException("عضو القوى البشرية غير موجود.");
        }
    }

    private async Task EnsureRoleExistsAsync(Guid facilityId, Guid roleDefinitionId, CancellationToken cancellationToken)
    {
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var exists = await db.WorkforceRoleDefinitions
            .AsNoTracking()
            .AnyAsync(r => r.Id == roleDefinitionId && r.OrganizationId == facility.OrganizationId && !r.IsDeleted, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException("الدور التشغيلي غير موجود.");
        }
    }

    private async Task<WorkforceMember> LoadMemberForUpdateAsync(Guid facilityId, Guid memberId, CancellationToken cancellationToken)
    {
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        return await db.WorkforceMembers
            .Where(m => m.Id == memberId
                && !m.IsDeleted
                && (m.CurrentOperationalFacilityId == facilityId || m.HomeFacilityId == facilityId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("عضو القوى البشرية غير موجود.");
    }

    private static void ValidateMemberCreate(WorkforceMemberCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName)
            || string.IsNullOrWhiteSpace(request.EmployeeNumber)
            || string.IsNullOrWhiteSpace(request.JobTitle)
            || string.IsNullOrWhiteSpace(request.PrimarySpecialty))
        {
            throw new ArgumentException("بيانات العضو الأساسية مطلوبة.");
        }
    }

    private static IReadOnlyList<string> BuildMemberDataQuality(
        string employeeNumber,
        string displayName,
        EmploymentStatus status,
        DateTimeOffset? lastVerified)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(employeeNumber)) issues.Add("رقم الموظف مفقود.");
        if (string.IsNullOrWhiteSpace(displayName)) issues.Add("الاسم مفقود.");
        if (status == EmploymentStatus.Unknown) issues.Add("حالة التوظيف غير معروفة.");
        if (lastVerified is null) issues.Add("لا يوجد تحقق حديث.");
        return issues;
    }


    private static string ResolveWorkspaceWarningCode(string warning, int index)
    {
        if (warning.Contains("إجازة", StringComparison.Ordinal))
        {
            return WorkforceOperationalCatalog.DataQuality.MemberOnLeaveButScheduled;
        }

        if (warning.Contains("تعارض", StringComparison.Ordinal))
        {
            return WorkforceOperationalCatalog.DataQuality.ConflictingAssignments;
        }

        return $"workspace_warning_{index}";
    }

    private static MemberSummaryStats AccumulateMemberSummaryStats(
        IReadOnlyList<SummaryMemberRow> members,
        SummarySourceRows sourceRows)
    {
        var stats = new MemberSummaryStats
        {
            Scheduled = sourceRows.PublishedAssignments
                .Where(a => a.Status is not RosterAssignmentStatus.Cancelled and not RosterAssignmentStatus.Replaced)
                .Select(a => a.WorkforceMemberId)
                .Distinct()
                .Count()
        };
        foreach (var member in members)
        {
            AccumulateOneMember(member, sourceRows, stats);
        }

        return stats;
    }

    private static void AccumulateOneMember(
        SummaryMemberRow member,
        SummarySourceRows sourceRows,
        MemberSummaryStats stats)
    {
        if (string.IsNullOrWhiteSpace(member.EmployeeNumber)
            || string.IsNullOrWhiteSpace(member.DisplayName)
            || string.IsNullOrWhiteSpace(member.JobTitle)
            || member.EmploymentStatus == EmploymentStatus.Unknown)
        {
            stats.Missing++;
        }

        if (member.LastVerifiedAtUtc is null || member.LastVerifiedAtUtc < sourceRows.NowUtc.AddDays(-sourceRows.StaleVerificationDays))
        {
            stats.Stale++;
        }

        var eligible = WorkforceReadinessPolicy.IsEmploymentOperationallyEligible(member.EmploymentStatus, member.IsOperational);
        if (eligible)
        {
            stats.OperationallyEligible++;
        }

        var memberAvailability = sourceRows.Availability.Where(a => a.WorkforceMemberId == member.Id).ToList();
        if (memberAvailability.Any(a => a.AvailabilityType is AvailabilityType.AnnualLeave or AvailabilityType.SickLeave or AvailabilityType.EmergencyLeave))
        {
            stats.OnLeave++;
        }

        if (memberAvailability.Any(a => a.AvailabilityType == AvailabilityType.Training))
        {
            stats.InTraining++;
        }

        if (memberAvailability.Any(a => a.AvailabilityType == AvailabilityType.RestrictedDuty))
        {
            stats.Restricted++;
        }

        var blocked = memberAvailability.Any(a => WorkforceReadinessPolicy.IsAvailabilityBlocking(a.AvailabilityType, a.AffectsOperationalAvailability));
        var rosterRows = sourceRows.PublishedAssignments.Where(a => a.WorkforceMemberId == member.Id).ToList();
        if (rosterRows.Any(a => CountedRosterStatuses.Contains(a.Status)))
        {
            stats.Present++;
        }

        var hasAttendanceImport = memberAvailability.Any(a =>
            a.SourceType is WorkforceSourceType.Import or WorkforceSourceType.ExternalSystem
            && a.AvailabilityType == AvailabilityType.Available);
        var source = sourceRows.SourceResolver.Resolve(
            rosterRows.Count > 0,
            rosterRows.FirstOrDefault()?.Status,
            hasAttendanceImport,
            memberAvailability.Count > 0,
            sourceRows.ActiveAssignments.Any(a => a.WorkforceMemberId == member.Id));

        if (eligible
            && !blocked
            && source is WorkforcePresenceSource.PublishedRoster
                or WorkforcePresenceSource.AttendanceOrImport
                or WorkforcePresenceSource.AvailabilityEvent)
        {
            stats.OperationallyAvailable++;
        }

        AccumulateQualificationStats(member.Id, sourceRows.Qualifications, sourceRows.ActiveAssignments, sourceRows.NowUtc, stats);
    }

    private static void AccumulateQualificationStats(
        Guid memberId,
        IReadOnlyList<SummaryQualificationRow> qualifications,
        IReadOnlyList<SummaryAssignmentRow> activeAssignments,
        DateTimeOffset now,
        MemberSummaryStats stats)
    {
        var memberQuals = qualifications.Where(q => q.WorkforceMemberId == memberId).ToList();
        foreach (var roleId in activeAssignments.Where(a => a.WorkforceMemberId == memberId).Select(a => a.RoleDefinitionId).Distinct())
        {
            var valid = memberQuals.Any(q => WorkforceReadinessPolicy.IsQualificationValidForRole(
                q.Status, q.RoleDefinitionId, roleId, now, q.ExpiresAtUtc));
            if (valid)
            {
                stats.Qualified++;
            }
            else
            {
                stats.Unqualified++;
            }
        }

        foreach (var expiry in memberQuals.Select(q => q.ExpiresAtUtc).Where(v => v.HasValue).Select(v => v.GetValueOrDefault()))
        {
            if (stats.NearestExpiry is null || expiry < stats.NearestExpiry)
            {
                stats.NearestExpiry = expiry;
            }
        }
    }

    private static int CountCriticalPositionsAtRisk(
        IReadOnlyList<SummaryCriticalRow> critical,
        IReadOnlyList<SummaryRosterRow> publishedAssignments)
    {
        return critical.Count(c =>
        {
            var covered = publishedAssignments.Count(a =>
                a.RoleDefinitionId == c.RoleDefinitionId
                && CountedRosterStatuses.Contains(a.Status));
            var primaryFilled = Math.Min(c.RequiredPrimaryCount, covered);
            var alternateFilled = Math.Min(Math.Max(0, covered - c.RequiredPrimaryCount), c.RequiredAlternateCount);
            var vacantPrimary = Math.Max(0, c.RequiredPrimaryCount - primaryFilled);
            var vacantAlternate = Math.Max(0, c.RequiredAlternateCount - alternateFilled);
            var spof = c.RequiredPrimaryCount > 0 && primaryFilled <= 1 && vacantAlternate > 0;
            return vacantPrimary > 0 || vacantAlternate > 0 || spof;
        });
    }

    private static IReadOnlyList<string> BuildSummaryFatigueIndicators(
        int overtime,
        IReadOnlyList<SummaryRosterRow> publishedAssignments,
        IReadOnlyList<SummaryCriticalRow> critical,
        DateTimeOffset? nearestExpiry,
        DateTimeOffset now)
    {
        return WorkforceFatiguePolicy.Evaluate(new WorkforceFatiguePolicy.FatigueIndicatorInputs(
            OvertimeHoursInWindow: overtime,
            ConsecutiveShiftCount: publishedAssignments.GroupBy(a => a.WorkforceMemberId).Select(g => g.Count()).DefaultIfEmpty(0).Max(),
            CriticalRoleCoverageCount: publishedAssignments.Select(a => a.WorkforceMemberId).Distinct().Count(id =>
                critical.Any(c => publishedAssignments.Any(a => a.WorkforceMemberId == id && a.RoleDefinitionId == c.RoleDefinitionId))),
            CriticalRoleRequiredCount: critical.Sum(c => c.RequiredPrimaryCount),
            NearestQualificationExpiryUtc: nearestExpiry,
            AsOfUtc: now));
    }

    private static IReadOnlyList<string> BuildSummaryWarnings(
        int totalMembers,
        int required,
        int stale,
        int missing,
        int criticalAtRisk,
        WorkforceCoverageStatus status)
    {
        var warnings = new List<string>();
        if (totalMembers == 0) warnings.Add("لا توجد سجلات قوى بشرية لهذا السجن.");
        if (required == 0) warnings.Add("لا يوجد baseline احتياج معتمد للقوى البشرية.");
        if (stale > 0) warnings.Add("توجد سجلات تحتاج تحققًا حديثًا.");
        if (missing > 0) warnings.Add("توجد سجلات ناقصة البيانات.");
        if (criticalAtRisk > 0) warnings.Add("توجد مواقع حرجة غير مغطاة.");
        if (status is WorkforceCoverageStatus.Critical or WorkforceCoverageStatus.Unsafe)
        {
            warnings.Add("تغطية القوى البشرية دون المستوى الآمن.");
        }

        return warnings;
    }

    private static IReadOnlyList<string>? ParseRestrictionCodes(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<string>();
        }

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void ValidateImportRequest(WorkforceImportPreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceSystem))
        {
            throw new ArgumentException("نظام المصدر مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceReference))
        {
            throw new ArgumentException("مرجع المصدر مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(request.FileHash))
        {
            throw new ArgumentException("بصمة الملف مطلوبة.");
        }

        if (request.Rows.Count > 500)
        {
            throw new ArgumentException("الحد الأقصى للاستيراد 500 صف.");
        }
    }

    private async Task<ImportContext> BuildImportContextAsync(
        Guid facilityId,
        WorkforceImportPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var existingNumbers = await db.WorkforceMembers
            .AsNoTracking()
            .Where(m => m.OrganizationId == facility.OrganizationId && !m.IsDeleted)
            .Select(m => m.EmployeeNumber)
            .ToListAsync(cancellationToken);
        var unitIds = await db.FacilityUnits
            .AsNoTracking()
            .Where(u => u.FacilityId == facilityId && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        return new ImportContext(
            facilityId,
            facility.OrganizationId,
            request.ImportKind,
            request.SourceSystem.Trim(),
            request.SourceReference.Trim(),
            request.FileHash.Trim(),
            existingNumbers.Select(WorkforceAccessPolicy.NormalizeEmployeeNumber).ToHashSet(StringComparer.Ordinal),
            unitIds.ToHashSet(),
            timeProvider.GetUtcNow());
    }

    private static ImportValidationSummary ValidateImportRows(ImportContext context, WorkforceImportPreviewRequest request) =>
        request.ImportKind switch
        {
            WorkforceImportKind.PersonnelMaster => ValidatePersonnelMasterRows(context, request),
            WorkforceImportKind.Assignments => ValidateKeyedRows(request, row =>
                !string.IsNullOrWhiteSpace(row.EmployeeNumber) && row.RoleDefinitionId.HasValue && row.EffectiveFromUtc.HasValue,
                "رقم الموظف ومعرّف الدور وتاريخ السريان مطلوبة."),
            WorkforceImportKind.Qualifications => ValidateKeyedRows(request, row =>
                !string.IsNullOrWhiteSpace(row.EmployeeNumber) && !string.IsNullOrWhiteSpace(row.QualificationName),
                "رقم الموظف واسم المؤهل مطلوبان."),
            WorkforceImportKind.Rosters => ValidateKeyedRows(request, row =>
                !string.IsNullOrWhiteSpace(row.EmployeeNumber) && row.RoleDefinitionId.HasValue && row.ShiftDefinitionId.HasValue && row.DutyDate.HasValue,
                "رقم الموظف والدور والوردية وتاريخ المناوبة مطلوبة."),
            WorkforceImportKind.Availability => ValidateKeyedRows(request, row =>
                !string.IsNullOrWhiteSpace(row.EmployeeNumber) && row.AvailabilityStartsAtUtc.HasValue,
                "رقم الموظف وبداية التوفر مطلوبان."),
            WorkforceImportKind.AttendanceSummary => ValidateKeyedRows(request, row =>
                !string.IsNullOrWhiteSpace(row.EmployeeNumber)
                && row.AttendancePresentCount.HasValue
                && row.AttendanceAbsentCount.HasValue
                && row.AttendancePresentCount >= 0
                && row.AttendanceAbsentCount >= 0,
                "رقم الموظف وعدادات الحضور/الغياب مطلوبة وقيمها غير سالبة."),
            _ => new ImportValidationSummary(request.Rows.Count, Array.Empty<NormalizedImportRow>(), request.Rows.Count, 0, ["نوع الاستيراد غير مدعوم."])
        };

    private static ImportValidationSummary ValidatePersonnelMasterRows(ImportContext context, WorkforceImportPreviewRequest request)
    {
        var requestNumbers = new HashSet<string>(StringComparer.Ordinal);
        var known = new HashSet<string>(context.ExistingEmployeeNumbers, StringComparer.Ordinal);
        var errors = new List<string>();
        var validRows = new List<NormalizedImportRow>();
        var duplicate = 0;

        foreach (var row in request.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.EmployeeNumber)
                || string.IsNullOrWhiteSpace(row.DisplayName)
                || string.IsNullOrWhiteSpace(row.JobTitle)
                || string.IsNullOrWhiteSpace(row.PrimarySpecialty))
            {
                errors.Add($"صف {row.EmployeeNumber}: الحقول الأساسية مطلوبة.");
                continue;
            }

            var number = WorkforceAccessPolicy.NormalizeEmployeeNumber(row.EmployeeNumber);
            if (row.CurrentOperationalUnitId.HasValue && !context.ActiveFacilityUnitIds.Contains(row.CurrentOperationalUnitId.Value))
            {
                errors.Add($"صف {row.EmployeeNumber}: الوحدة غير موجودة داخل السجن.");
                continue;
            }

            if (!requestNumbers.Add(number) || known.Contains(number))
            {
                duplicate++;
                continue;
            }

            known.Add(number);
            validRows.Add(new NormalizedImportRow(
                number,
                row.DisplayName.Trim(),
                string.IsNullOrWhiteSpace(row.ExternalPersonnelId) ? null : row.ExternalPersonnelId.Trim(),
                row.EmploymentStatus,
                row.JobTitle.Trim(),
                row.PrimarySpecialty.Trim(),
                row.CurrentOperationalUnitId,
                row.IsOperational,
                row));
        }

        return new ImportValidationSummary(request.Rows.Count, validRows, errors.Count, duplicate, errors);
    }

    private static ImportValidationSummary ValidateKeyedRows(
        WorkforceImportPreviewRequest request,
        Func<WorkforceImportRow, bool> isValid,
        string errorMessage)
    {
        var errors = new List<string>();
        var validRows = new List<NormalizedImportRow>();
        var duplicate = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in request.Rows)
        {
            if (!isValid(row))
            {
                errors.Add($"صف {row.EmployeeNumber}: {errorMessage}");
                continue;
            }

            var employeeNumber = row.EmployeeNumber ?? string.Empty;
            var key = $"{WorkforceAccessPolicy.NormalizeEmployeeNumber(employeeNumber)}|{row.RoleDefinitionId}|{row.DutyDate}|{row.AvailabilityStartsAtUtc}|{row.QualificationName}";
            if (!seen.Add(key))
            {
                duplicate++;
                continue;
            }

            validRows.Add(new NormalizedImportRow(
                WorkforceAccessPolicy.NormalizeEmployeeNumber(employeeNumber),
                row.DisplayName?.Trim() ?? string.Empty,
                row.ExternalPersonnelId,
                row.EmploymentStatus,
                row.JobTitle?.Trim() ?? string.Empty,
                row.PrimarySpecialty?.Trim() ?? string.Empty,
                row.CurrentOperationalUnitId,
                row.IsOperational,
                row));
        }

        return new ImportValidationSummary(request.Rows.Count, validRows, errors.Count, duplicate, errors);
    }

    private async Task<WorkforceImportResult> ApplyImportAsync(
        ImportContext context,
        ImportValidationSummary validation,
        CancellationToken cancellationToken)
    {
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            var applied = 0;
            if (context.ImportKind == WorkforceImportKind.PersonnelMaster)
            {
                EnsureImportBatchCounts(validation, WorkforceImportBatchStatuses.Confirmed, validation.ValidRows.Count, context.NowUtc);
                foreach (var row in validation.ValidRows)
                {
                    db.Add(new WorkforceMember
                    {
                        OrganizationId = context.OrganizationId,
                        ExternalPersonnelId = row.ExternalPersonnelId,
                        DisplayName = row.DisplayName,
                        EmployeeNumber = row.EmployeeNumber,
                        EmploymentStatus = row.EmploymentStatus,
                        JobTitle = row.JobTitle,
                        PrimarySpecialty = row.PrimarySpecialty,
                        AdministrativeOrganizationId = context.OrganizationId,
                        HomeFacilityId = context.FacilityId,
                        CurrentOperationalFacilityId = context.FacilityId,
                        CurrentOperationalUnitId = row.CurrentOperationalUnitId,
                        IsOperational = row.IsOperational,
                        SourceType = WorkforceSourceType.Import,
                        SourceReference = context.SourceReference,
                        LastVerifiedAtUtc = context.NowUtc
                    });
                }

                applied = validation.ValidRows.Count;
            }
            else
            {
                applied = await ApplyNonPersonnelImportAsync(context, validation, ct);
                EnsureImportBatchCounts(validation, WorkforceImportBatchStatuses.Confirmed, applied, context.NowUtc);
            }

            var batch = new WorkforceImportBatch
            {
                FacilityId = context.FacilityId,
                ImportKind = context.ImportKind,
                SourceSystem = context.SourceSystem,
                SourceReference = context.SourceReference,
                FileHash = context.FileHash,
                SubmittedByUserId = currentUser.UserId,
                SubmittedAtUtc = context.NowUtc,
                Status = WorkforceImportBatchStatuses.Confirmed,
                TotalRows = validation.TotalRows,
                ValidRows = validation.ValidRows.Count,
                RejectedRows = validation.RejectedRows,
                DuplicateRows = validation.DuplicateRows,
                ConfirmedAtUtc = context.NowUtc,
                AppliedRows = applied
            };
            db.Add(batch);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (WorkforceAccessPolicy.IsWorkforceImportBatchesUniqueViolation(ex))
            {
                db.ClearChanges();
                var raced = await FindConfirmedImportAsync(context.FacilityId, context.ImportKind, context.SourceSystem, context.SourceReference, context.FileHash, ct);
                if (raced is not null)
                {
                    return raced;
                }

                throw;
            }
            catch (DbUpdateException ex) when (WorkforceAccessPolicy.IsWorkforceMembersUniqueViolation(ex))
            {
                throw new InvalidOperationException("رقم الموظف مستخدم مسبقًا داخل المنظمة.", ex);
            }

            await AuditAsync("WorkforceImportConfirmed", "WorkforceImportBatch", batch.Id, ct);
            return ToImportResult(validation, applied);
        }, cancellationToken);
    }

    private async Task<int> ApplyNonPersonnelImportAsync(
        ImportContext context,
        ImportValidationSummary validation,
        CancellationToken cancellationToken)
    {
        var numbers = validation.ValidRows.Select(r => r.EmployeeNumber).Distinct().ToList();
        var members = await db.WorkforceMembers
            .Where(m => m.OrganizationId == context.OrganizationId && !m.IsDeleted && numbers.Contains(m.EmployeeNumber))
            .Select(m => new { m.Id, m.EmployeeNumber })
            .ToListAsync(cancellationToken);
        var byNumber = members.ToDictionary(m => m.EmployeeNumber, m => m.Id, StringComparer.Ordinal);
        var applied = 0;
        foreach (var row in validation.ValidRows)
        {
            if (!byNumber.TryGetValue(row.EmployeeNumber, out var memberId))
            {
                continue;
            }

            applied += await ApplyNonPersonnelImportRowAsync(context, row.Source, memberId, cancellationToken);
        }

        return applied;
    }

    private async Task<int> ApplyNonPersonnelImportRowAsync(
        ImportContext context,
        WorkforceImportRow source,
        Guid memberId,
        CancellationToken cancellationToken) =>
        context.ImportKind switch
        {
            WorkforceImportKind.Assignments => ApplyAssignmentImportRow(context, source, memberId),
            WorkforceImportKind.Qualifications => ApplyQualificationImportRow(context, source, memberId),
            WorkforceImportKind.Availability => ApplyAvailabilityImportRow(context, source, memberId),
            WorkforceImportKind.Rosters => await ApplyRosterImportRowAsync(context, source, memberId, cancellationToken),
            WorkforceImportKind.AttendanceSummary => ApplyAttendanceImportRow(context, source, memberId),
            _ => 0
        };

    private int ApplyAssignmentImportRow(ImportContext context, WorkforceImportRow source, Guid memberId)
    {
        if (!source.RoleDefinitionId.HasValue || !source.EffectiveFromUtc.HasValue)
        {
            return 0;
        }

        db.Add(new WorkforceAssignment
        {
            WorkforceMemberId = memberId,
            FacilityId = context.FacilityId,
            FacilityUnitId = source.FacilityUnitId ?? source.CurrentOperationalUnitId,
            RoleDefinitionId = source.RoleDefinitionId.Value,
            AssignmentType = source.AssignmentType,
            EffectiveFromUtc = source.EffectiveFromUtc.Value,
            EffectiveToUtc = source.EffectiveToUtc,
            IsPrimary = true,
            SourceReference = context.SourceReference,
            ApprovedBy = currentUser.DisplayName
        });
        return 1;
    }

    private int ApplyQualificationImportRow(ImportContext context, WorkforceImportRow source, Guid memberId)
    {
        if (string.IsNullOrWhiteSpace(source.QualificationName))
        {
            return 0;
        }

        db.Add(new WorkforceQualification
        {
            WorkforceMemberId = memberId,
            QualificationType = source.QualificationType,
            RoleDefinitionId = source.RoleDefinitionId,
            Name = source.QualificationName.Trim(),
            ExpiresAtUtc = source.QualificationExpiresAtUtc,
            Status = QualificationStatus.Valid,
            VerifiedAtUtc = context.NowUtc,
            VerifiedBy = currentUser.DisplayName
        });
        return 1;
    }

    private int ApplyAvailabilityImportRow(ImportContext context, WorkforceImportRow source, Guid memberId)
    {
        if (!source.AvailabilityStartsAtUtc.HasValue)
        {
            return 0;
        }

        db.Add(new WorkforceAvailabilityEvent
        {
            WorkforceMemberId = memberId,
            AvailabilityType = source.AvailabilityType,
            StartsAtUtc = source.AvailabilityStartsAtUtc.Value,
            EndsAtUtc = source.AvailabilityEndsAtUtc,
            AffectsOperationalAvailability = true,
            SourceType = WorkforceSourceType.Import,
            SourceReference = context.SourceReference,
            RecordedAtUtc = context.NowUtc,
            RecordedBy = currentUser.DisplayName
        });
        return 1;
    }

    private async Task<int> ApplyRosterImportRowAsync(
        ImportContext context,
        WorkforceImportRow source,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        if (!source.ShiftDefinitionId.HasValue || !source.DutyDate.HasValue || !source.RoleDefinitionId.HasValue)
        {
            return 0;
        }

        var roster = await LoadOrCreateImportRosterAsync(context, source, cancellationToken);
        if (roster.Status == DutyRosterStatuses.Published)
        {
            return 0;
        }

        db.Add(new DutyRosterAssignment
        {
            DutyRosterId = roster.Id,
            WorkforceMemberId = memberId,
            RoleDefinitionId = source.RoleDefinitionId.Value,
            Status = RosterAssignmentStatus.Planned
        });
        return 1;
    }

    private async Task<DutyRoster> LoadOrCreateImportRosterAsync(
        ImportContext context,
        WorkforceImportRow source,
        CancellationToken cancellationToken)
    {
        var roster = await db.DutyRosters.FirstOrDefaultAsync(r =>
            r.FacilityId == context.FacilityId
            && r.ShiftDefinitionId == source.ShiftDefinitionId
            && r.DutyDate == source.DutyDate
            && !r.IsDeleted, cancellationToken);
        if (roster is not null)
        {
            return roster;
        }

        roster = new DutyRoster
        {
            FacilityId = context.FacilityId,
            FacilityUnitId = source.FacilityUnitId,
            ShiftDefinitionId = source.ShiftDefinitionId.GetValueOrDefault(),
            DutyDate = source.DutyDate.GetValueOrDefault(),
            Status = DutyRosterStatuses.Draft
        };
        db.Add(roster);
        return roster;
    }

    private int ApplyAttendanceImportRow(ImportContext context, WorkforceImportRow source, Guid memberId)
    {
        if ((source.AttendancePresentCount ?? 0) <= 0)
        {
            return 0;
        }

        db.Add(new WorkforceAvailabilityEvent
        {
            WorkforceMemberId = memberId,
            AvailabilityType = AvailabilityType.Available,
            StartsAtUtc = context.NowUtc.AddHours(-8),
            EndsAtUtc = context.NowUtc,
            AffectsOperationalAvailability = false,
            SourceType = WorkforceSourceType.Import,
            SourceReference = context.SourceReference,
            RecordedAtUtc = context.NowUtc,
            RecordedBy = currentUser.DisplayName
        });
        return 1;
    }

    private static void EnsureImportBatchCounts(
        ImportValidationSummary validation,
        string status,
        int appliedRows,
        DateTimeOffset? confirmedAtUtc)
    {
        if (!WorkforceAccessPolicy.IsValidImportBatchCounts(
                validation.TotalRows,
                validation.ValidRows.Count,
                validation.RejectedRows,
                validation.DuplicateRows,
                appliedRows,
                status,
                confirmedAtUtc))
        {
            throw new InvalidOperationException("إحصاءات دفعة الاستيراد غير متسقة.");
        }
    }

    private static WorkforceImportResult ToImportResult(ImportValidationSummary validation, int appliedRows) =>
        new(validation.TotalRows, validation.ValidRows.Count, validation.RejectedRows, validation.DuplicateRows, appliedRows, validation.Errors);

    private async Task<WorkforceImportResult?> FindConfirmedImportAsync(
        Guid facilityId,
        WorkforceImportKind importKind,
        string sourceSystem,
        string sourceReference,
        string fileHash,
        CancellationToken cancellationToken)
    {
        var batch = await db.WorkforceImportBatches
            .AsNoTracking()
            .Where(b => b.FacilityId == facilityId
                && b.ImportKind == importKind
                && b.SourceSystem == sourceSystem
                && b.SourceReference == sourceReference
                && b.FileHash == fileHash
                && b.Status == WorkforceImportBatchStatuses.Confirmed)
            .FirstOrDefaultAsync(cancellationToken);
        return batch is null
            ? null
            : new WorkforceImportResult(batch.TotalRows, batch.ValidRows, batch.RejectedRows, batch.DuplicateRows, batch.AppliedRows, Array.Empty<string>());
    }

    private void Require(string permission)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw new UnauthorizedAccessException("لا تملك الصلاحية المطلوبة.");
        }
    }

    private Task AuditAsync(string action, string entityType, Guid entityId, CancellationToken cancellationToken) =>
        AuditAsync(action, entityType, entityId.ToString(), cancellationToken);

    private async Task AuditAsync(string action, string entityType, string entityId, CancellationToken cancellationToken)
    {
        await audit.WriteAsync(new AuditEntry
        {
            Action = action,
            Module = "Workforce",
            EntityType = entityType,
            EntityId = entityId,
            NewValues = new Dictionary<string, string>
            {
                ["Actor"] = currentUser.DisplayName ?? currentUser.ExternalSubject ?? "unknown"
            },
            IsSensitiveView = false
        },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }


    private sealed class MemberSummaryStats
    {
        public int OperationallyEligible;
        public int OperationallyAvailable;
        public int OnLeave;
        public int InTraining;
        public int Restricted;
        public int Present;
        public int Scheduled;
        public int Qualified;
        public int Unqualified;
        public int Stale;
        public int Missing;
        public int Overtime = 0;
        public DateTimeOffset? NearestExpiry;
    }

    private sealed record SummaryMemberRow(
        Guid Id,
        EmploymentStatus EmploymentStatus,
        bool IsOperational,
        DateTimeOffset? LastVerifiedAtUtc,
        string EmployeeNumber,
        string DisplayName,
        string JobTitle);

    private sealed record SummaryRosterRow(Guid Id, Guid WorkforceMemberId, Guid RoleDefinitionId, RosterAssignmentStatus Status, Guid? ReplacementForAssignmentId);
    private sealed record SummaryAssignmentRow(Guid WorkforceMemberId, Guid RoleDefinitionId);
    private sealed record SummaryAvailabilityRow(Guid WorkforceMemberId, AvailabilityType AvailabilityType, bool AffectsOperationalAvailability, WorkforceSourceType SourceType);
    private sealed record SummaryQualificationRow(Guid WorkforceMemberId, Guid? RoleDefinitionId, QualificationStatus Status, DateTimeOffset? ExpiresAtUtc);
    private sealed record SummaryCriticalRow(Guid RoleDefinitionId, int RequiredPrimaryCount, int RequiredAlternateCount);
    private sealed record SummarySourceRows(
        IReadOnlyList<SummaryRosterRow> PublishedAssignments,
        IReadOnlyList<SummaryAssignmentRow> ActiveAssignments,
        IReadOnlyList<SummaryAvailabilityRow> Availability,
        IReadOnlyList<SummaryQualificationRow> Qualifications,
        DateTimeOffset NowUtc,
        IWorkforceSourceResolver SourceResolver,
        int StaleVerificationDays);

    private sealed record DataQualityMemberRow(
        Guid Id,
        string EmployeeNumber,
        string? ExternalPersonnelId,
        EmploymentStatus EmploymentStatus,
        Guid? HomeFacilityId,
        Guid? CurrentOperationalFacilityId,
        Guid? CurrentOperationalUnitId,
        DateTimeOffset? LastVerifiedAtUtc,
        Guid? UserId);

    private sealed class DataQualityIssueCounts
    {
        public int MissingNumber { get; set; }
        public int UnknownStatus { get; set; }
        public int MissingFacility { get; set; }
        public int MissingUnit { get; set; }
        public int Stale { get; set; }
        public int DuplicateExternal { get; set; }
        public int DuplicateEmployee { get; set; }
        public int ExpiredAssignments { get; set; }
        public int MissingApproval { get; set; }
        public int RosterWithoutCommander { get; set; }
        public int LeaveWhileRostered { get; set; }
        public int RetiredOnRoster { get; set; }
        public int MissingQualification { get; set; }
        public int MissingRole { get; set; }
        public int ShiftWithoutMinimum { get; set; }
        public int ExpiredQualification { get; set; }
        public int OverlappingRoster { get; set; }
        public int GapWithoutOwner { get; set; }
        public int InvalidUserLink { get; set; }
        public int UnknownAvailability { get; set; }
    }

    private sealed record ImportContext(
        Guid FacilityId,
        Guid OrganizationId,
        WorkforceImportKind ImportKind,
        string SourceSystem,
        string SourceReference,
        string FileHash,
        HashSet<string> ExistingEmployeeNumbers,
        HashSet<Guid> ActiveFacilityUnitIds,
        DateTimeOffset NowUtc);

    private sealed record NormalizedImportRow(
        string EmployeeNumber,
        string DisplayName,
        string? ExternalPersonnelId,
        EmploymentStatus EmploymentStatus,
        string JobTitle,
        string PrimarySpecialty,
        Guid? CurrentOperationalUnitId,
        bool IsOperational,
        WorkforceImportRow Source);

    private sealed record ImportValidationSummary(
        int TotalRows,
        IReadOnlyList<NormalizedImportRow> ValidRows,
        int RejectedRows,
        int DuplicateRows,
        IReadOnlyList<string> Errors);

    private sealed record FacilityScopeInfo(Guid FacilityId, Guid OrganizationId, Guid RegionId);
}
