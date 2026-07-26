namespace Baseera.Application.SensitiveCustody;

using Baseera.Domain.SensitiveCustody;
using Baseera.Domain.Workforce;
using System.Security.Cryptography;
using System.Text;

public static class SensitiveCustodyOperationalCatalog
{
    public static class Interventions
    {
        public const string WeaponMissing = "WeaponMissing";
        public const string WeaponUnaccountedFor = "WeaponUnaccountedFor";
        public const string CustodyReturnOverdue = "CustodyReturnOverdue";
        public const string CustodyHandoverIncomplete = "CustodyHandoverIncomplete";
        public const string WeaponInspectionExpired = "WeaponInspectionExpired";
        public const string WeaponMaintenanceOverdue = "WeaponMaintenanceOverdue";
        public const string WeaponUnserviceable = "WeaponUnserviceable";
        public const string WeaponWrongLocation = "WeaponWrongLocation";
        public const string WeaponWrongCustodian = "WeaponWrongCustodian";
        public const string InventoryDiscrepancyCritical = "InventoryDiscrepancyCritical";
        public const string InventoryNotCompleted = "InventoryNotCompleted";
        public const string InventoryApprovalOverdue = "InventoryApprovalOverdue";
        public const string AmmunitionBelowMinimum = "AmmunitionBelowMinimum";
        public const string AmmunitionQuantityMismatch = "AmmunitionQuantityMismatch";
        public const string AmmunitionExpired = "AmmunitionExpired";
        public const string AmmunitionQuarantined = "AmmunitionQuarantined";
        public const string ArmoryInspectionExpired = "ArmoryInspectionExpired";
        public const string SensitiveDataStale = "SensitiveDataStale";
        public const string SourceConflict = "SourceConflict";
        public const string UnverifiedWeapon = "UnverifiedWeapon";
    }

    public static class DataQuality
    {
        public const string MissingAssetCode = "MissingAssetCode";
        public const string MissingEncryptedSerial = "MissingEncryptedSerial";
        public const string DuplicateSerialHash = "DuplicateSerialHash";
        public const string UnknownStatus = "UnknownStatus";
        public const string UnknownCondition = "UnknownCondition";
        public const string MissingCustody = "MissingCustody";
        public const string MultipleActiveCustodyRecords = "MultipleActiveCustodyRecords";
        public const string WeaponIssuedWithoutQualifiedCustodian = "WeaponIssuedWithoutQualifiedCustodian";
        public const string WeaponIssuedToUnavailableMember = "WeaponIssuedToUnavailableMember";
        public const string MissingArmoryLocation = "MissingArmoryLocation";
        public const string StaleVerification = "StaleVerification";
        public const string ExpiredInspection = "ExpiredInspection";
        public const string MissingMaintenanceHistory = "MissingMaintenanceHistory";
        public const string OpenTransactionWithoutCompletion = "OpenTransactionWithoutCompletion";
        public const string OverdueReturn = "OverdueReturn";
        public const string InventoryNotPerformed = "InventoryNotPerformed";
        public const string InventoryDiscrepancyUnresolved = "InventoryDiscrepancyUnresolved";
        public const string AmmunitionNegativeProjection = "AmmunitionNegativeProjection";
        public const string AmmunitionLotWithoutExpiry = "AmmunitionLotWithoutExpiry";
        public const string ExpiredAmmunitionStillAvailable = "ExpiredAmmunitionStillAvailable";
        public const string QuantityMismatch = "QuantityMismatch";
        public const string MissingRequirementBaseline = "MissingRequirementBaseline";
        public const string SourceMissing = "SourceMissing";
        public const string SourceConflict = "SourceConflict";
        public const string TransactionWithoutApprover = "TransactionWithoutApprover";
        public const string SameCreatorAndApprover = "SameCreatorAndApprover";
        public const string RetiredWeaponStillIssued = "RetiredWeaponStillIssued";
        public const string DestroyedWeaponStillCounted = "DestroyedWeaponStillCounted";
        public const string MissingAuditReference = "MissingAuditReference";
    }
}

public static class SensitiveSerialProtection
{
    public static string Hash(string value)
    {
        var normalized = Normalize(value);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    public static string ProtectForStorage(string value)
    {
        var hash = Hash(value);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"serial-hash:{hash}"));
    }

    public static string Mask(string? serialHash)
    {
        if (string.IsNullOrWhiteSpace(serialHash))
        {
            return "غير متوفر";
        }

        var suffix = serialHash.Length <= 6 ? serialHash : serialHash[^6..];
        return $"***-{suffix}";
    }

    private static string Normalize(string value) =>
        value.Trim().ToUpperInvariant();
}

public static class SensitiveCustodyReadinessPolicy
{
    public static bool IsOperationallyAvailable(WeaponStatus status, WeaponCondition condition) =>
        status is WeaponStatus.InArmory or WeaponStatus.IssuedToMember or WeaponStatus.IssuedToUnit
        && condition is WeaponCondition.Serviceable or WeaponCondition.ServiceableWithRestrictions;

    public static bool CountsAsIssued(WeaponStatus status) =>
        status is WeaponStatus.IssuedToMember or WeaponStatus.IssuedToUnit;

    public static bool IsFinal(WeaponStatus status) =>
        status is WeaponStatus.Retired or WeaponStatus.Destroyed;

    public static decimal? Rate(int numerator, int denominator) =>
        denominator == 0 ? null : Math.Round((decimal)numerator / denominator * 100m, 2);
}

public static class SensitiveCustodyTransactionPolicy
{
    private static readonly CustodyTransactionStatus[] TerminalStatuses =
    [
        CustodyTransactionStatus.Completed,
        CustodyTransactionStatus.Rejected,
        CustodyTransactionStatus.Cancelled,
        CustodyTransactionStatus.Reversed
    ];

    public static bool RequiresApproval(CustodyTransactionType type) =>
        type is CustodyTransactionType.IssueToMember
            or CustodyTransactionType.IssueToUnit
            or CustodyTransactionType.TransferBetweenArmories
            or CustodyTransactionType.TemporaryTransfer
            or CustodyTransactionType.ReportMissing
            or CustodyTransactionType.RecoverMissing
            or CustodyTransactionType.Retire
            or CustodyTransactionType.Destroy
            or CustodyTransactionType.Correction;

    public static bool IsTerminal(CustodyTransactionStatus status) =>
        TerminalStatuses.Contains(status);

    public static bool CanTransition(CustodyTransactionStatus current, CustodyTransactionStatus next) =>
        (current, next) switch
        {
            (CustodyTransactionStatus.Draft, CustodyTransactionStatus.PendingApproval) => true,
            (CustodyTransactionStatus.Draft, CustodyTransactionStatus.Approved) => true,
            (CustodyTransactionStatus.PendingApproval, CustodyTransactionStatus.Approved) => true,
            (CustodyTransactionStatus.PendingApproval, CustodyTransactionStatus.Rejected) => true,
            (CustodyTransactionStatus.Approved, CustodyTransactionStatus.HandedOver) => true,
            (CustodyTransactionStatus.HandedOver, CustodyTransactionStatus.Received) => true,
            (CustodyTransactionStatus.Received, CustodyTransactionStatus.Completed) => true,
            (CustodyTransactionStatus.Approved, CustodyTransactionStatus.Completed) => true,
            (_, CustodyTransactionStatus.Reversed) when current == CustodyTransactionStatus.Completed => true,
            _ => false
        };

    public static WeaponStatus CompletionStatus(CustodyTransactionType type) =>
        type switch
        {
            CustodyTransactionType.IssueToMember => WeaponStatus.IssuedToMember,
            CustodyTransactionType.IssueToUnit => WeaponStatus.IssuedToUnit,
            CustodyTransactionType.ReturnToArmory => WeaponStatus.InArmory,
            CustodyTransactionType.TransferBetweenArmories => WeaponStatus.InArmory,
            CustodyTransactionType.TemporaryTransfer => WeaponStatus.InTransit,
            CustodyTransactionType.SendToMaintenance => WeaponStatus.UnderMaintenance,
            CustodyTransactionType.ReturnFromMaintenance => WeaponStatus.UnderInspection,
            CustodyTransactionType.Quarantine => WeaponStatus.Quarantined,
            CustodyTransactionType.ReleaseFromQuarantine => WeaponStatus.InArmory,
            CustodyTransactionType.ReportMissing => WeaponStatus.Missing,
            CustodyTransactionType.RecoverMissing => WeaponStatus.UnderInspection,
            CustodyTransactionType.Retire => WeaponStatus.Retired,
            CustodyTransactionType.Destroy => WeaponStatus.Destroyed,
            CustodyTransactionType.Correction => WeaponStatus.UnderInvestigation,
            _ => WeaponStatus.Unknown
        };
}

public static class SensitiveCustodyEligibilityPolicy
{
    private static readonly AvailabilityType[] BlockingAvailability =
    [
        AvailabilityType.AnnualLeave,
        AvailabilityType.SickLeave,
        AvailabilityType.Training,
        AvailabilityType.ExternalAssignment,
        AvailabilityType.Suspended,
        AvailabilityType.RestrictedDuty,
        AvailabilityType.EmergencyLeave,
        AvailabilityType.UnexcusedAbsence
    ];

    public static bool IsEligibleMember(EmploymentStatus employmentStatus, bool isOperational) =>
        isOperational && employmentStatus is EmploymentStatus.Active or EmploymentStatus.SecondedIn;

    public static bool IsAvailable(AvailabilityType availabilityType, bool affectsOperationalAvailability) =>
        !affectsOperationalAvailability || !BlockingAvailability.Contains(availabilityType);

    public static bool HasWeaponQualification(QualificationStatus status, DateTimeOffset? expiresAtUtc, DateTimeOffset now) =>
        status is QualificationStatus.Valid or QualificationStatus.ExpiringSoon
        && (expiresAtUtc is null || expiresAtUtc > now);
}

public static class AmmunitionLedgerPolicy
{
    public static int Apply(int current, AmmunitionTransactionType type, int quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("كمية الذخيرة يجب أن تكون أكبر من صفر.");
        }

        var delta = type switch
        {
            AmmunitionTransactionType.Receipt => quantity,
            AmmunitionTransactionType.Return => quantity,
            AmmunitionTransactionType.TransferIn => quantity,
            AmmunitionTransactionType.Release => quantity,
            AmmunitionTransactionType.Issue => -quantity,
            AmmunitionTransactionType.Consumption => -quantity,
            AmmunitionTransactionType.TransferOut => -quantity,
            AmmunitionTransactionType.Damage => -quantity,
            AmmunitionTransactionType.Expiry => -quantity,
            AmmunitionTransactionType.Quarantine => -quantity,
            AmmunitionTransactionType.Destruction => -quantity,
            AmmunitionTransactionType.Adjustment => quantity,
            _ => 0
        };

        var next = current + delta;
        if (next < 0)
        {
            throw new InvalidOperationException("لا يسمح برصيد ذخيرة سالب.");
        }

        return next;
    }
}
