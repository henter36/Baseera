namespace Baseera.Application.Workforce;

using System.Globalization;
using System.Text;
using Baseera.Domain.Identity;
using Baseera.Domain.Workforce;
using Microsoft.EntityFrameworkCore;

public sealed partial class WorkforceReadinessService
{
    private static readonly HashSet<string> AllowedResolutionActions = new(StringComparer.OrdinalIgnoreCase)
    {
        WorkforceReconciliationActions.Acknowledge,
        WorkforceReconciliationActions.Corrected,
        WorkforceReconciliationActions.Deferred,
        WorkforceReconciliationActions.FalsePositive,
        "Acknowledged",
        "CorrectedAtSource",
        "DuplicateMerged"
    };

    public async Task<IReadOnlyList<WorkforceCriticalPositionDto>> ListCriticalPositionsAsync(
        Guid facilityId,
        CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceViewCoverage);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        return await LoadCriticalPositionDtosAsync(facilityId, cancellationToken);
    }

    private async Task<IReadOnlyList<WorkforceCriticalPositionDto>> LoadCriticalPositionDtosAsync(
        Guid facilityId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var requirements = await db.CriticalPositionRequirements
            .AsNoTracking()
            .Where(c => !c.IsDeleted
                && c.FacilityId == facilityId
                && c.EffectiveFromUtc <= now
                && (c.EffectiveToUtc == null || c.EffectiveToUtc > now))
            .Select(c => new
            {
                c.Id,
                c.RoleDefinitionId,
                RoleCode = c.RoleDefinition.Code,
                RoleNameAr = c.RoleDefinition.NameAr,
                c.FacilityUnitId,
                c.ShiftDefinitionId,
                c.RequiredPrimaryCount,
                c.RequiredAlternateCount,
                c.Criticality
            })
            .ToListAsync(cancellationToken);

        var roster = await db.DutyRosterAssignments
            .AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.DutyRoster.FacilityId == facilityId
                && a.DutyRoster.Status == DutyRosterStatuses.Published
                && a.DutyRoster.DutyDate == today
                && CountedRosterStatuses.Contains(a.Status))
            .Select(a => new
            {
                a.RoleDefinitionId,
                a.DutyRoster.FacilityUnitId,
                a.DutyRoster.ShiftDefinitionId
            })
            .ToListAsync(cancellationToken);

        var actingMemberIds = await db.WorkforceAssignments
            .AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.FacilityId == facilityId
                && a.AssignmentType == AssignmentType.Acting
                && a.EffectiveFromUtc <= now
                && (a.EffectiveToUtc == null || a.EffectiveToUtc > now))
            .Select(a => new { a.RoleDefinitionId, a.FacilityUnitId })
            .ToListAsync(cancellationToken);

        return requirements.Select(req =>
        {
            var matching = roster.Where(r =>
                r.RoleDefinitionId == req.RoleDefinitionId
                && (req.FacilityUnitId == null || r.FacilityUnitId == req.FacilityUnitId)
                && (req.ShiftDefinitionId == null || r.ShiftDefinitionId == req.ShiftDefinitionId))
                .ToList();
            var primaryFilled = Math.Min(req.RequiredPrimaryCount, matching.Count);
            var alternateFilled = Math.Min(
                Math.Max(0, matching.Count - req.RequiredPrimaryCount),
                req.RequiredAlternateCount);
            var vacantPrimary = Math.Max(0, req.RequiredPrimaryCount - primaryFilled);
            var vacantAlternate = Math.Max(0, req.RequiredAlternateCount - alternateFilled);
            var acting = actingMemberIds.Count(a =>
                a.RoleDefinitionId == req.RoleDefinitionId
                && (req.FacilityUnitId == null || a.FacilityUnitId == req.FacilityUnitId));
            var spof = req.RequiredPrimaryCount > 0 && primaryFilled <= 1 && vacantAlternate > 0;
            var statusAr = vacantPrimary > 0
                ? "شاغر"
                : vacantAlternate > 0
                    ? "بلا بديل كافٍ"
                    : acting > 0
                        ? "تكليف مؤقت"
                        : "مغطى";
            return new WorkforceCriticalPositionDto
            {
                Id = req.Id,
                RoleDefinitionId = req.RoleDefinitionId,
                RoleCode = req.RoleCode,
                RoleNameAr = req.RoleNameAr,
                FacilityUnitId = req.FacilityUnitId,
                ShiftDefinitionId = req.ShiftDefinitionId,
                RequiredPrimaryCount = req.RequiredPrimaryCount,
                RequiredAlternateCount = req.RequiredAlternateCount,
                PrimaryFilled = primaryFilled,
                AlternateFilled = alternateFilled,
                VacantPrimary = vacantPrimary,
                VacantAlternate = vacantAlternate,
                ActingCount = acting,
                SinglePointOfFailure = spof,
                Criticality = req.Criticality,
                StatusAr = statusAr
            };
        }).OrderByDescending(r => r.VacantPrimary).ThenBy(r => r.RoleCode).ToList();
    }

    public async Task<(string FileName, string ContentType, byte[] Content)> ExportAsync(
        Guid facilityId,
        string? search,
        int pageSize,
        CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceExport);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var limit = Math.Clamp(pageSize <= 0 ? WorkforceExportOptions.DefaultLimit : pageSize, 1, WorkforceExportOptions.MaxLimit);
        var query = MembersInFacility(facilityId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(m => m.EmployeeNumber.Contains(term) || m.DisplayName.Contains(term) || m.JobTitle.Contains(term));
        }

        var rows = await query
            .OrderBy(m => m.EmployeeNumber)
            .Take(limit)
            .Select(m => new
            {
                m.EmployeeNumber,
                m.DisplayName,
                m.JobTitle,
                m.PrimarySpecialty,
                m.EmploymentStatus,
                Unit = m.CurrentOperationalUnit != null ? m.CurrentOperationalUnit.NameAr : "",
                m.IsOperational,
                m.LastVerifiedAtUtc
            })
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("EmployeeNumber,DisplayName,JobTitle,PrimarySpecialty,EmploymentStatus,Unit,IsOperational,LastVerifiedAtUtc");
        foreach (var row in rows)
        {
            sb.Append(Csv(row.EmployeeNumber)).Append(',')
                .Append(Csv(row.DisplayName)).Append(',')
                .Append(Csv(row.JobTitle)).Append(',')
                .Append(Csv(row.PrimarySpecialty)).Append(',')
                .Append(row.EmploymentStatus).Append(',')
                .Append(Csv(row.Unit)).Append(',')
                .Append(row.IsOperational ? "true" : "false").Append(',')
                .AppendLine(row.LastVerifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "");
        }

        await AuditAsync("WorkforceExported", "Facility", facilityId, cancellationToken);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return ($"workforce-{facilityId:N}.csv", "text/csv; charset=utf-8", bytes);
    }

    public async Task<WorkforceReconciliationListDto> ListReconciliationAsync(
        Guid facilityId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceReconcile);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 100);
        var items = await DetectReconciliationItemsAsync(facilityId, cancellationToken);
        var resolved = await db.WorkforceReconciliationResolutions
            .AsNoTracking()
            .Where(r => r.FacilityId == facilityId)
            .Select(r => r.ItemKey)
            .ToListAsync(cancellationToken);
        var resolvedSet = resolved.ToHashSet(StringComparer.Ordinal);
        var open = items.Where(i => !resolvedSet.Contains(i.Id)).ToList();
        var pageItems = open.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new WorkforceReconciliationListDto
        {
            Items = pageItems,
            TotalCount = open.Count,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task ResolveReconciliationAsync(
        Guid facilityId,
        string itemId,
        WorkforceReconciliationResolveRequest request,
        CancellationToken cancellationToken)
    {
        Require(PermissionCodes.WorkforceReconcile);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new InvalidOperationException("معرّف عنصر المصالحة مطلوب.");
        }

        if (!AllowedResolutionActions.Contains(request.ResolutionAction.Trim()))
        {
            throw new InvalidOperationException("إجراء المصالحة غير مدعوم.");
        }

        var items = await DetectReconciliationItemsAsync(facilityId, cancellationToken);
        var match = items.FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.Ordinal));
        if (match is null)
        {
            throw new KeyNotFoundException("عنصر المصالحة غير موجود.");
        }

        var exists = await db.WorkforceReconciliationResolutions.AnyAsync(
            r => r.FacilityId == facilityId && r.ItemKey == itemId,
            cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("تم حل عنصر المصالحة مسبقًا.");
        }

        db.Add(new WorkforceReconciliationResolution
        {
            FacilityId = facilityId,
            ItemKey = itemId,
            IssueType = match.IssueType,
            ResolutionAction = request.ResolutionAction.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            ResolvedAtUtc = timeProvider.GetUtcNow(),
            ResolvedBy = currentUser.DisplayName
        });
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("WorkforceReconciled", "WorkforceReconciliationItem", itemId, cancellationToken);
    }

    public async Task<WorkforceReconciliationResult> ReconcileAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        var list = await ListReconciliationAsync(facilityId, 1, 100, cancellationToken);
        await AuditAsync("WorkforceReconciled", "Facility", facilityId, cancellationToken);
        return new WorkforceReconciliationResult(list.TotalCount, MarkedReconciled: list.TotalCount == 0);
    }

    private async Task<IReadOnlyList<WorkforceReconciliationItemDto>> DetectReconciliationItemsAsync(
        Guid facilityId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var staleBefore = now.AddDays(-options.StaleVerificationDays);

        var members = await MembersInFacility(facilityId)
            .Select(m => new ReconciliationMember(
                m.Id,
                m.ExternalPersonnelId,
                m.SourceReference,
                m.UserId,
                m.EmploymentStatus,
                m.LastVerifiedAtUtc,
                m.CurrentOperationalFacilityId,
                m.HomeFacilityId))
            .ToListAsync(cancellationToken);

        var userIds = members.Where(m => m.UserId.HasValue).Select(m => m.UserId!.Value).Distinct().ToList();
        var existingUsers = userIds.Count == 0
            ? new HashSet<Guid>()
            : (await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).Select(u => u.Id).ToListAsync(cancellationToken))
                .ToHashSet();

        var assignments = await db.WorkforceAssignments
            .AsNoTracking()
            .Where(a => !a.IsDeleted && a.FacilityId == facilityId)
            .Select(a => new ReconciliationAssignment(a.Id, a.WorkforceMemberId, a.IsPrimary, a.EffectiveFromUtc, a.EffectiveToUtc, a.FacilityId))
            .ToListAsync(cancellationToken);

        var rosterRows = await db.DutyRosterAssignments
            .AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.DutyRoster.FacilityId == facilityId
                && a.DutyRoster.DutyDate == today
                && a.DutyRoster.Status == DutyRosterStatuses.Published)
            .Select(a => new ReconciliationRosterRow(a.Id, a.WorkforceMemberId, a.Status))
            .ToListAsync(cancellationToken);

        var memberIds = members.Select(m => m.Id).ToList();
        var availability = await db.WorkforceAvailabilityEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted
                && memberIds.Contains(e.WorkforceMemberId)
                && e.StartsAtUtc <= now
                && (e.EndsAtUtc == null || e.EndsAtUtc > now))
            .Select(e => new ReconciliationAvailability(e.WorkforceMemberId, e.AvailabilityType, e.AffectsOperationalAvailability))
            .ToListAsync(cancellationToken);

        var unpublishedIds = await db.DutyRosters
            .AsNoTracking()
            .Where(r => !r.IsDeleted
                && r.FacilityId == facilityId
                && r.DutyDate >= today
                && r.DutyDate <= today.AddDays(1)
                && r.Status == DutyRosterStatuses.Draft)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var noAlternateCriticalPositionIds = (await LoadCriticalPositionDtosAsync(facilityId, cancellationToken))
            .Where(c => c.VacantAlternate > 0 && c.RequiredAlternateCount > 0)
            .Select(c => c.Id)
            .ToList();

        var input = new WorkforceReconciliationScanInput(
            ExternalIds: members.Select(m => (m.Id, m.ExternalPersonnelId)).ToList(),
            ConflictingPrimaryAssignments: DetectConflictingPrimaryAssignments(assignments),
            LeaveWhileRostered: DetectLeaveWhileRostered(rosterRows, availability),
            RetirementWhileRostered: DetectRetirementWhileRostered(rosterRows, members),
            StaleMemberIds: members.Where(m => m.LastVerifiedAtUtc is null || m.LastVerifiedAtUtc < staleBefore).Select(m => m.Id).ToList(),
            InvalidUserLinkMemberIds: members.Where(m => m.UserId.HasValue && !existingUsers.Contains(m.UserId!.Value)).Select(m => m.Id).ToList(),
            AssignmentOutsideFacilityIds: DetectAssignmentsOutsideFacility(assignments, members),
            UnpublishedRosterIds: unpublishedIds,
            SourceConflictMemberIds: DetectSourceConflictMembers(members),
            UnknownAvailabilityMemberIds: members.Where(m => m.EmploymentStatus == EmploymentStatus.Unknown).Select(m => m.Id).ToList(),
            NoAlternateCriticalPositionIds: noAlternateCriticalPositionIds,
            DuplicateRosterSlotMemberIds: rosterRows.GroupBy(r => r.WorkforceMemberId).Where(g => g.Count() > 1).Select(g => g.Key).ToList());

        return WorkforceReconciliationDetector.Detect(input)
            .Select(issue => ToReconciliationItem(issue, now))
            .GroupBy(i => i.Id)
            .Select(g => g.First())
            .OrderByDescending(i => SeverityRank(i.Severity))
            .ThenBy(i => i.IssueType)
            .ToList();
    }

    private static IReadOnlyList<(Guid MemberId, Guid AssignmentId)> DetectConflictingPrimaryAssignments(
        IReadOnlyList<ReconciliationAssignment> assignments)
    {
        var conflicts = new List<(Guid MemberId, Guid AssignmentId)>();
        foreach (var memberGroup in assignments.Where(a => a.IsPrimary).GroupBy(a => a.WorkforceMemberId))
        {
            var list = memberGroup.OrderBy(a => a.EffectiveFromUtc).ToList();
            for (var i = 1; i < list.Count; i++)
            {
                if (!WorkforceAssignmentPolicy.PeriodsOverlap(
                        list[i - 1].EffectiveFromUtc,
                        list[i - 1].EffectiveToUtc,
                        list[i].EffectiveFromUtc,
                        list[i].EffectiveToUtc))
                {
                    continue;
                }

                conflicts.Add((memberGroup.Key, list[i].Id));
                break;
            }
        }

        return conflicts;
    }

    private static IReadOnlyList<Guid> DetectAssignmentsOutsideFacility(
        IReadOnlyList<ReconciliationAssignment> assignments,
        IReadOnlyList<ReconciliationMember> members) =>
        assignments
            .Where(a => members.Any(m => m.Id == a.WorkforceMemberId
                && m.CurrentOperationalFacilityId.HasValue
                && m.CurrentOperationalFacilityId != a.FacilityId
                && m.HomeFacilityId != a.FacilityId))
            .Select(a => a.Id)
            .ToList();

    private static IReadOnlyList<(Guid MemberId, Guid RosterAssignmentId)> DetectLeaveWhileRostered(
        IReadOnlyList<ReconciliationRosterRow> rosterRows,
        IReadOnlyList<ReconciliationAvailability> availability) =>
        rosterRows
            .Where(row => row.Status is RosterAssignmentStatus.Planned or RosterAssignmentStatus.Confirmed or RosterAssignmentStatus.Present)
            .Where(row => availability.Any(a => a.WorkforceMemberId == row.WorkforceMemberId
                && WorkforceReadinessPolicy.IsAvailabilityBlocking(a.AvailabilityType, a.AffectsOperationalAvailability)))
            .Select(row => (row.WorkforceMemberId, row.Id))
            .ToList();

    private static IReadOnlyList<(Guid MemberId, Guid RosterAssignmentId)> DetectRetirementWhileRostered(
        IReadOnlyList<ReconciliationRosterRow> rosterRows,
        IReadOnlyList<ReconciliationMember> members) =>
        rosterRows
            .Where(row => members.Any(m => m.Id == row.WorkforceMemberId
                && m.EmploymentStatus is EmploymentStatus.Retired or EmploymentStatus.Terminated))
            .Select(row => (row.WorkforceMemberId, row.Id))
            .ToList();

    private static IReadOnlyList<Guid> DetectSourceConflictMembers(IReadOnlyList<ReconciliationMember> members) =>
        members
            .Where(m => !string.IsNullOrWhiteSpace(m.ExternalPersonnelId))
            .GroupBy(m => m.ExternalPersonnelId!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group
                .Select(m => m.SourceReference?.Trim())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1)
            .Select(group => group.First().Id)
            .ToList();

    private static WorkforceReconciliationItemDto ToReconciliationItem(
        WorkforceReconciliationIssue issue,
        DateTimeOffset now) =>
        Item(
            issue.IssueKey,
            issue.IssueType.ToString(),
            ToDtoSeverity(issue.Severity),
            issue.TitleAr,
            ReconciliationDetail(issue.IssueType),
            issue.EntityType ?? "WorkforceMember",
            issue.EntityId,
            ReconciliationAction(issue.IssueType),
            now);

    private static string ToDtoSeverity(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => "Critical",
        "high" => "High",
        "medium" => "Medium",
        _ => "Low"
    };

    private static string ReconciliationDetail(WorkforceReconciliationIssueType issueType) => issueType switch
    {
        WorkforceReconciliationIssueType.DuplicateExternalId => "يوجد أكثر من سجل لنفس المعرّف الخارجي.",
        WorkforceReconciliationIssueType.ConflictingAssignments => "يوجد تكليفان أساسيان متداخلان لنفس العضو.",
        WorkforceReconciliationIssueType.LeaveWhileRostered => "العضو مجدول رغم حدث توفر مانع.",
        WorkforceReconciliationIssueType.RetirementWhileRostered => "عضو غير نشط ما زال في جدول منشور.",
        WorkforceReconciliationIssueType.StaleSourceRecord => "آخر تحقق للعضو قديم أو مفقود.",
        WorkforceReconciliationIssueType.InvalidUserLink => "UserId يشير إلى مستخدم غير موجود.",
        WorkforceReconciliationIssueType.AssignmentOutsideFacility => "تكليف لا يطابق الموقع التشغيلي الحالي للعضو.",
        WorkforceReconciliationIssueType.UnpublishedRoster => "توجد مسودة جدول مناوبة تحتاج مراجعة ونشرًا.",
        WorkforceReconciliationIssueType.WorkforceSourceConflict => "نفس المعرّف الخارجي مرتبط بمراجع مصدر مختلفة.",
        WorkforceReconciliationIssueType.UnknownAvailability => "لا يمكن احتساب التوفر بثقة.",
        WorkforceReconciliationIssueType.NoCriticalPositionAlternate => "منصب حرج ينقصه بديل مؤهل.",
        WorkforceReconciliationIssueType.ConflictingRosterSlots => "العضو مدرج في أكثر من خانة مناوبة لنفس اليوم.",
        _ => "توجد ملاحظة مصالحة تحتاج مراجعة."
    };

    private static string ReconciliationAction(WorkforceReconciliationIssueType issueType) => issueType switch
    {
        WorkforceReconciliationIssueType.DuplicateExternalId => "مراجعة الدمج أو تصحيح المصدر",
        WorkforceReconciliationIssueType.ConflictingAssignments => "إنهاء أحد التكليفين",
        WorkforceReconciliationIssueType.LeaveWhileRostered => "تعيين بديل أو تعديل التوفر",
        WorkforceReconciliationIssueType.RetirementWhileRostered => "إزالة العضو من المناوبة",
        WorkforceReconciliationIssueType.StaleSourceRecord => "تحديث التحقق من المصدر",
        WorkforceReconciliationIssueType.InvalidUserLink => "إزالة الربط أو تصحيحه",
        WorkforceReconciliationIssueType.AssignmentOutsideFacility => "مراجعة موقع التكليف",
        WorkforceReconciliationIssueType.UnpublishedRoster => "مراجعة ونشر الجدول",
        WorkforceReconciliationIssueType.WorkforceSourceConflict => "توحيد مرجع المصدر",
        WorkforceReconciliationIssueType.UnknownAvailability => "تحديث الحالة من المصدر",
        WorkforceReconciliationIssueType.NoCriticalPositionAlternate => "تعيين بديل مؤهل",
        WorkforceReconciliationIssueType.ConflictingRosterSlots => "إزالة التعيين المكرر",
        _ => "مراجعة الملاحظة"
    };

    private sealed record ReconciliationMember(
        Guid Id,
        string? ExternalPersonnelId,
        string? SourceReference,
        Guid? UserId,
        EmploymentStatus EmploymentStatus,
        DateTimeOffset? LastVerifiedAtUtc,
        Guid? CurrentOperationalFacilityId,
        Guid? HomeFacilityId);

    private sealed record ReconciliationAssignment(
        Guid Id,
        Guid WorkforceMemberId,
        bool IsPrimary,
        DateTimeOffset EffectiveFromUtc,
        DateTimeOffset? EffectiveToUtc,
        Guid FacilityId);

    private sealed record ReconciliationRosterRow(
        Guid Id,
        Guid WorkforceMemberId,
        RosterAssignmentStatus Status);

    private sealed record ReconciliationAvailability(
        Guid WorkforceMemberId,
        AvailabilityType AvailabilityType,
        bool AffectsOperationalAvailability);

    private static WorkforceReconciliationItemDto Item(
        string id,
        string issueType,
        string severity,
        string titleAr,
        string detailAr,
        string entityType,
        Guid? entityId,
        string actionAr,
        DateTimeOffset now) =>
        new()
        {
            Id = id,
            IssueType = issueType,
            Severity = severity,
            TitleAr = titleAr,
            DetailAr = detailAr,
            EntityType = entityType,
            EntityId = entityId,
            SourceSystem = null,
            SuggestedActionAr = actionAr,
            ResponsibleHintAr = "ضابط القوى البشرية / مدير السجن",
            DetectedAtUtc = now
        };

    private static int SeverityRank(string severity) => severity switch
    {
        "Critical" => 4,
        "High" => 3,
        "Medium" => 2,
        _ => 1
    };

    private static string Csv(string? value)
    {
        var v = value ?? string.Empty;
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n'))
        {
            return $"\"{v.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return v;
    }
}
