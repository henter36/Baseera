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

public interface IWorkforceReconciliationService
{
    Task<WorkforceReconciliationResult> ReconcileAsync(Guid facilityId, CancellationToken cancellationToken);
}

public sealed class WorkforceReadinessOptions
{
    public int StaleVerificationDays { get; init; } = 30;
    public int MemberPageSizeLimit { get; init; } = 100;
}

public sealed class WorkforceReadinessService(
    IBaseeraDbContext db,
    IOrganizationalScopeService scope,
    ICurrentUser currentUser,
    IAuditService audit,
    IWorkforceSourceResolver sourceResolver,
    TimeProvider timeProvider)
    : IWorkforceReadinessQueryService,
      IWorkforceMemberCommandService,
      IStaffingRequirementService,
      IDutyRosterService,
      IWorkforceAvailabilityService,
      IWorkforceImportService,
      IWorkforceReconciliationService
{
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
        var roles = coverage
            .GroupBy(row => new { row.RoleDefinitionId, row.RoleCode, row.RoleNameAr })
            .Select(group => new WorkforceRoleDefinitionDto
            {
                Id = group.Key.RoleDefinitionId,
                Code = group.Key.RoleCode,
                NameAr = group.Key.RoleNameAr,
                NameEn = null,
                Category = WorkforceRoleCategory.Other,
                Criticality = WorkforceRoleCriticality.Medium,
                RequiresCertification = false,
                IsShiftBased = true,
                IsSensitive = false
            })
            .OrderBy(role => role.Code)
            .ToList();
        var dataQuality = new WorkforceDataQualityDto
        {
            TotalMembers = summary.TotalMembers,
            MissingEmployeeNumber = summary.MissingDataRecords,
            UnknownEmploymentStatus = 0,
            MissingHomeOrOperationalFacility = 0,
            StaleVerification = summary.StaleRecords,
            OpenImportIssues = 0,
            Warnings = summary.Warnings
        };
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

    private async Task<WorkforceSummaryDto> GetSummaryAsync(Guid facilityId, CancellationToken cancellationToken, bool persistSnapshot)
    {
        Require(PermissionCodes.WorkforceViewSummary);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var members = await MembersInFacility(facilityId)
            .Select(m => new
            {
                m.Id,
                m.EmploymentStatus,
                m.IsOperational,
                m.LastVerifiedAtUtc,
                m.EmployeeNumber,
                m.DisplayName,
                m.JobTitle
            })
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
            .Select(a => new { a.WorkforceMemberId, a.RoleDefinitionId, a.Status })
            .ToListAsync(cancellationToken);

        var activeAssignments = await db.WorkforceAssignments
            .AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.FacilityId == facilityId
                && a.EffectiveFromUtc <= now
                && (a.EffectiveToUtc == null || a.EffectiveToUtc > now))
            .Select(a => new { a.WorkforceMemberId, a.RoleDefinitionId })
            .ToListAsync(cancellationToken);

        var availability = await db.WorkforceAvailabilityEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted
                && memberIds.Contains(e.WorkforceMemberId)
                && e.StartsAtUtc <= now
                && (e.EndsAtUtc == null || e.EndsAtUtc > now))
            .Select(e => new { e.WorkforceMemberId, e.AvailabilityType, e.AffectsOperationalAvailability, e.SourceType })
            .ToListAsync(cancellationToken);

        var qualifications = await db.WorkforceQualifications
            .AsNoTracking()
            .Where(q => !q.IsDeleted && memberIds.Contains(q.WorkforceMemberId))
            .Select(q => new { q.WorkforceMemberId, q.RoleDefinitionId, q.Status, q.ExpiresAtUtc })
            .ToListAsync(cancellationToken);

        var critical = await db.CriticalPositionRequirements
            .AsNoTracking()
            .Where(c => !c.IsDeleted
                && c.FacilityId == facilityId
                && c.EffectiveFromUtc <= now
                && (c.EffectiveToUtc == null || c.EffectiveToUtc > now))
            .Select(c => new { c.RoleDefinitionId, c.RequiredPrimaryCount })
            .ToListAsync(cancellationToken);

        var operationallyEligible = 0;
        var operationallyAvailable = 0;
        var onLeave = 0;
        var inTraining = 0;
        var restricted = 0;
        var present = 0;
        var scheduled = publishedAssignments.Count;
        var qualified = 0;
        var unqualified = 0;
        var stale = 0;
        var missing = 0;
        var nearestExpiry = (DateTimeOffset?)null;
        var overtime = 0;

        foreach (var member in members)
        {
            if (string.IsNullOrWhiteSpace(member.EmployeeNumber)
                || string.IsNullOrWhiteSpace(member.DisplayName)
                || string.IsNullOrWhiteSpace(member.JobTitle)
                || member.EmploymentStatus == EmploymentStatus.Unknown)
            {
                missing++;
            }

            if (member.LastVerifiedAtUtc is null || member.LastVerifiedAtUtc < now.AddDays(-options.StaleVerificationDays))
            {
                stale++;
            }

            var eligible = WorkforceReadinessPolicy.IsEmploymentOperationallyEligible(member.EmploymentStatus, member.IsOperational);
            if (eligible)
            {
                operationallyEligible++;
            }

            var memberAvailability = availability.Where(a => a.WorkforceMemberId == member.Id).ToList();
            if (memberAvailability.Any(a => a.AvailabilityType == AvailabilityType.AnnualLeave || a.AvailabilityType == AvailabilityType.SickLeave || a.AvailabilityType == AvailabilityType.EmergencyLeave))
            {
                onLeave++;
            }

            if (memberAvailability.Any(a => a.AvailabilityType == AvailabilityType.Training))
            {
                inTraining++;
            }

            if (memberAvailability.Any(a => a.AvailabilityType == AvailabilityType.RestrictedDuty))
            {
                restricted++;
            }

            var blocked = memberAvailability.Any(a => WorkforceReadinessPolicy.IsAvailabilityBlocking(a.AvailabilityType, a.AffectsOperationalAvailability));
            var rosterRows = publishedAssignments.Where(a => a.WorkforceMemberId == member.Id).ToList();
            if (rosterRows.Any(a => a.Status is RosterAssignmentStatus.Present or RosterAssignmentStatus.Confirmed or RosterAssignmentStatus.Late))
            {
                present++;
            }

            var hasAttendanceImport = memberAvailability.Any(a =>
                a.SourceType is WorkforceSourceType.Import or WorkforceSourceType.ExternalSystem
                && a.AvailabilityType == AvailabilityType.Available);
            var source = sourceResolver.Resolve(
                rosterRows.Count > 0,
                rosterRows.FirstOrDefault()?.Status,
                hasAttendanceImport,
                memberAvailability.Count > 0,
                activeAssignments.Any(a => a.WorkforceMemberId == member.Id));
            _ = source; // documents precedence; presence for availability uses eligibility + not blocked

            if (eligible && !blocked)
            {
                operationallyAvailable++;
            }

            var memberQuals = qualifications.Where(q => q.WorkforceMemberId == member.Id).ToList();
            foreach (var roleId in activeAssignments.Where(a => a.WorkforceMemberId == member.Id).Select(a => a.RoleDefinitionId).Distinct())
            {
                var valid = memberQuals.Any(q => WorkforceReadinessPolicy.IsQualificationValidForRole(
                    q.Status, q.RoleDefinitionId, roleId, now, q.ExpiresAtUtc));
                if (valid)
                {
                    qualified++;
                }
                else
                {
                    unqualified++;
                }
            }

            foreach (var expiry in memberQuals.Where(q => q.ExpiresAtUtc.HasValue).Select(q => q.ExpiresAtUtc!.Value))
            {
                if (nearestExpiry is null || expiry < nearestExpiry)
                {
                    nearestExpiry = expiry;
                }
            }
        }

        var coverage = WorkforceReadinessPolicy.Calculate(new WorkforceCoverageInputs(
            required,
            minimumSafe,
            activeAssignments.Select(a => a.WorkforceMemberId).Distinct().Count(),
            scheduled,
            present,
            operationallyAvailable,
            qualified,
            unqualified,
            members.Count(m => m.EmploymentStatus == EmploymentStatus.Suspended),
            onLeave,
            inTraining,
            restricted,
            overtime,
            requirements.Count > 0,
            publishedAssignments.Count > 0 || activeAssignments.Count > 0));

        var criticalAtRisk = critical.Count(c =>
        {
            var covered = publishedAssignments.Count(a =>
                a.RoleDefinitionId == c.RoleDefinitionId
                && a.Status is RosterAssignmentStatus.Present or RosterAssignmentStatus.Confirmed or RosterAssignmentStatus.Planned);
            return covered < c.RequiredPrimaryCount;
        });

        var warnings = BuildSummaryWarnings(members.Count, required, stale, missing, criticalAtRisk, coverage.Status);
        var fatigue = WorkforceFatiguePolicy.Evaluate(new WorkforceFatiguePolicy.FatigueIndicatorInputs(
            overtime,
            publishedAssignments.GroupBy(a => a.WorkforceMemberId).Select(g => g.Count()).DefaultIfEmpty(0).Max(),
            publishedAssignments.Select(a => a.WorkforceMemberId).Distinct().Count(id =>
                critical.Any(c => publishedAssignments.Any(a => a.WorkforceMemberId == id && a.RoleDefinitionId == c.RoleDefinitionId))),
            critical.Sum(c => c.RequiredPrimaryCount),
            nearestExpiry,
            now));

        var summary = new WorkforceSummaryDto
        {
            FacilityId = facilityId,
            TotalMembers = members.Count,
            OperationallyEligible = operationallyEligible,
            Required = required,
            MinimumSafe = minimumSafe,
            Scheduled = scheduled,
            Present = present,
            OperationallyAvailable = operationallyAvailable,
            OnLeave = onLeave,
            InTraining = inTraining,
            Restricted = restricted,
            Gap = coverage.Gap,
            SafeGap = coverage.SafeGap,
            CoverageRate = coverage.CoverageRate,
            QualificationCoverage = coverage.QualificationCoverage,
            CoverageStatus = coverage.Status,
            CriticalPositionsAtRisk = criticalAtRisk,
            StaleRecords = stale,
            MissingDataRecords = missing,
            FreshnessStatus = WorkforceReadinessPolicy.ResolveFreshness(members.Count, stale),
            ConfidenceLevel = WorkforceReadinessPolicy.ResolveConfidence(members.Count, warnings.Count),
            IsPartial = warnings.Count > 0,
            Warnings = warnings,
            FatigueIndicators = fatigue,
            GeneratedAtUtc = now,
            DataEffectiveAtUtc = members
                .Select(m => m.LastVerifiedAtUtc)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .DefaultIfEmpty()
                .Max() is var max && max != default
                ? max
                : null
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
                Scheduled = g.Count(),
                Present = g.Count(a => a.Status == RosterAssignmentStatus.Present
                    || a.Status == RosterAssignmentStatus.Confirmed
                    || a.Status == RosterAssignmentStatus.Late)
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
                m.LastVerifiedAtUtc
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

        IReadOnlyList<string>? restrictionCodes = canViewSensitive
            ? availability.Where(a => a.RestrictionCodes is not null).SelectMany(a => a.RestrictionCodes!).Distinct(StringComparer.Ordinal).ToList()
            : null;

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
                LastVerifiedAtUtc = member.LastVerifiedAtUtc,
                DataQualityIssues = BuildMemberDataQuality(member.EmployeeNumber, member.DisplayName, member.EmploymentStatus, member.LastVerifiedAtUtc)
            },
            Assignments = assignments,
            Qualifications = qualifications,
            Availability = availability,
            RestrictionCodes = restrictionCodes
        };
    }

    public async Task<WorkforceDataQualityDto> GetDataQualityAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceViewSummary);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var staleBefore = now.AddDays(-options.StaleVerificationDays);
        var members = await MembersInFacility(facilityId)
            .Select(m => new { m.EmployeeNumber, m.EmploymentStatus, m.HomeFacilityId, m.CurrentOperationalFacilityId, m.LastVerifiedAtUtc })
            .ToListAsync(cancellationToken);
        var missingNumber = members.Count(m => string.IsNullOrWhiteSpace(m.EmployeeNumber));
        var unknownStatus = members.Count(m => m.EmploymentStatus == EmploymentStatus.Unknown);
        var missingFacility = members.Count(m => m.HomeFacilityId is null && m.CurrentOperationalFacilityId is null);
        var stale = members.Count(m => m.LastVerifiedAtUtc is null || m.LastVerifiedAtUtc < staleBefore);
        var warnings = new List<string>();
        if (missingNumber > 0) warnings.Add("أرقام موظفين مفقودة.");
        if (unknownStatus > 0) warnings.Add("حالات توظيف غير معروفة.");
        if (missingFacility > 0) warnings.Add("أعضاء بلا منشأة منزلية أو تشغيلية.");
        if (stale > 0) warnings.Add("سجلات تحتاج تحققًا حديثًا.");
        return new WorkforceDataQualityDto
        {
            TotalMembers = members.Count,
            MissingEmployeeNumber = missingNumber,
            UnknownEmploymentStatus = unknownStatus,
            MissingHomeOrOperationalFacility = missingFacility,
            StaleVerification = stale,
            OpenImportIssues = 0,
            Warnings = warnings
        };
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
        var member = await LoadMemberForUpdateAsync(facilityId, memberId, cancellationToken);
        if (request.SupervisorWorkforceMemberId == member.Id)
        {
            throw new InvalidOperationException("لا يمكن تعيين العضو مشرفًا على نفسه.");
        }

        if (request.CurrentOperationalUnitId.HasValue)
        {
            await EnsureUnitInFacilityAsync(facilityId, request.CurrentOperationalUnitId.Value, cancellationToken);
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
        await db.SaveChangesAsync(cancellationToken);
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

        var existing = await db.WorkforceAssignments
            .AsNoTracking()
            .Where(a => !a.IsDeleted && a.WorkforceMemberId == request.WorkforceMemberId)
            .Select(a => new { a.IsPrimary, a.EffectiveFromUtc, a.EffectiveToUtc })
            .ToListAsync(cancellationToken);
        if (WorkforceAssignmentPolicy.HasConflictingPrimaryAssignment(
                existing.Select(a => (a.IsPrimary, a.EffectiveFromUtc, a.EffectiveToUtc)),
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.IsPrimary))
        {
            throw new InvalidOperationException("يوجد تكليف أساسي متداخل لنفس العضو.");
        }

        var assignment = new WorkforceAssignment
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
        db.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);
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
        await GetSummaryAsync(facilityId, cancellationToken);
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
        var existing = await FindConfirmedImportAsync(facilityId, sourceSystem, sourceReference, fileHash, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var context = await BuildImportContextAsync(facilityId, request, cancellationToken);
        var validation = ValidateImportRows(context, request);
        if (validation.ValidRows.Count == 0)
        {
            EnsureImportBatchCounts(validation, WorkforceImportBatchStatuses.Previewed, 0, null);
            return ToImportResult(validation, 0);
        }

        return await ApplyImportAsync(context, validation, cancellationToken);
    }

    public async Task<WorkforceReconciliationResult> ReconcileAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceReconcile);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var quality = await GetDataQualityAsync(facilityId, cancellationToken);
        var openIssues = quality.MissingEmployeeNumber
            + quality.UnknownEmploymentStatus
            + quality.MissingHomeOrOperationalFacility
            + quality.StaleVerification;
        await AuditAsync("WorkforceReconciled", "Facility", facilityId, cancellationToken);
        return new WorkforceReconciliationResult(openIssues, MarkedReconciled: true);
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
        var latest = await db.WorkforceReadinessSnapshots
            .Where(s => s.FacilityId == facilityId
                && s.FacilityUnitId == null
                && s.ShiftDefinitionId == null
                && s.RoleDefinitionId == null)
            .OrderByDescending(s => s.CapturedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null && latest.CapturedAtUtc.Date == summary.GeneratedAtUtc.UtcDateTime.Date)
        {
            latest.CapturedAtUtc = summary.GeneratedAtUtc;
            latest.Required = summary.Required;
            latest.MinimumSafe = summary.MinimumSafe;
            latest.Assigned = summary.OperationallyEligible;
            latest.Scheduled = summary.Scheduled;
            latest.Present = summary.Present;
            latest.OperationallyAvailable = summary.OperationallyAvailable;
            latest.Gap = summary.Gap;
            latest.SafeGap = summary.SafeGap;
            latest.CoverageRate = summary.CoverageRate;
            latest.QualificationCoverage = summary.QualificationCoverage;
            latest.Freshness = summary.FreshnessStatus;
            latest.Confidence = summary.ConfidenceLevel;
            latest.SourceStatus = summary.CoverageStatus.ToString();
            latest.CoverageStatus = summary.CoverageStatus;
            latest.OnLeave = summary.OnLeave;
            latest.InTraining = summary.InTraining;
            latest.Restricted = summary.Restricted;
        }
        else
        {
            db.Add(new WorkforceReadinessSnapshot
            {
                FacilityId = facilityId,
                CapturedAtUtc = summary.GeneratedAtUtc,
                Required = summary.Required,
                MinimumSafe = summary.MinimumSafe,
                Assigned = summary.OperationallyEligible,
                Scheduled = summary.Scheduled,
                Present = summary.Present,
                OperationallyAvailable = summary.OperationallyAvailable,
                Gap = summary.Gap,
                SafeGap = summary.SafeGap,
                CoverageRate = summary.CoverageRate,
                QualificationCoverage = summary.QualificationCoverage,
                Freshness = summary.FreshnessStatus,
                Confidence = summary.ConfidenceLevel,
                SourceStatus = summary.CoverageStatus.ToString(),
                CoverageStatus = summary.CoverageStatus,
                OnLeave = summary.OnLeave,
                InTraining = summary.InTraining,
                Restricted = summary.Restricted
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<FacilityScopeInfo> EnsureFacilityVisibleAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        if (!scope.CanAccessFacility(facilityId))
        {
            throw new KeyNotFoundException("السجن غير موجود.");
        }

        return await db.Facilities
            .AsNoTracking()
            .Where(f => f.Id == facilityId && f.IsActive)
            .Select(f => new FacilityScopeInfo(f.Id, f.Region.OrganizationId, f.RegionId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("السجن غير موجود.");
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
            request.SourceSystem.Trim(),
            request.SourceReference.Trim(),
            request.FileHash.Trim(),
            existingNumbers.Select(WorkforceAccessPolicy.NormalizeEmployeeNumber).ToHashSet(StringComparer.Ordinal),
            unitIds.ToHashSet(),
            timeProvider.GetUtcNow());
    }

    private static ImportValidationSummary ValidateImportRows(ImportContext context, WorkforceImportPreviewRequest request)
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
                row.IsOperational));
        }

        return new ImportValidationSummary(request.Rows.Count, validRows, errors.Count, duplicate, errors);
    }

    private async Task<WorkforceImportResult> ApplyImportAsync(
        ImportContext context,
        ImportValidationSummary validation,
        CancellationToken cancellationToken)
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

        db.Add(new WorkforceImportBatch
        {
            FacilityId = context.FacilityId,
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
            AppliedRows = validation.ValidRows.Count
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (WorkforceAccessPolicy.IsWorkforceImportBatchesUniqueViolation(ex))
        {
            db.ClearChanges();
            var raced = await FindConfirmedImportAsync(context.FacilityId, context.SourceSystem, context.SourceReference, context.FileHash, cancellationToken);
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

        await AuditAsync("WorkforceImportConfirmed", "WorkforceImportBatch", context.FacilityId, cancellationToken);
        return ToImportResult(validation, validation.ValidRows.Count);
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
        string sourceSystem,
        string sourceReference,
        string fileHash,
        CancellationToken cancellationToken)
    {
        var batch = await db.WorkforceImportBatches
            .AsNoTracking()
            .Where(b => b.FacilityId == facilityId
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

    private async Task AuditAsync(string action, string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        await audit.WriteAsync(new AuditEntry
        {
            Action = action,
            Module = "Workforce",
            EntityType = entityType,
            EntityId = entityId.ToString(),
            NewValues = new Dictionary<string, string>
            {
                ["Actor"] = currentUser.DisplayName ?? currentUser.ExternalSubject ?? "unknown"
            },
            IsSensitiveView = false
        },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record ImportContext(
        Guid FacilityId,
        Guid OrganizationId,
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
        bool IsOperational);

    private sealed record ImportValidationSummary(
        int TotalRows,
        IReadOnlyList<NormalizedImportRow> ValidRows,
        int RejectedRows,
        int DuplicateRows,
        IReadOnlyList<string> Errors);

    private sealed record FacilityScopeInfo(Guid FacilityId, Guid OrganizationId, Guid RegionId);
}
