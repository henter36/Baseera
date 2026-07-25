namespace Baseera.Application.Occupancy;

using Baseera.Domain.Occupancy;

public sealed record OccupancySummaryDto
{
    public required Guid FacilityId { get; init; }
    public required int? ApprovedCapacity { get; init; }
    public required int? CurrentCount { get; init; }
    public required decimal? OccupancyRate { get; init; }
    public required int? AvailablePlaces { get; init; }
    public required int? OverCapacityCount { get; init; }
    public required string StatusCode { get; init; }
    public required string StatusAr { get; init; }
    public required int UnitCount { get; init; }
    public required int OverloadedUnits { get; init; }
    public required int EmptyUnits { get; init; }
    public required DateTimeOffset? LatestSnapshotAtUtc { get; init; }
    public required string SourceCode { get; init; }
    public required string SourceAr { get; init; }
    public required string FreshnessStatus { get; init; }
    public required string ConfidenceLevel { get; init; }
    public required bool IsPartial { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed record OccupancyUnitDto
{
    public required Guid UnitId { get; init; }
    public required string UnitNameAr { get; init; }
    public required string UnitCode { get; init; }
    public required int? ApprovedCapacity { get; init; }
    public required int? CurrentCount { get; init; }
    public required decimal? OccupancyRate { get; init; }
    public required int? AvailablePlaces { get; init; }
    public required int? OverloadCount { get; init; }
    public required string StatusCode { get; init; }
    public required string StatusAr { get; init; }
    public required DateTimeOffset? LastUpdatedAtUtc { get; init; }
    public required string DataSourceAr { get; init; }
    public required int OpenNotesCount { get; init; }
    public required int OpenIncidentsCount { get; init; }
    public required int RiskCount { get; init; }
    public required IReadOnlyList<string> AlertReasons { get; init; }
}

public sealed record OccupancyUnitBreakdownDto(IReadOnlyList<OccupancyUnitDto> Units);

public sealed record MovementSummaryDto
{
    public required int Admissions { get; init; }
    public required int Releases { get; init; }
    public required int TransferIn { get; init; }
    public required int TransferOut { get; init; }
    public required int InternalTransfers { get; init; }
    public required int TemporaryLeave { get; init; }
    public required int Returns { get; init; }
    public required int Death { get; init; }
    public required int HospitalTransfers { get; init; }
    public required int CourtTransfers { get; init; }
    public required int Corrections { get; init; }
    public required int OtherMovements { get; init; }
    public required int NetMovement { get; init; }
    public required IReadOnlyList<MovementTrendPointDto> DailyTrend { get; init; }
}

public sealed record MovementTrendPointDto(DateOnly Date, int Admissions, int Releases, int TransfersIn, int TransfersOut, int Net);

public sealed record OccupancyInterventionDto
{
    public required string Type { get; init; }
    public required string Reference { get; init; }
    public required string TitleAr { get; init; }
    public required string SeverityAr { get; init; }
    public required int PriorityRank { get; init; }
    public required string ReasonAr { get; init; }
    public required string ActionLabelAr { get; init; }
    public Guid? UnitId { get; init; }
    public DateTimeOffset? DueAtUtc { get; init; }
}

public sealed record OccupancyWorkspacePayload
{
    public required OccupancySummaryDto Summary { get; init; }
    public required OccupancyUnitBreakdownDto UnitBreakdown { get; init; }
    public required MovementSummaryDto MovementSummary { get; init; }
    public required IReadOnlyList<OccupancyInterventionDto> Interventions { get; init; }
}

public sealed record OccupancyCapacityRequest
{
    public Guid? FacilityUnitId { get; init; }
    public CapacityType CapacityType { get; init; } = CapacityType.ApprovedOperational;
    public int ApprovedCapacity { get; init; }
    public required DateTimeOffset EffectiveFromUtc { get; init; }
    public DateTimeOffset? EffectiveToUtc { get; init; }
    public string? ApprovalReference { get; init; }
    public DateTimeOffset? ApprovalDateUtc { get; init; }
    public OccupancySourceType SourceType { get; init; } = OccupancySourceType.Manual;
    public required string SourceReference { get; init; }
    public string? Notes { get; init; }
}

public sealed record OccupancySnapshotRequest
{
    public Guid? FacilityUnitId { get; init; }
    public required DateTimeOffset CapturedAtUtc { get; init; }
    public int InmateCount { get; init; }
    public int? MaleCount { get; init; }
    public int? FemaleCount { get; init; }
    public int? AdultCount { get; init; }
    public int? JuvenileCount { get; init; }
    public int? MedicalCount { get; init; }
    public int? IsolationCount { get; init; }
    public OccupancySourceType SourceType { get; init; } = OccupancySourceType.Manual;
    public required string SourceReference { get; init; }
    public string? SourceVersion { get; init; }
    public bool IsAuthoritative { get; init; } = true;
    public CensusQualityStatus QualityStatus { get; init; } = CensusQualityStatus.Complete;
    public string? QualityNotes { get; init; }
}

public sealed record InmateMovementImportRequest
{
    public required string SourceSystem { get; init; }
    public required string ImportReference { get; init; }
    public required IReadOnlyList<InmateMovementImportRow> Rows { get; init; }
}

public sealed record InmateMovementImportRow
{
    public required string InmateReferenceHash { get; init; }
    public required MovementType MovementType { get; init; }
    public Guid? FromFacilityId { get; init; }
    public Guid? ToFacilityId { get; init; }
    public Guid? FromFacilityUnitId { get; init; }
    public Guid? ToFacilityUnitId { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required string ExternalEventId { get; init; }
    public string? ReasonCode { get; init; }
}

public sealed record OccupancyImportResult(int AcceptedRows, int DuplicateRows, IReadOnlyList<string> RejectedRows);
