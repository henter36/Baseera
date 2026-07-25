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
            .Select(s => new SnapshotProjection(s.InmateCount, s.CapturedAtUtc, s.IsAuthoritative, s.QualityStatus))
            .FirstOrDefaultAsync(cancellationToken);
        var units = currentUser.HasPermission(PermissionCodes.OccupancyViewUnitBreakdown)
            ? await GetUnitBreakdownAsync(facilityId, asOfUtc, cancellationToken)
            : new OccupancyUnitBreakdownDto([]);

        var status = OccupancyClassifier.Classify(capacity, snapshot?.InmateCount, options);
        var source = ResolveSource(snapshot);
        var warnings = BuildSummaryWarnings(capacity, snapshot, units, asOfUtc);
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
            OverloadedUnits = units.Units.Count(u => u.StatusCode == OccupancyStatusCodes.OverCapacity),
            EmptyUnits = units.Units.Count(u => u.CurrentCount == 0 && u.ApprovedCapacity.HasValue),
            LatestSnapshotAtUtc = snapshot?.CapturedAtUtc,
            SourceCode = source.Code,
            SourceAr = source.LabelAr,
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

        var units = await LoadActiveUnitsAsync(facilityId, cancellationToken);
        var unitIds = units.Select(u => u.Id).ToArray();

        var capacityByUnit = await LoadCapacityByUnitAsync(facilityId, unitIds, asOfUtc, cancellationToken);
        var snapshotsByUnit = await LoadLatestSnapshotsByUnitAsync(facilityId, unitIds, asOfUtc, cancellationToken);
        var notesByUnit = await LoadOpenNotesByUnitAsync(facilityId, unitIds, cancellationToken);

        var rows = units
            .Select(unit => BuildUnitOccupancyDto(unit, capacityByUnit, snapshotsByUnit, notesByUnit, asOfUtc))
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

        var dailyByType = await db.InmateMovementEvents.AsNoTracking()
            .Where(e => e.FacilityId == facilityId
                && !e.IsReversed
                && e.OccurredAtUtc >= fromUtc
                && e.OccurredAtUtc <= toUtc)
            .GroupBy(e => new { e.OccurredAtUtc.Year, e.OccurredAtUtc.Month, e.OccurredAtUtc.Day, e.MovementType })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                g.Key.MovementType,
                Count = g.Count()
            })
            .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
            .Take(60)
            .ToListAsync(cancellationToken);

        var admissions = CountByFlow(counts, MovementFlow.Inflow);
        var releases = CountByFlow(counts, MovementFlow.Outflow);
        return new MovementSummaryDto
        {
            Admissions = admissions,
            Releases = releases,
            TransferIn = Count(counts, MovementType.TransferIn),
            TransferOut = Count(counts, MovementType.TransferOut),
            InternalTransfers = Count(counts, MovementType.InternalTransfer),
            TemporaryLeave = Count(counts, MovementType.TemporaryLeave),
            Returns = Count(counts, MovementType.ReturnFromLeave),
            Death = Count(counts, MovementType.Death),
            HospitalTransfers = Count(counts, MovementType.HospitalTransfer),
            CourtTransfers = Count(counts, MovementType.CourtTransfer),
            Corrections = Count(counts, MovementType.Correction),
            OtherMovements = Count(counts, MovementType.Other),
            NetMovement = admissions - releases,
            DailyTrend = BuildDailyTrend(dailyByType.Select(row => new DailyMovementProjection(row.Year, row.Month, row.Day, row.MovementType, row.Count)).ToList())
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
        var sourceSystem = NormalizeRequired(request.SourceSystem, "نظام المصدر مطلوب.");
        if (request.Rows.Count > 100)
        {
            throw new ArgumentException("حد الاستيراد في الطلب الواحد هو 100 حركة.");
        }

        var duplicates = 0;
        var rejected = new List<string>();
        var seenInRequest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<MovementImportCandidate>();
        foreach (var row in request.Rows)
        {
            var validation = ValidateMovementRow(facilityId, row);
            if (validation is not null)
            {
                rejected.Add($"{row.ExternalEventId}: {validation}");
                continue;
            }

            await EnsureInternalTransferUnitsBelongAsync(facilityId, row, cancellationToken);
            var externalEventId = NormalizeRequired(row.ExternalEventId, "معرف الحدث الخارجي مطلوب.");
            var idempotencyKey = $"{sourceSystem}\u001f{externalEventId}";
            if (!seenInRequest.Add(idempotencyKey))
            {
                duplicates++;
                continue;
            }

            candidates.Add(new MovementImportCandidate(row, externalEventId));
        }

        if (candidates.Count == 0)
        {
            return new OccupancyImportResult(0, duplicates, rejected);
        }

        var existingIds = await LoadExistingMovementEventIdsAsync(sourceSystem, candidates.Select(candidate => candidate.ExternalEventId).ToArray(), cancellationToken);
        var newCandidates = candidates.Where(candidate => !existingIds.Contains(candidate.ExternalEventId)).ToList();
        duplicates += candidates.Count - newCandidates.Count;

        var accepted = await SaveMovementCandidatesAsync(facility, sourceSystem, newCandidates, cancellationToken);
        duplicates += newCandidates.Count - accepted;
        return new OccupancyImportResult(accepted, duplicates, rejected);
    }

    private async Task<int> SaveMovementCandidatesAsync(
        FacilityScopeProjection facility,
        string sourceSystem,
        List<MovementImportCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var remaining = candidates;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (remaining.Count == 0)
            {
                return 0;
            }

            foreach (var candidate in remaining)
            {
                db.Add(CreateMovementEvent(facility, sourceSystem, candidate));
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await AuditAsync("Occupancy.MovementImported", "InmateMovementEvent", facility.Id, cancellationToken);
                return remaining.Count;
            }
            catch (DbUpdateException ex) when (SqlServerOccupancyUniqueConstraintDetector.IsMovementImportDuplicate(ex) && attempt == 0)
            {
                db.ClearChanges();
                var existingIds = await LoadExistingMovementEventIdsAsync(sourceSystem, remaining.Select(candidate => candidate.ExternalEventId).ToArray(), cancellationToken);
                remaining = remaining.Where(candidate => !existingIds.Contains(candidate.ExternalEventId)).ToList();
            }
        }

        return 0;
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

    private Task<List<UnitProjection>> LoadActiveUnitsAsync(Guid facilityId, CancellationToken cancellationToken) =>
        db.FacilityUnits.AsNoTracking()
            .Where(unit => unit.FacilityId == facilityId && unit.IsActive)
            .OrderBy(unit => unit.Code)
            .Select(unit => new UnitProjection(unit.Id, unit.Code, unit.NameAr))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, int>> LoadCapacityByUnitAsync(
        Guid facilityId,
        IReadOnlyCollection<Guid> unitIds,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        var rows = await db.FacilityCapacityBaselines.AsNoTracking()
            .Where(c => c.FacilityId == facilityId
                && c.FacilityUnitId.HasValue
                && unitIds.Contains(c.FacilityUnitId.Value)
                && c.CapacityType == CapacityType.ApprovedOperational
                && c.EffectiveFromUtc <= asOfUtc
                && (c.EffectiveToUtc == null || c.EffectiveToUtc > asOfUtc))
            .Select(c => new UnitCapacityProjection(c.FacilityUnitId, c.ApprovedCapacity, c.EffectiveFromUtc))
            .GroupBy(c => c.UnitId)
            .Select(g => g.OrderByDescending(c => c.EffectiveFromUtc).First())
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, int>();
        foreach (var row in rows)
        {
            if (row.UnitId.HasValue)
            {
                result[row.UnitId.Value] = row.ApprovedCapacity;
            }
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, UnitSnapshotProjection>> LoadLatestSnapshotsByUnitAsync(
        Guid facilityId,
        IReadOnlyCollection<Guid> unitIds,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        var rows = await db.InmateCensusSnapshots.AsNoTracking()
            .Where(s => s.FacilityId == facilityId
                && s.FacilityUnitId.HasValue
                && unitIds.Contains(s.FacilityUnitId.Value)
                && s.CapturedAtUtc <= asOfUtc)
            .Select(s => new UnitSnapshotProjection(s.FacilityUnitId, s.InmateCount, s.CapturedAtUtc, s.IsAuthoritative))
            .GroupBy(s => s.UnitId)
            .Select(g => g.OrderByDescending(s => s.IsAuthoritative).ThenByDescending(s => s.CapturedAtUtc).First())
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, UnitSnapshotProjection>();
        foreach (var row in rows)
        {
            if (row.UnitId.HasValue)
            {
                result[row.UnitId.Value] = row;
            }
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, int>> LoadOpenNotesByUnitAsync(
        Guid facilityId,
        IReadOnlyCollection<Guid> unitIds,
        CancellationToken cancellationToken)
    {
        var rows = await db.OperationalNotes.AsNoTracking()
            .Where(note => note.FacilityId == facilityId
                && note.FacilityUnitId.HasValue
                && note.Status != NoteStatus.Closed
                && unitIds.Contains(note.FacilityUnitId.Value))
            .Select(note => note.FacilityUnitId)
            .GroupBy(unitId => unitId)
            .Select(g => new UnitOpenNotesProjection(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, int>();
        foreach (var row in rows)
        {
            if (row.UnitId.HasValue)
            {
                result[row.UnitId.Value] = row.Count;
            }
        }

        return result;
    }

    private OccupancyUnitDto BuildUnitOccupancyDto(
        UnitProjection unit,
        IReadOnlyDictionary<Guid, int> capacityByUnit,
        IReadOnlyDictionary<Guid, UnitSnapshotProjection> snapshotsByUnit,
        IReadOnlyDictionary<Guid, int> notesByUnit,
        DateTimeOffset asOfUtc)
    {
        var hasCapacity = capacityByUnit.TryGetValue(unit.Id, out var rawCapacity);
        var capacity = hasCapacity && rawCapacity > 0 ? rawCapacity : (int?)null;
        snapshotsByUnit.TryGetValue(unit.Id, out var snapshot);
        var current = snapshot?.InmateCount;
        var status = OccupancyClassifier.Classify(capacity, current, options);

        return new OccupancyUnitDto
        {
            UnitId = unit.Id,
            UnitNameAr = unit.NameAr,
            UnitCode = unit.Code,
            ApprovedCapacity = capacity,
            CurrentCount = current,
            OccupancyRate = OccupancyClassifier.Rate(capacity, current),
            AvailablePlaces = capacity is null || current is null ? null : Math.Max(0, capacity.Value - current.Value),
            OverloadCount = capacity is null || current is null ? null : Math.Max(0, current.Value - capacity.Value),
            StatusCode = status.Code,
            StatusAr = status.LabelAr,
            LastUpdatedAtUtc = snapshot?.CapturedAtUtc,
            DataSourceAr = snapshot is null ? "لا يوجد Snapshot" : "Snapshot إشغال",
            OpenNotesCount = notesByUnit.GetValueOrDefault(unit.Id),
            OpenIncidentsCount = 0,
            RiskCount = 0,
            AlertReasons = UnitAlerts(capacity, current, snapshot?.CapturedAtUtc, asOfUtc)
        };
    }

    private async Task<HashSet<string>> LoadExistingMovementEventIdsAsync(
        string sourceSystem,
        IReadOnlyCollection<string> externalEventIds,
        CancellationToken cancellationToken)
    {
        if (externalEventIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await db.InmateMovementEvents.AsNoTracking()
            .Where(e => e.SourceType == OccupancySourceType.Import
                && e.SourceReference == sourceSystem
                && e.ExternalEventId != null
                && externalEventIds.Contains(e.ExternalEventId))
            .Select(e => e.ExternalEventId)
            .ToListAsync(cancellationToken);

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in rows)
        {
            if (id is not null)
            {
                result.Add(id);
            }
        }

        return result;
    }

    private InmateMovementEvent CreateMovementEvent(
        FacilityScopeProjection facility,
        string sourceSystem,
        MovementImportCandidate candidate)
    {
        var row = candidate.Row;
        return new InmateMovementEvent
        {
            OrganizationId = facility.OrganizationId,
            FacilityId = facility.Id,
            InmateReferenceHash = NormalizeRequired(row.InmateReferenceHash, "مرجع النزيل المموه مطلوب."),
            MovementType = row.MovementType,
            FromFacilityId = row.FromFacilityId,
            ToFacilityId = row.ToFacilityId,
            FromFacilityUnitId = row.FromFacilityUnitId,
            ToFacilityUnitId = row.ToFacilityUnitId,
            OccurredAtUtc = row.OccurredAtUtc,
            RecordedAtUtc = timeProvider.GetUtcNow(),
            SourceType = OccupancySourceType.Import,
            SourceReference = sourceSystem,
            ExternalEventId = candidate.ExternalEventId,
            ReasonCode = row.ReasonCode,
            CreatedBy = currentUser.ExternalSubject
        };
    }

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

    private async Task EnsureInternalTransferUnitsBelongAsync(Guid facilityId, InmateMovementImportRow row, CancellationToken cancellationToken)
    {
        if (row.MovementType != MovementType.InternalTransfer)
        {
            return;
        }

        await EnsureUnitBelongsAsync(facilityId, row.FromFacilityUnitId, cancellationToken);
        await EnsureUnitBelongsAsync(facilityId, row.ToFacilityUnitId, cancellationToken);
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
        if (request.EffectiveFromUtc == default) throw new ArgumentException("تاريخ بداية سريان الطاقة مطلوب.");
        if (request.EffectiveToUtc.HasValue && request.EffectiveToUtc <= request.EffectiveFromUtc) throw new ArgumentException("نهاية السريان يجب أن تكون بعد بدايته.");
        if (string.IsNullOrWhiteSpace(request.SourceReference)) throw new ArgumentException("مرجع المصدر مطلوب.");
    }

    private static void ValidateSnapshot(OccupancySnapshotRequest request)
    {
        if (request.CapturedAtUtc == default) throw new ArgumentException("وقت التقاط Snapshot الإشغال مطلوب.");
        if (request.InmateCount < 0) throw new ArgumentException("عدد النزلاء لا يمكن أن يكون سالبًا.");
        if (string.IsNullOrWhiteSpace(request.SourceReference)) throw new ArgumentException("مرجع المصدر مطلوب.");
    }

    private static string NormalizeRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message);
        }

        return value.Trim();
    }

    private static string? ValidateMovementRow(Guid facilityId, InmateMovementImportRow row)
    {
        if (string.IsNullOrWhiteSpace(row.InmateReferenceHash)) return "مرجع النزيل المموه مطلوب.";
        if (string.IsNullOrWhiteSpace(row.ExternalEventId)) return "معرف الحدث الخارجي مطلوب.";
        if (row.OccurredAtUtc == default) return "وقت الحركة مطلوب.";
        if (row.MovementType == MovementType.Admission && row.ToFacilityId is null) return "الدخول يتطلب وجهة.";
        if (row.MovementType == MovementType.Release && row.FromFacilityId is null) return "الإفراج يتطلب مصدرًا.";
        if (row.MovementType == MovementType.InternalTransfer && (row.FromFacilityUnitId is null || row.ToFacilityUnitId is null)) return "النقل الداخلي يتطلب وحدتين.";
        var hasMovementScope = row.FromFacilityId.HasValue
            || row.ToFacilityId.HasValue
            || row.FromFacilityUnitId.HasValue
            || row.ToFacilityUnitId.HasValue;
        if (hasMovementScope && row.FromFacilityId == row.ToFacilityId && row.FromFacilityUnitId == row.ToFacilityUnitId) return "لا يمكن تسجيل نقل إلى نفس النطاق.";
        if ((row.FromFacilityId.HasValue || row.ToFacilityId.HasValue) && row.FromFacilityId != facilityId && row.ToFacilityId != facilityId) return "الحركة لا تخص السجن المطلوب.";
        return null;
    }

    private static IReadOnlyList<string> BuildSummaryWarnings(int? capacity, SnapshotProjection? snapshot, OccupancyUnitBreakdownDto units, DateTimeOffset asOfUtc)
    {
        var warnings = new List<string>();
        if (!capacity.HasValue) warnings.Add("لا توجد طاقة تشغيلية معتمدة للسجن.");
        if (snapshot is null) warnings.Add("لا يوجد Snapshot إشغال موثوق للسجن.");
        if (snapshot is not null && Freshness(snapshot.CapturedAtUtc, asOfUtc) == "stale") warnings.Add("آخر Snapshot إشغال قديم.");
        if (units.Units.Any(u => u.StatusCode == OccupancyStatusCodes.Unknown)) warnings.Add("بعض الوحدات لا تحتوي طاقة أو عددًا موثوقًا.");
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
        if (!capturedAtUtc.HasValue) return OccupancyStatusCodes.Unknown;
        var age = asOfUtc - capturedAtUtc.Value;
        if (age <= TimeSpan.FromDays(1)) return "current";
        if (age <= TimeSpan.FromDays(3)) return "delayed";
        return "stale";
    }

    private static string Confidence(int? capacity, SnapshotProjection? snapshot, IReadOnlyList<string> warnings)
    {
        if (!capacity.HasValue || snapshot is null) return OccupancyStatusCodes.Unknown;
        if (snapshot.QualityStatus == CensusQualityStatus.Conflicting) return "low";
        return warnings.Count == 0 ? "high" : "medium";
    }

    private static IReadOnlyList<OccupancyInterventionDto> BuildInterventions(OccupancySummaryDto summary, OccupancyUnitBreakdownDto units)
    {
        var items = new List<OccupancyInterventionDto>();
        if (summary.StatusCode == OccupancyStatusCodes.OverCapacity)
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

        foreach (var unit in units.Units.Where(u => u.StatusCode is OccupancyStatusCodes.OverCapacity or OccupancyStatusCodes.Unknown).Take(10))
        {
            var isOverCapacity = unit.StatusCode == OccupancyStatusCodes.OverCapacity;
            items.Add(new OccupancyInterventionDto
            {
                Type = isOverCapacity ? "UnitOverCapacity" : "CapacityMissing",
                Reference = unit.UnitCode,
                TitleAr = unit.UnitNameAr,
                SeverityAr = isOverCapacity ? "عالية" : "متوسطة",
                PriorityRank = isOverCapacity ? 940 : 760,
                ReasonAr = isOverCapacity ? $"تجاوز بمقدار {unit.OverloadCount ?? 0}." : "بيانات الطاقة أو العدد غير مكتملة.",
                UnitId = unit.UnitId,
                ActionLabelAr = "فتح الوحدة"
            });
        }

        return items.OrderByDescending(i => i.PriorityRank).ToList();
    }

    private static int Count(IReadOnlyDictionary<MovementType, int> counts, MovementType type) =>
        counts.TryGetValue(type, out var count) ? count : 0;

    private static int CountByFlow(IReadOnlyDictionary<MovementType, int> counts, MovementFlow flow) =>
        counts.Where(item => MovementClassifier.Flow(item.Key) == flow).Sum(item => item.Value);

    private static IReadOnlyList<MovementTrendPointDto> BuildDailyTrend(IReadOnlyList<DailyMovementProjection> rows) =>
        rows.GroupBy(row => new { row.Year, row.Month, row.Day })
            .OrderBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Month)
            .ThenBy(group => group.Key.Day)
            .Select(group =>
            {
                var admissions = group.Where(row => MovementClassifier.Flow(row.MovementType) == MovementFlow.Inflow).Sum(row => row.Count);
                var releases = group.Where(row => MovementClassifier.Flow(row.MovementType) == MovementFlow.Outflow).Sum(row => row.Count);
                return new MovementTrendPointDto(
                    new DateOnly(group.Key.Year, group.Key.Month, group.Key.Day),
                    admissions,
                    releases,
                    group.Where(row => row.MovementType == MovementType.TransferIn).Sum(row => row.Count),
                    group.Where(row => row.MovementType == MovementType.TransferOut).Sum(row => row.Count),
                    admissions - releases);
            })
            .ToList();

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
            Death = 0,
            HospitalTransfers = 0,
            CourtTransfers = 0,
            Corrections = 0,
            OtherMovements = 0,
            NetMovement = 0,
            DailyTrend = []
        };

    private static OccupancySourceDisplay ResolveSource(SnapshotProjection? snapshot)
    {
        if (snapshot is null)
        {
            return new OccupancySourceDisplay(OccupancyStatusCodes.Unknown, "غير معروف");
        }

        if (snapshot.IsAuthoritative)
        {
            return new OccupancySourceDisplay("authoritative-snapshot", "Snapshot رسمي");
        }

        return new OccupancySourceDisplay("internal-snapshot", "Snapshot داخلي");
    }

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
    private sealed record SnapshotProjection(int InmateCount, DateTimeOffset CapturedAtUtc, bool IsAuthoritative, CensusQualityStatus QualityStatus);
    private sealed record OccupancySourceDisplay(string Code, string LabelAr);
    private sealed record UnitProjection(Guid Id, string Code, string NameAr);
    private sealed record UnitCapacityProjection(Guid? UnitId, int ApprovedCapacity, DateTimeOffset EffectiveFromUtc);
    private sealed record UnitSnapshotProjection(Guid? UnitId, int InmateCount, DateTimeOffset CapturedAtUtc, bool IsAuthoritative);
    private sealed record UnitOpenNotesProjection(Guid? UnitId, int Count);
    private sealed record DailyMovementProjection(int Year, int Month, int Day, MovementType MovementType, int Count);
    private sealed record MovementImportCandidate(InmateMovementImportRow Row, string ExternalEventId);
}

internal static class OccupancyStatusCodes
{
    public const string OverCapacity = "over-capacity";
    public const string Unknown = "unknown";
}

internal static class OccupancyClassifier
{
    public static (string Code, string LabelAr) Classify(int? capacity, int? current, OccupancyPolicyOptions options)
    {
        if (!capacity.HasValue || !current.HasValue || capacity.Value <= 0)
        {
            return (OccupancyStatusCodes.Unknown, "غير معروف");
        }

        var rate = Rate(capacity, current);
        if (!rate.HasValue)
        {
            return (OccupancyStatusCodes.Unknown, "غير معروف");
        }

        var rateValue = rate.Value;
        if (rateValue > 1m) return (OccupancyStatusCodes.OverCapacity, "متجاوز للطاقة");
        if (rateValue >= options.HighThreshold) return ("high", "مرتفع");
        if (rateValue >= options.AttentionThreshold) return ("attention", "يحتاج متابعة");
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

internal enum MovementFlow
{
    Inflow,
    Outflow,
    Neutral
}

internal static class MovementClassifier
{
    public static MovementFlow Flow(MovementType type) =>
        type switch
        {
            MovementType.Admission or MovementType.TransferIn or MovementType.ReturnFromLeave => MovementFlow.Inflow,
            MovementType.Release or MovementType.TransferOut or MovementType.TemporaryLeave or MovementType.Death => MovementFlow.Outflow,
            _ => MovementFlow.Neutral
        };
}

internal static class SqlServerOccupancyUniqueConstraintDetector
{
    private const string MovementImportUniqueIndex = "IX_InmateMovementEvents_SourceType_SourceReference_ExternalEventId";

    public static bool IsMovementImportDuplicate(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var number = current.GetType().GetProperty("Number")?.GetValue(current);
            if (number is not (2601 or 2627))
            {
                continue;
            }

            if (current.Message.Contains(MovementImportUniqueIndex, StringComparison.OrdinalIgnoreCase)
                || (current.Message.Contains("SourceReference", StringComparison.OrdinalIgnoreCase)
                    && current.Message.Contains("ExternalEventId", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
