namespace Baseera.Application.Occupancy;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Domain.Occupancy;
using Microsoft.EntityFrameworkCore;

public interface IOccupancyQueryService
{
    Task<OccupancyWorkspacePayload> GetWorkspacePayloadAsync(Guid facilityId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);
    Task<OccupancySummaryDto> GetSummaryAsync(Guid facilityId, DateTimeOffset asOfUtc, CancellationToken cancellationToken);
    Task<OccupancyUnitBreakdownDto> GetUnitBreakdownAsync(Guid facilityId, DateTimeOffset asOfUtc, CancellationToken cancellationToken);
    Task<MovementSummaryDto> GetMovementSummaryAsync(Guid facilityId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);
}

public interface IOccupancyCommandService
{
    Task<Guid> RecordCapacityAsync(Guid facilityId, OccupancyCapacityRequest request, CancellationToken cancellationToken);
    Task<Guid> RecordSnapshotAsync(Guid facilityId, OccupancySnapshotRequest request, CancellationToken cancellationToken);
    Task<OccupancyImportResult> ImportMovementsAsync(Guid facilityId, InmateMovementImportRequest request, CancellationToken cancellationToken);
}

public sealed class OccupancyPolicyOptions
{
    public decimal AttentionThreshold { get; init; } = 0.85m;
    public decimal HighThreshold { get; init; } = 0.95m;
    public int SnapshotCurrentMinutes { get; init; } = 24 * 60;
    public int SnapshotStaleMinutes { get; init; } = 72 * 60;
    public int RecentMovementLimit { get; init; } = 20;
}

public sealed class OccupancyService(
    IBaseeraDbContext db,
    IOrganizationalScopeService scope,
    ICurrentUser currentUser,
    IAuditService audit,
    TimeProvider timeProvider) : IOccupancyQueryService, IOccupancyCommandService
{
    private readonly OccupancyPolicyOptions options = new();

    public async Task<OccupancyWorkspacePayload> GetWorkspacePayloadAsync(
        Guid facilityId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var summary = await GetSummaryAsync(facilityId, toUtc, cancellationToken);
        var units = currentUser.HasPermission(PermissionCodes.OccupancyViewUnitBreakdown)
            ? await GetUnitBreakdownAsync(facilityId, toUtc, cancellationToken)
            : new OccupancyUnitBreakdownDto([]);
        var movements = currentUser.HasPermission(PermissionCodes.OccupancyViewMovements)
            ? await GetMovementSummaryAsync(facilityId, fromUtc, toUtc, cancellationToken)
            : EmptyMovementSummary();
        return new OccupancyWorkspacePayload
        {
            Summary = summary,
            UnitBreakdown = units,
            MovementSummary = movements,
            Interventions = BuildInterventions(summary, units)
        };
    }

    public async Task<OccupancySummaryDto> GetSummaryAsync(Guid facilityId, DateTimeOffset asOfUtc, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.OccupancyViewSummary);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);

        var capacity = await LatestCapacityQuery(facilityId, null, asOfUtc)
            .Select(c => (int?)c.ApprovedCapacity)
            .FirstOrDefaultAsync(cancellationToken);
        var snapshot = await LatestSnapshotQuery(facilityId, null, asOfUtc)
            .Select(s => new SnapshotProjection(s.InmateCount, s.CapturedAtUtc, s.IsAuthoritative, s.QualityStatus, s.SourceType, s.SourceReference))
            .FirstOrDefaultAsync(cancellationToken);
        var units = currentUser.HasPermission(PermissionCodes.OccupancyViewUnitBreakdown)
            ? await GetUnitBreakdownAsync(facilityId, asOfUtc, cancellationToken)
            : new OccupancyUnitBreakdownDto([]);

        var status = OccupancyClassifier.Classify(capacity, snapshot?.InmateCount, options);
        var warnings = BuildSummaryWarnings(capacity, snapshot, units);
        var rate = OccupancyClassifier.Rate(capacity, snapshot?.InmateCount);
        int? available = capacity.HasValue && snapshot is not null ? capacity.Value - snapshot.InmateCount : null;
        return new OccupancySummaryDto
        {
            FacilityId = facilityId,
            ApprovedCapacity = capacity,
            CurrentCount = snapshot?.InmateCount,
            OccupancyRate = rate,
            AvailablePlaces = available.HasValue && available.Value > 0 ? available.Value : 0,
            OverCapacityCount = available.HasValue && available.Value < 0 ? Math.Abs(available.Value) : 0,
            StatusCode = status.Code,
            StatusAr = status.LabelAr,
            UnitCount = units.Units.Count,
            OverloadedUnits = units.Units.Count(u => u.StatusCode == "over-capacity"),
            EmptyUnits = units.Units.Count(u => u.CurrentCount == 0 && u.ApprovedCapacity.HasValue),
            LatestSnapshotAtUtc = snapshot?.CapturedAtUtc,
            SourceCode = snapshot is null ? "unknown" : snapshot.IsAuthoritative ? "authoritative-snapshot" : "internal-snapshot",
            SourceAr = snapshot is null ? "غير معروف" : snapshot.IsAuthoritative ? "Snapshot رسمي" : "Snapshot داخلي",
            FreshnessStatus = Freshness(snapshot?.CapturedAtUtc, asOfUtc),
            ConfidenceLevel = Confidence(capacity, snapshot, warnings),
            IsPartial = warnings.Count > 0,
            Warnings = warnings
        };
    }

    public async Task<OccupancyUnitBreakdownDto> GetUnitBreakdownAsync(Guid facilityId, DateTimeOffset asOfUtc, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.OccupancyViewUnitBreakdown);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);

        var units = await db.FacilityUnits.AsNoTracking()
            .Where(unit => unit.FacilityId == facilityId && unit.IsActive)
            .OrderBy(unit => unit.Code)
            .Select(unit => new { unit.Id, unit.Code, unit.NameAr })
            .ToListAsync(cancellationToken);
        var unitIds = units.Select(u => u.Id).ToArray();

        var capacityRows = await db.FacilityCapacityBaselines.AsNoTracking()
            .Where(c => c.FacilityId == facilityId
                && c.FacilityUnitId != null
                && unitIds.Contains(c.FacilityUnitId.Value)
                && c.CapacityType == CapacityType.ApprovedOperational
                && c.EffectiveFromUtc <= asOfUtc
                && (c.EffectiveToUtc == null || c.EffectiveToUtc > asOfUtc))
            .GroupBy(c => c.FacilityUnitId!.Value)
            .Select(g => g.OrderByDescending(c => c.EffectiveFromUtc).Select(c => new { c.FacilityUnitId, c.ApprovedCapacity }).First())
            .ToListAsync(cancellationToken);
        var capacityByUnit = capacityRows.ToDictionary(c => c.FacilityUnitId!.Value, c => c.ApprovedCapacity);

        var snapshotRows = await db.InmateCensusSnapshots.AsNoTracking()
            .Where(s => s.FacilityId == facilityId
                && s.FacilityUnitId != null
                && unitIds.Contains(s.FacilityUnitId.Value)
                && s.CapturedAtUtc <= asOfUtc)
            .GroupBy(s => s.FacilityUnitId!.Value)
            .Select(g => g.OrderByDescending(s => s.IsAuthoritative).ThenByDescending(s => s.CapturedAtUtc).Select(s => new { s.FacilityUnitId, s.InmateCount, s.CapturedAtUtc, s.SourceType }).First())
            .ToListAsync(cancellationToken);
        var snapshotsByUnit = snapshotRows.ToDictionary(s => s.FacilityUnitId!.Value);

        var openNotes = await db.OperationalNotes.AsNoTracking()
            .Where(note => note.FacilityId == facilityId
                && note.FacilityUnitId != null
                && note.Status != NoteStatus.Closed
                && unitIds.Contains(note.FacilityUnitId.Value))
            .GroupBy(note => note.FacilityUnitId!.Value)
            .Select(g => new { UnitId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var notesByUnit = openNotes.ToDictionary(n => n.UnitId, n => n.Count);

        var rows = units.Select(unit =>
        {
            capacityByUnit.TryGetValue(unit.Id, out var capacity);
            snapshotsByUnit.TryGetValue(unit.Id, out var snapshot);
            var current = snapshot?.InmateCount;
            var status = OccupancyClassifier.Classify(capacity == 0 ? null : capacity, current, options);
            return new OccupancyUnitDto
            {
                UnitId = unit.Id,
                UnitNameAr = unit.NameAr,
                UnitCode = unit.Code,
                ApprovedCapacity = capacity == 0 ? null : capacity,
                CurrentCount = current,
                OccupancyRate = OccupancyClassifier.Rate(capacity == 0 ? null : capacity, current),
                AvailablePlaces = capacity == 0 || current is null ? null : Math.Max(0, capacity - current.Value),
                OverloadCount = capacity == 0 || current is null ? null : Math.Max(0, current.Value - capacity),
                StatusCode = status.Code,
                StatusAr = status.LabelAr,
                LastUpdatedAtUtc = snapshot?.CapturedAtUtc,
                DataSourceAr = snapshot is null ? "لا يوجد Snapshot" : "Snapshot إشغال",
                OpenNotesCount = notesByUnit.GetValueOrDefault(unit.Id),
                OpenIncidentsCount = 0,
                RiskCount = 0,
                AlertReasons = UnitAlerts(capacity == 0 ? null : capacity, current, snapshot?.CapturedAtUtc, asOfUtc)
            };
        })
            .OrderByDescending(unit => unit.OverloadCount ?? 0)
            .ThenByDescending(unit => unit.OccupancyRate ?? 0)
            .ThenBy(unit => unit.UnitCode, StringComparer.Ordinal)
            .ToList();

        return new OccupancyUnitBreakdownDto(rows);
    }

    public async Task<MovementSummaryDto> GetMovementSummaryAsync(Guid facilityId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.OccupancyViewMovements);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        if (fromUtc > toUtc)
        {
            throw new ArgumentException("نطاق التاريخ غير صالح.");
        }

        var events = await db.InmateMovementEvents.AsNoTracking()
            .Where(e => e.FacilityId == facilityId
                && !e.IsReversed
                && e.OccurredAtUtc >= fromUtc
                && e.OccurredAtUtc <= toUtc)
            .GroupBy(e => e.MovementType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var counts = events.ToDictionary(e => e.Type, e => e.Count);

        var daily = await db.InmateMovementEvents.AsNoTracking()
            .Where(e => e.FacilityId == facilityId
                && !e.IsReversed
                && e.OccurredAtUtc >= fromUtc
                && e.OccurredAtUtc <= toUtc)
            .GroupBy(e => new { e.OccurredAtUtc.Year, e.OccurredAtUtc.Month, e.OccurredAtUtc.Day })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                Admissions = g.Count(e => e.MovementType == MovementType.Admission || e.MovementType == MovementType.TransferIn || e.MovementType == MovementType.ReturnFromLeave),
                Releases = g.Count(e => e.MovementType == MovementType.Release || e.MovementType == MovementType.TransferOut || e.MovementType == MovementType.Death || e.MovementType == MovementType.TemporaryLeave),
                TransfersIn = g.Count(e => e.MovementType == MovementType.TransferIn),
                TransfersOut = g.Count(e => e.MovementType == MovementType.TransferOut)
            })
            .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
            .Take(60)
            .ToListAsync(cancellationToken);

        var admissions = Count(counts, MovementType.Admission) + Count(counts, MovementType.TransferIn) + Count(counts, MovementType.ReturnFromLeave);
        var releases = Count(counts, MovementType.Release) + Count(counts, MovementType.TransferOut) + Count(counts, MovementType.TemporaryLeave) + Count(counts, MovementType.Death);
        return new MovementSummaryDto
        {
            Admissions = Count(counts, MovementType.Admission),
            Releases = Count(counts, MovementType.Release),
            TransferIn = Count(counts, MovementType.TransferIn),
            TransferOut = Count(counts, MovementType.TransferOut),
            InternalTransfers = Count(counts, MovementType.InternalTransfer),
            TemporaryLeave = Count(counts, MovementType.TemporaryLeave),
            Returns = Count(counts, MovementType.ReturnFromLeave),
            NetMovement = admissions - releases,
            DailyTrend = daily.Select(d => new MovementTrendPointDto(
                new DateOnly(d.Year, d.Month, d.Day),
                d.Admissions,
                d.Releases,
                d.TransfersIn,
                d.TransfersOut,
                d.Admissions - d.Releases)).ToList(),
            RejectedMovements = 0
        };
    }

    public async Task<Guid> RecordCapacityAsync(Guid facilityId, OccupancyCapacityRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.OccupancyManageCapacity);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        ValidateCapacity(request);
        await EnsureUnitBelongsAsync(facilityId, request.FacilityUnitId, cancellationToken);

        var overlap = await db.FacilityCapacityBaselines.AnyAsync(c =>
            c.FacilityId == facilityId
            && c.FacilityUnitId == request.FacilityUnitId
            && c.CapacityType == request.CapacityType
            && c.EffectiveFromUtc < (request.EffectiveToUtc ?? DateTimeOffset.MaxValue)
            && (c.EffectiveToUtc ?? DateTimeOffset.MaxValue) > request.EffectiveFromUtc,
            cancellationToken);
        if (overlap)
        {
            throw new InvalidOperationException("يوجد سجل طاقة متداخل لنفس النطاق والنوع.");
        }

        var entity = new FacilityCapacityBaseline
        {
            OrganizationId = facility.OrganizationId,
            FacilityId = facilityId,
            FacilityUnitId = request.FacilityUnitId,
            CapacityType = request.CapacityType,
            ApprovedCapacity = request.ApprovedCapacity,
            EffectiveFromUtc = request.EffectiveFromUtc,
            EffectiveToUtc = request.EffectiveToUtc,
            ApprovalReference = request.ApprovalReference,
            ApprovalDateUtc = request.ApprovalDateUtc,
            SourceType = request.SourceType,
            SourceReference = request.SourceReference.Trim(),
            Notes = request.Notes,
            CreatedBy = currentUser.ExternalSubject
        };
        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("Occupancy.CapacityRecorded", "FacilityCapacityBaseline", entity.Id, cancellationToken);
        return entity.Id;
    }

    public async Task<Guid> RecordSnapshotAsync(Guid facilityId, OccupancySnapshotRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.OccupancyRecordSnapshot);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        ValidateSnapshot(request);
        await EnsureUnitBelongsAsync(facilityId, request.FacilityUnitId, cancellationToken);

        var entity = new InmateCensusSnapshot
        {
            OrganizationId = facility.OrganizationId,
            FacilityId = facilityId,
            FacilityUnitId = request.FacilityUnitId,
            CapturedAtUtc = request.CapturedAtUtc,
            InmateCount = request.InmateCount,
            MaleCount = request.MaleCount,
            FemaleCount = request.FemaleCount,
            AdultCount = request.AdultCount,
            JuvenileCount = request.JuvenileCount,
            MedicalCount = request.MedicalCount,
            IsolationCount = request.IsolationCount,
            SourceType = request.SourceType,
            SourceReference = request.SourceReference.Trim(),
            SourceVersion = request.SourceVersion,
            ImportedAtUtc = request.SourceType == OccupancySourceType.Import ? timeProvider.GetUtcNow() : null,
            IsAuthoritative = request.IsAuthoritative,
            QualityStatus = request.QualityStatus,
            QualityNotes = request.QualityNotes,
            CreatedBy = currentUser.ExternalSubject
        };
        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("Occupancy.SnapshotRecorded", "InmateCensusSnapshot", entity.Id, cancellationToken);
        return entity.Id;
    }

    public async Task<OccupancyImportResult> ImportMovementsAsync(Guid facilityId, InmateMovementImportRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.OccupancyImport);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        if (request.Rows.Count > 100)
        {
            throw new ArgumentException("حد الاستيراد في الطلب الواحد هو 100 حركة.");
        }

        var accepted = 0;
        var duplicates = 0;
        var rejected = new List<string>();
        foreach (var row in request.Rows)
        {
            var validation = ValidateMovementRow(facilityId, row);
            if (validation is not null)
            {
                rejected.Add($"{row.ExternalEventId}: {validation}");
                continue;
            }

            var duplicate = await db.InmateMovementEvents.AnyAsync(e =>
                e.SourceType == OccupancySourceType.Import
                && e.SourceReference == request.SourceSystem
                && e.ExternalEventId == row.ExternalEventId,
                cancellationToken);
            if (duplicate)
            {
                duplicates++;
                continue;
            }

            db.Add(new InmateMovementEvent
            {
                OrganizationId = facility.OrganizationId,
                FacilityId = facilityId,
                InmateReferenceHash = row.InmateReferenceHash.Trim(),
                MovementType = row.MovementType,
                FromFacilityId = row.FromFacilityId,
                ToFacilityId = row.ToFacilityId,
                FromFacilityUnitId = row.FromFacilityUnitId,
                ToFacilityUnitId = row.ToFacilityUnitId,
                OccurredAtUtc = row.OccurredAtUtc,
                RecordedAtUtc = timeProvider.GetUtcNow(),
                SourceType = OccupancySourceType.Import,
                SourceReference = request.SourceSystem.Trim(),
                ExternalEventId = row.ExternalEventId.Trim(),
                ReasonCode = row.ReasonCode,
                CreatedBy = currentUser.ExternalSubject
            });
            accepted++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("Occupancy.MovementImported", "InmateMovementEvent", facilityId, cancellationToken);
        return new OccupancyImportResult(accepted, duplicates, rejected);
    }

    private IQueryable<FacilityCapacityBaseline> LatestCapacityQuery(Guid facilityId, Guid? unitId, DateTimeOffset asOfUtc) =>
        db.FacilityCapacityBaselines.AsNoTracking()
            .Where(c => c.FacilityId == facilityId
                && c.FacilityUnitId == unitId
                && c.CapacityType == CapacityType.ApprovedOperational
                && c.EffectiveFromUtc <= asOfUtc
                && (c.EffectiveToUtc == null || c.EffectiveToUtc > asOfUtc))
            .OrderByDescending(c => c.EffectiveFromUtc);

    private IQueryable<InmateCensusSnapshot> LatestSnapshotQuery(Guid facilityId, Guid? unitId, DateTimeOffset asOfUtc) =>
        db.InmateCensusSnapshots.AsNoTracking()
            .Where(s => s.FacilityId == facilityId && s.FacilityUnitId == unitId && s.CapturedAtUtc <= asOfUtc)
            .OrderByDescending(s => s.IsAuthoritative)
            .ThenByDescending(s => s.CapturedAtUtc);

    private async Task<FacilityScopeProjection> EnsureFacilityVisibleAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        if (!scope.CanAccessFacility(facilityId))
        {
            throw new KeyNotFoundException("السجن غير موجود.");
        }

        var facility = await db.Facilities.AsNoTracking()
            .Where(f => f.Id == facilityId && f.IsActive)
            .Select(f => new FacilityScopeProjection(f.Id, f.Region.OrganizationId))
            .SingleOrDefaultAsync(cancellationToken);

        return facility ?? throw new KeyNotFoundException("السجن غير موجود.");
    }

    private async Task EnsureUnitBelongsAsync(Guid facilityId, Guid? unitId, CancellationToken cancellationToken)
    {
        if (!unitId.HasValue)
        {
            return;
        }

        var exists = await db.FacilityUnits.AnyAsync(u => u.Id == unitId.Value && u.FacilityId == facilityId, cancellationToken);
        if (!exists || !scope.CanAccessFacilityUnit(unitId.Value))
        {
            throw new KeyNotFoundException("الوحدة غير موجودة.");
        }
    }

    private void Require(string permission)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw new UnauthorizedAccessException("لا تملك صلاحية تنفيذ هذه العملية.");
        }
    }

    private static void ValidateCapacity(OccupancyCapacityRequest request)
    {
        if (request.ApprovedCapacity <= 0) throw new ArgumentException("الطاقة المعتمدة يجب أن تكون أكبر من صفر.");
        if (request.EffectiveToUtc.HasValue && request.EffectiveToUtc <= request.EffectiveFromUtc) throw new ArgumentException("نهاية السريان يجب أن تكون بعد بدايته.");
        if (string.IsNullOrWhiteSpace(request.SourceReference)) throw new ArgumentException("مرجع المصدر مطلوب.");
    }

    private static void ValidateSnapshot(OccupancySnapshotRequest request)
    {
        if (request.InmateCount < 0) throw new ArgumentException("عدد النزلاء لا يمكن أن يكون سالبًا.");
        if (string.IsNullOrWhiteSpace(request.SourceReference)) throw new ArgumentException("مرجع المصدر مطلوب.");
    }

    private static string? ValidateMovementRow(Guid facilityId, InmateMovementImportRow row)
    {
        if (string.IsNullOrWhiteSpace(row.InmateReferenceHash)) return "مرجع النزيل المموه مطلوب.";
        if (string.IsNullOrWhiteSpace(row.ExternalEventId)) return "معرف الحدث الخارجي مطلوب.";
        if (row.MovementType == MovementType.Admission && row.ToFacilityId is null) return "الدخول يتطلب وجهة.";
        if (row.MovementType == MovementType.Release && row.FromFacilityId is null) return "الإفراج يتطلب مصدرًا.";
        if (row.MovementType == MovementType.InternalTransfer && (row.FromFacilityUnitId is null || row.ToFacilityUnitId is null)) return "النقل الداخلي يتطلب وحدتين.";
        if (row.FromFacilityId == row.ToFacilityId && row.FromFacilityUnitId == row.ToFacilityUnitId) return "لا يمكن تسجيل نقل إلى نفس النطاق.";
        if (row.FromFacilityId != facilityId && row.ToFacilityId != facilityId) return "الحركة لا تخص السجن المطلوب.";
        return null;
    }

    private IReadOnlyList<string> BuildSummaryWarnings(int? capacity, SnapshotProjection? snapshot, OccupancyUnitBreakdownDto units)
    {
        var warnings = new List<string>();
        if (!capacity.HasValue) warnings.Add("لا توجد طاقة تشغيلية معتمدة للسجن.");
        if (snapshot is null) warnings.Add("لا يوجد Snapshot إشغال موثوق للسجن.");
        if (snapshot is not null && Freshness(snapshot.CapturedAtUtc, timeProvider.GetUtcNow()) == "stale") warnings.Add("آخر Snapshot إشغال قديم.");
        if (units.Units.Any(u => u.StatusCode == "unknown")) warnings.Add("بعض الوحدات لا تحتوي طاقة أو عددًا موثوقًا.");
        return warnings;
    }

    private static IReadOnlyList<string> UnitAlerts(int? capacity, int? current, DateTimeOffset? capturedAtUtc, DateTimeOffset asOfUtc)
    {
        var alerts = new List<string>();
        if (!capacity.HasValue) alerts.Add("CapacityMissing");
        if (!current.HasValue) alerts.Add("CensusMissing");
        if (capacity.HasValue && current.HasValue && current.Value > capacity.Value) alerts.Add("OverCapacity");
        if (capacity.HasValue && current.HasValue && current.Value <= capacity.Value && OccupancyClassifier.Rate(capacity, current) >= 0.85m) alerts.Add("NearCapacity");
        if (capturedAtUtc.HasValue && asOfUtc - capturedAtUtc.Value > TimeSpan.FromDays(3)) alerts.Add("SnapshotStale");
        return alerts;
    }

    private static string Freshness(DateTimeOffset? capturedAtUtc, DateTimeOffset asOfUtc)
    {
        if (!capturedAtUtc.HasValue) return "unknown";
        var age = asOfUtc - capturedAtUtc.Value;
        if (age <= TimeSpan.FromDays(1)) return "current";
        if (age <= TimeSpan.FromDays(3)) return "delayed";
        return "stale";
    }

    private static string Confidence(int? capacity, SnapshotProjection? snapshot, IReadOnlyList<string> warnings)
    {
        if (!capacity.HasValue || snapshot is null) return "unknown";
        if (snapshot.QualityStatus == CensusQualityStatus.Conflicting) return "low";
        return warnings.Count == 0 ? "high" : "medium";
    }

    private static IReadOnlyList<OccupancyInterventionDto> BuildInterventions(OccupancySummaryDto summary, OccupancyUnitBreakdownDto units)
    {
        var items = new List<OccupancyInterventionDto>();
        if (summary.StatusCode == "over-capacity")
        {
            items.Add(new OccupancyInterventionDto
            {
                Type = "FacilityOverCapacity",
                Reference = $"OCC-{summary.FacilityId:N}",
                TitleAr = "السجن متجاوز للطاقة",
                SeverityAr = "حرجة",
                PriorityRank = 970,
                ReasonAr = $"تجاوز بمقدار {summary.OverCapacityCount ?? 0} نزيل.",
                ActionLabelAr = "فتح الإشغال"
            });
        }

        foreach (var unit in units.Units.Where(u => u.StatusCode is "over-capacity" or "unknown").Take(10))
        {
            items.Add(new OccupancyInterventionDto
            {
                Type = unit.StatusCode == "over-capacity" ? "UnitOverCapacity" : "CapacityMissing",
                Reference = unit.UnitCode,
                TitleAr = unit.UnitNameAr,
                SeverityAr = unit.StatusCode == "over-capacity" ? "عالية" : "متوسطة",
                PriorityRank = unit.StatusCode == "over-capacity" ? 940 : 760,
                ReasonAr = unit.StatusCode == "over-capacity" ? $"تجاوز بمقدار {unit.OverloadCount ?? 0}." : "بيانات الطاقة أو العدد غير مكتملة.",
                UnitId = unit.UnitId,
                ActionLabelAr = "فتح الوحدة"
            });
        }

        return items.OrderByDescending(i => i.PriorityRank).ToList();
    }

    private static int Count(IReadOnlyDictionary<MovementType, int> counts, MovementType type) =>
        counts.TryGetValue(type, out var count) ? count : 0;

    private static MovementSummaryDto EmptyMovementSummary() =>
        new()
        {
            Admissions = 0,
            Releases = 0,
            TransferIn = 0,
            TransferOut = 0,
            InternalTransfers = 0,
            TemporaryLeave = 0,
            Returns = 0,
            NetMovement = 0,
            DailyTrend = [],
            RejectedMovements = 0
        };

    private Task AuditAsync(string action, string entityType, object entityId, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditEntry
        {
            Action = action,
            Module = "Occupancy",
            EntityType = entityType,
            EntityId = entityId.ToString(),
            NewValues = null,
            Reason = "Occupancy domain operation",
            IsSensitiveView = false
        }, cancellationToken);

    private sealed record FacilityScopeProjection(Guid Id, Guid OrganizationId);
    private sealed record SnapshotProjection(int InmateCount, DateTimeOffset CapturedAtUtc, bool IsAuthoritative, CensusQualityStatus QualityStatus, OccupancySourceType SourceType, string SourceReference);
}

internal static class OccupancyClassifier
{
    public static (string Code, string LabelAr) Classify(int? capacity, int? current, OccupancyPolicyOptions options)
    {
        if (!capacity.HasValue || !current.HasValue || capacity.Value <= 0)
        {
            return ("unknown", "غير معروف");
        }

        var rate = Rate(capacity, current)!.Value;
        if (rate > 1m) return ("over-capacity", "متجاوز للطاقة");
        if (rate >= options.HighThreshold) return ("high", "مرتفع");
        if (rate >= options.AttentionThreshold) return ("attention", "يحتاج متابعة");
        return ("normal", "طبيعي");
    }

    public static decimal? Rate(int? capacity, int? current)
    {
        if (!capacity.HasValue || !current.HasValue || capacity.Value <= 0)
        {
            return null;
        }

        return Math.Round(current.Value / (decimal)capacity.Value, 4);
    }
}
