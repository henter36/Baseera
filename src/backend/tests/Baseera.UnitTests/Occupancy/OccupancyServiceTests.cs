namespace Baseera.UnitTests.Occupancy;

using Baseera.Application.Abstractions;
using Baseera.Application.Occupancy;
using Baseera.Application.Security;
using Baseera.Domain.Audit;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Domain.Occupancy;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Persistence;

public sealed class OccupancyServiceTests : IDisposable
{
    private readonly BaseeraDbContext db = NoteTestFixtures.CreateDb();
    private readonly DateTimeOffset now = new(2026, 7, 24, 9, 0, 0, TimeSpan.Zero);

    public OccupancyServiceTests()
    {
        SeedOrganization();
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task Summary_uses_authoritative_snapshot_and_classifies_over_capacity()
    {
        db.FacilityCapacityBaselines.Add(Capacity(null, 100, now.AddDays(-10)));
        db.InmateCensusSnapshots.AddRange(
            Snapshot(null, 90, now.AddHours(-1), false, "internal"),
            Snapshot(null, 107, now.AddHours(-2), true, "official"));
        await db.SaveChangesAsync();

        var summary = await Service().GetSummaryAsync(SeedIds.FacilityA1, now, CancellationToken.None);

        Assert.Equal(107, summary.CurrentCount);
        Assert.Equal(100, summary.ApprovedCapacity);
        Assert.Equal("over-capacity", summary.StatusCode);
        Assert.Equal(7, summary.OverCapacityCount);
        Assert.Equal("authoritative-snapshot", summary.SourceCode);
    }

    [Fact]
    public async Task Summary_reports_unknown_source_when_snapshot_is_missing()
    {
        var summary = await Service().GetSummaryAsync(SeedIds.FacilityA1, now, CancellationToken.None);

        Assert.Equal("unknown", summary.StatusCode);
        Assert.Equal("غير معروف", summary.StatusAr);
        Assert.Equal("unknown", summary.SourceCode);
        Assert.Equal("غير معروف", summary.SourceAr);
    }

    [Fact]
    public async Task Summary_reports_internal_source_for_non_authoritative_snapshot()
    {
        db.FacilityCapacityBaselines.Add(Capacity(null, 100, now.AddDays(-10)));
        db.InmateCensusSnapshots.Add(Snapshot(null, 80, now.AddHours(-1), false, "internal"));
        await db.SaveChangesAsync();

        var summary = await Service().GetSummaryAsync(SeedIds.FacilityA1, now, CancellationToken.None);

        Assert.Equal("internal-snapshot", summary.SourceCode);
        Assert.Equal("Snapshot داخلي", summary.SourceAr);
    }

    [Fact]
    public async Task Summary_classifies_normal_and_high_occupancy_without_changing_thresholds()
    {
        db.FacilityCapacityBaselines.Add(Capacity(null, 100, now.AddDays(-10)));
        db.InmateCensusSnapshots.AddRange(
            Snapshot(null, 70, now.AddHours(-4), true, "normal"),
            Snapshot(null, 96, now.AddHours(-1), true, "high"));
        await db.SaveChangesAsync();

        var normal = await Service().GetSummaryAsync(SeedIds.FacilityA1, now.AddHours(-2), CancellationToken.None);
        var high = await Service().GetSummaryAsync(SeedIds.FacilityA1, now, CancellationToken.None);

        Assert.Equal("normal", normal.StatusCode);
        Assert.Equal("طبيعي", normal.StatusAr);
        Assert.Equal("high", high.StatusCode);
        Assert.Equal("مرتفع", high.StatusAr);
    }

    [Fact]
    public async Task Summary_does_not_require_unit_or_movement_permissions()
    {
        db.FacilityCapacityBaselines.Add(Capacity(null, 100, now.AddDays(-10)));
        db.InmateCensusSnapshots.Add(Snapshot(null, 72, now.AddHours(-1), true, "official"));
        await db.SaveChangesAsync();

        var summaryOnly = Service([PermissionCodes.OccupancyViewSummary]);
        var summary = await summaryOnly.GetSummaryAsync(SeedIds.FacilityA1, now, CancellationToken.None);

        Assert.Equal(72, summary.CurrentCount);
        Assert.Equal(100, summary.ApprovedCapacity);
        Assert.Equal(0, summary.UnitCount);
    }

    [Fact]
    public async Task Unit_breakdown_flags_missing_capacity_and_overloaded_units()
    {
        db.FacilityUnits.AddRange(
            new FacilityUnit { Id = SeedIds.FacilityA1UnitNorth, FacilityId = SeedIds.FacilityA1, Code = "N", NameAr = "الشمال" },
            new FacilityUnit { Id = SeedIds.FacilityA1UnitSouth, FacilityId = SeedIds.FacilityA1, Code = "S", NameAr = "الجنوب" });
        db.FacilityCapacityBaselines.Add(Capacity(SeedIds.FacilityA1UnitNorth, 10, now.AddDays(-1)));
        db.InmateCensusSnapshots.AddRange(
            Snapshot(SeedIds.FacilityA1UnitNorth, 12, now.AddHours(-2), true, "north"),
            Snapshot(SeedIds.FacilityA1UnitSouth, 5, now.AddHours(-2), true, "south"));
        await db.SaveChangesAsync();

        var units = await Service().GetUnitBreakdownAsync(SeedIds.FacilityA1, now, CancellationToken.None);

        Assert.Contains(units.Units, unit => unit.UnitId == SeedIds.FacilityA1UnitNorth && unit.StatusCode == "over-capacity" && unit.AlertReasons.Contains("OverCapacity"));
        Assert.Contains(units.Units, unit => unit.UnitId == SeedIds.FacilityA1UnitSouth && unit.StatusCode == "unknown" && unit.AlertReasons.Contains("CapacityMissing"));
    }

    [Fact]
    public async Task Unit_breakdown_uses_latest_effective_capacity_authoritative_snapshot_open_notes_and_expected_order()
    {
        var user = NoteTestFixtures.AddUser(db);
        var firstUnit = Guid.NewGuid();
        var secondUnit = Guid.NewGuid();
        var thirdUnit = Guid.NewGuid();
        db.FacilityUnits.AddRange(
            new FacilityUnit { Id = firstUnit, FacilityId = SeedIds.FacilityA1, Code = "A", NameAr = "أ" },
            new FacilityUnit { Id = secondUnit, FacilityId = SeedIds.FacilityA1, Code = "B", NameAr = "ب" },
            new FacilityUnit { Id = thirdUnit, FacilityId = SeedIds.FacilityA1, Code = "C", NameAr = "ج" });
        db.FacilityCapacityBaselines.AddRange(
            Capacity(firstUnit, 80, now.AddDays(-20)),
            Capacity(firstUnit, 100, now.AddDays(-2)),
            Capacity(secondUnit, 100, now.AddDays(-20)),
            Capacity(thirdUnit, 50, now.AddDays(-20)));
        db.InmateCensusSnapshots.AddRange(
            Snapshot(firstUnit, 90, now.AddHours(-1), true, "first"),
            Snapshot(secondUnit, 70, now.AddMinutes(-30), false, "newer-internal"),
            Snapshot(secondUnit, 115, now.AddHours(-3), true, "older-official"),
            Snapshot(thirdUnit, 10, now.AddHours(-1), true, "third"));
        db.OperationalNotes.AddRange(
            NoteTestFixtures.NewNote(ScopeType.FacilityUnit, user.Id, SeedIds.RegionA, SeedIds.FacilityA1, firstUnit, NoteStatus.Open),
            NoteTestFixtures.NewNote(ScopeType.FacilityUnit, user.Id, SeedIds.RegionA, SeedIds.FacilityA1, firstUnit, NoteStatus.Closed));
        await db.SaveChangesAsync();

        var units = await Service().GetUnitBreakdownAsync(SeedIds.FacilityA1, now, CancellationToken.None);

        Assert.Collection(
            units.Units,
            unit =>
            {
                Assert.Equal(secondUnit, unit.UnitId);
                Assert.Equal(115, unit.CurrentCount);
                Assert.Equal(15, unit.OverloadCount);
                Assert.Equal(1.15m, unit.OccupancyRate);
                Assert.Equal("over-capacity", unit.StatusCode);
            },
            unit =>
            {
                Assert.Equal(firstUnit, unit.UnitId);
                Assert.Equal(100, unit.ApprovedCapacity);
                Assert.Equal(90, unit.CurrentCount);
                Assert.Equal(0.9m, unit.OccupancyRate);
                Assert.Equal(1, unit.OpenNotesCount);
            },
            unit =>
            {
                Assert.Equal(thirdUnit, unit.UnitId);
                Assert.Equal(40, unit.AvailablePlaces);
                Assert.Equal("normal", unit.StatusCode);
            });
    }

    [Fact]
    public async Task Movement_summary_counts_net_movement_without_identity_projection()
    {
        var reversedRelease = Movement("M5", MovementType.Release, now.AddDays(-1), from: SeedIds.FacilityA1);
        reversedRelease.IsReversed = true;
        db.InmateMovementEvents.AddRange(
            Movement("M1", MovementType.Admission, now.AddDays(-1), to: SeedIds.FacilityA1),
            Movement("M2", MovementType.TransferIn, now.AddDays(-1), to: SeedIds.FacilityA1),
            Movement("M3", MovementType.Release, now.AddDays(-1), from: SeedIds.FacilityA1),
            Movement("M4", MovementType.InternalTransfer, now.AddDays(-1), from: SeedIds.FacilityA1, to: SeedIds.FacilityA1, fromUnit: SeedIds.FacilityA1UnitNorth, toUnit: SeedIds.FacilityA1UnitSouth),
            reversedRelease);
        await db.SaveChangesAsync();

        var summary = await Service().GetMovementSummaryAsync(SeedIds.FacilityA1, now.AddDays(-2), now, CancellationToken.None);

        Assert.Equal(2, summary.Admissions);
        Assert.Equal(1, summary.TransferIn);
        Assert.Equal(1, summary.Releases);
        Assert.Equal(1, summary.InternalTransfers);
        Assert.Equal(1, summary.NetMovement);
        Assert.DoesNotContain("hash", string.Join(" ", summary.DailyTrend));
    }

    [Fact]
    public async Task Movement_summary_uses_one_flow_classification_for_totals_daily_trend_and_net()
    {
        db.InmateMovementEvents.AddRange(
            Movement("FLOW-1", MovementType.Admission, now.AddDays(-1), to: SeedIds.FacilityA1),
            Movement("FLOW-2", MovementType.TransferIn, now.AddDays(-1), to: SeedIds.FacilityA1),
            Movement("FLOW-3", MovementType.ReturnFromLeave, now.AddDays(-1), to: SeedIds.FacilityA1),
            Movement("FLOW-4", MovementType.Release, now.AddDays(-1), from: SeedIds.FacilityA1),
            Movement("FLOW-5", MovementType.TransferOut, now.AddDays(-1), from: SeedIds.FacilityA1),
            Movement("FLOW-6", MovementType.TemporaryLeave, now.AddDays(-1), from: SeedIds.FacilityA1),
            Movement("FLOW-7", MovementType.Death, now.AddDays(-1), from: SeedIds.FacilityA1),
            Movement("FLOW-8", MovementType.InternalTransfer, now.AddDays(-1), fromUnit: SeedIds.FacilityA1UnitNorth, toUnit: SeedIds.FacilityA1UnitSouth),
            Movement("FLOW-9", MovementType.HospitalTransfer, now.AddDays(-1)),
            Movement("FLOW-10", MovementType.CourtTransfer, now.AddDays(-1)),
            Movement("FLOW-11", MovementType.Correction, now.AddDays(-1)),
            Movement("FLOW-12", MovementType.Other, now.AddDays(-1)));
        await db.SaveChangesAsync();

        var summary = await Service().GetMovementSummaryAsync(SeedIds.FacilityA1, now.AddDays(-2), now, CancellationToken.None);
        var day = Assert.Single(summary.DailyTrend);

        Assert.Equal(3, summary.Admissions);
        Assert.Equal(4, summary.Releases);
        Assert.Equal(-1, summary.NetMovement);
        Assert.Equal(summary.Admissions, day.Admissions);
        Assert.Equal(summary.Releases, day.Releases);
        Assert.Equal(summary.NetMovement, day.Net);
        Assert.Equal(1, summary.Death);
        Assert.Equal(1, summary.HospitalTransfers);
        Assert.Equal(1, summary.CourtTransfers);
        Assert.Equal(1, summary.Corrections);
        Assert.Equal(1, summary.OtherMovements);
    }

    [Fact]
    public async Task Summary_staleness_uses_query_as_of_instead_of_wall_clock()
    {
        var capturedAt = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        db.FacilityCapacityBaselines.Add(Capacity(null, 100, capturedAt.AddDays(-10)));
        db.InmateCensusSnapshots.Add(Snapshot(null, 90, capturedAt, true, "historical"));
        await db.SaveChangesAsync();

        var summary = await Service().GetSummaryAsync(SeedIds.FacilityA1, capturedAt.AddHours(12), CancellationToken.None);

        Assert.Equal("current", summary.FreshnessStatus);
        Assert.DoesNotContain(summary.Warnings, warning => warning.Contains("قديم", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_is_idempotent_by_external_event_id()
    {
        var service = Service();
        var request = new InmateMovementImportRequest
        {
            SourceSystem = "inmate-system",
            ImportReference = "batch-1",
            Rows =
            [
                new InmateMovementImportRow
                {
                    InmateReferenceHash = "hash-1",
                    MovementType = MovementType.Admission,
                    ToFacilityId = SeedIds.FacilityA1,
                    OccurredAtUtc = now,
                    ExternalEventId = "event-1"
                }
            ]
        };

        var first = await service.ImportMovementsAsync(SeedIds.FacilityA1, request, CancellationToken.None);
        var second = await service.ImportMovementsAsync(SeedIds.FacilityA1, request, CancellationToken.None);

        Assert.Equal(1, first.AcceptedRows);
        Assert.Equal(0, first.DuplicateRows);
        Assert.Equal(0, second.AcceptedRows);
        Assert.Equal(1, second.DuplicateRows);
    }

    [Fact]
    public async Task Import_counts_duplicate_rows_inside_same_request_without_rejecting_batch()
    {
        var request = ImportRequest([
            ImportRow("duplicate-event", MovementType.Admission, to: SeedIds.FacilityA1),
            ImportRow(" duplicate-event ", MovementType.Admission, to: SeedIds.FacilityA1)
        ]);

        var result = await Service().ImportMovementsAsync(SeedIds.FacilityA1, request, CancellationToken.None);

        Assert.Equal(1, result.AcceptedRows);
        Assert.Equal(1, result.DuplicateRows);
        Assert.Empty(result.RejectedRows);
        Assert.Equal(1, db.InmateMovementEvents.Count(e => e.ExternalEventId == "duplicate-event"));
    }

    [Fact]
    public async Task Import_allows_internal_transfer_with_unit_scope_only()
    {
        AddUnits();
        var request = ImportRequest([
            ImportRow(
                "internal-unit-only",
                MovementType.InternalTransfer,
                fromUnit: SeedIds.FacilityA1UnitNorth,
                toUnit: SeedIds.FacilityA1UnitSouth)
        ]);

        var result = await Service().ImportMovementsAsync(SeedIds.FacilityA1, request, CancellationToken.None);

        Assert.Equal(1, result.AcceptedRows);
        Assert.Empty(result.RejectedRows);
    }

    [Fact]
    public async Task Import_rejects_internal_transfer_with_missing_unit()
    {
        var request = ImportRequest([
            ImportRow("internal-missing-unit", MovementType.InternalTransfer, fromUnit: SeedIds.FacilityA1UnitNorth)
        ]);

        var result = await Service().ImportMovementsAsync(SeedIds.FacilityA1, request, CancellationToken.None);

        Assert.Equal(0, result.AcceptedRows);
        Assert.Single(result.RejectedRows);
    }

    [Fact]
    public async Task Import_rejects_movement_with_unrelated_facility_identifier()
    {
        var request = ImportRequest([
            ImportRow("wrong-facility", MovementType.Admission, to: SeedIds.FacilityB1)
        ]);

        var result = await Service().ImportMovementsAsync(SeedIds.FacilityA1, request, CancellationToken.None);

        Assert.Equal(0, result.AcceptedRows);
        Assert.Single(result.RejectedRows);
    }

    [Fact]
    public async Task Import_allows_neutral_non_transfer_without_facility_identifiers()
    {
        var request = ImportRequest([
            ImportRow("neutral-correction", MovementType.Correction)
        ]);

        var result = await Service().ImportMovementsAsync(SeedIds.FacilityA1, request, CancellationToken.None);

        Assert.Equal(1, result.AcceptedRows);
        Assert.Empty(result.RejectedRows);
    }

    [Fact]
    public async Task Commands_reject_default_required_timestamps()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().RecordCapacityAsync(
            SeedIds.FacilityA1,
            new OccupancyCapacityRequest
            {
                ApprovedCapacity = 10,
                EffectiveFromUtc = default,
                SourceReference = "capacity"
            },
            CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() => Service().RecordSnapshotAsync(
            SeedIds.FacilityA1,
            new OccupancySnapshotRequest
            {
                CapturedAtUtc = default,
                InmateCount = 10,
                SourceReference = "snapshot"
            },
            CancellationToken.None));

        var import = await Service().ImportMovementsAsync(
            SeedIds.FacilityA1,
            ImportRequest([ImportRow("missing-time", MovementType.Admission, to: SeedIds.FacilityA1, occurredAt: DateTimeOffset.MinValue)]),
            CancellationToken.None);

        Assert.Single(import.RejectedRows);
    }

    private OccupancyService Service(IReadOnlyList<string>? permissions = null) =>
        new(
            db,
            new OrganizationalScopeService(CurrentUser(permissions), db),
            CurrentUser(permissions),
            new NoopAudit(),
            TimeProvider.System);

    private FakeCurrentUser CurrentUser(IReadOnlyList<string>? permissions = null) =>
        new(
            true,
            Guid.NewGuid(),
            "occupancy-user",
            "occupancy-user",
            permissions ??
            [
                PermissionCodes.OccupancyViewSummary,
                PermissionCodes.OccupancyViewUnitBreakdown,
                PermissionCodes.OccupancyViewMovements,
                PermissionCodes.OccupancyManageCapacity,
                PermissionCodes.OccupancyRecordSnapshot,
                PermissionCodes.OccupancyImport
            ],
            [new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null)]);

    private void SeedOrganization()
    {
        db.Organizations.Add(new Organization { Id = SeedIds.Organization, Code = "HQ", NameAr = "رئيسي" });
        db.Regions.Add(new Region { Id = SeedIds.RegionA, OrganizationId = SeedIds.Organization, Code = "A", NameAr = "أ" });
        db.Facilities.Add(new Facility { Id = SeedIds.FacilityA1, RegionId = SeedIds.RegionA, Code = "A1", NameAr = "سجن أ1", IsActive = true });
        db.SaveChanges();
    }

    private void AddUnits()
    {
        db.FacilityUnits.AddRange(
            new FacilityUnit { Id = SeedIds.FacilityA1UnitNorth, FacilityId = SeedIds.FacilityA1, Code = "N", NameAr = "الشمال", IsActive = true },
            new FacilityUnit { Id = SeedIds.FacilityA1UnitSouth, FacilityId = SeedIds.FacilityA1, Code = "S", NameAr = "الجنوب", IsActive = true });
        db.SaveChanges();
    }

    private static FacilityCapacityBaseline Capacity(Guid? unitId, int count, DateTimeOffset effectiveFrom) =>
        new()
        {
            OrganizationId = SeedIds.Organization,
            FacilityId = SeedIds.FacilityA1,
            FacilityUnitId = unitId,
            ApprovedCapacity = count,
            EffectiveFromUtc = effectiveFrom,
            SourceReference = Guid.NewGuid().ToString("N")
        };

    private static InmateCensusSnapshot Snapshot(Guid? unitId, int count, DateTimeOffset capturedAt, bool authoritative, string source) =>
        new()
        {
            OrganizationId = SeedIds.Organization,
            FacilityId = SeedIds.FacilityA1,
            FacilityUnitId = unitId,
            InmateCount = count,
            CapturedAtUtc = capturedAt,
            IsAuthoritative = authoritative,
            SourceReference = source,
            QualityStatus = CensusQualityStatus.Complete
        };

    private static InmateMovementEvent Movement(
        string id,
        MovementType type,
        DateTimeOffset occurredAt,
        Guid? from = null,
        Guid? to = null,
        Guid? fromUnit = null,
        Guid? toUnit = null) =>
        new()
        {
            OrganizationId = SeedIds.Organization,
            FacilityId = SeedIds.FacilityA1,
            InmateReferenceHash = $"hash-{id}",
            MovementType = type,
            FromFacilityId = from,
            ToFacilityId = to,
            FromFacilityUnitId = fromUnit,
            ToFacilityUnitId = toUnit,
            OccurredAtUtc = occurredAt,
            RecordedAtUtc = occurredAt.AddMinutes(1),
            SourceReference = "test",
            ExternalEventId = id
        };

    private InmateMovementImportRequest ImportRequest(IReadOnlyList<InmateMovementImportRow> rows) =>
        new()
        {
            SourceSystem = " inmate-system ",
            ImportReference = Guid.NewGuid().ToString("N"),
            Rows = rows
        };

    private InmateMovementImportRow ImportRow(
        string id,
        MovementType type,
        Guid? from = null,
        Guid? to = null,
        Guid? fromUnit = null,
        Guid? toUnit = null,
        DateTimeOffset? occurredAt = null) =>
        new()
        {
            InmateReferenceHash = $"hash-{id}",
            MovementType = type,
            FromFacilityId = from,
            ToFacilityId = to,
            FromFacilityUnitId = fromUnit,
            ToFacilityUnitId = toUnit,
            OccurredAtUtc = occurredAt ?? now,
            ExternalEventId = id
        };

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
