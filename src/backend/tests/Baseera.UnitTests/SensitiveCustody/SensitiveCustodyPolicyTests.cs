namespace Baseera.UnitTests.SensitiveCustody;

using Baseera.Application.SensitiveCustody;
using Baseera.Domain.SensitiveCustody;
using Baseera.Domain.Workforce;

public sealed class SensitiveCustodyPolicyTests
{
    [Theory]
    [InlineData(WeaponStatus.InArmory, WeaponCondition.Serviceable, true)]
    [InlineData(WeaponStatus.IssuedToMember, WeaponCondition.ServiceableWithRestrictions, true)]
    [InlineData(WeaponStatus.UnderMaintenance, WeaponCondition.Serviceable, false)]
    [InlineData(WeaponStatus.Missing, WeaponCondition.Serviceable, false)]
    [InlineData(WeaponStatus.InArmory, WeaponCondition.Unknown, false)]
    public void IsOperationallyAvailable_ExcludesUnsafeAndUnavailableStates(
        WeaponStatus status,
        WeaponCondition condition,
        bool expected) =>
        Assert.Equal(expected, SensitiveCustodyReadinessPolicy.IsOperationallyAvailable(status, condition));

    [Fact]
    public void CompletionStatus_MapsAppendOnlyTransactionsToCurrentStatus()
    {
        Assert.Equal(WeaponStatus.IssuedToMember, SensitiveCustodyTransactionPolicy.CompletionStatus(CustodyTransactionType.IssueToMember));
        Assert.Equal(WeaponStatus.InArmory, SensitiveCustodyTransactionPolicy.CompletionStatus(CustodyTransactionType.ReturnToArmory));
        Assert.Equal(WeaponStatus.Missing, SensitiveCustodyTransactionPolicy.CompletionStatus(CustodyTransactionType.ReportMissing));
        Assert.Equal(WeaponStatus.Destroyed, SensitiveCustodyTransactionPolicy.CompletionStatus(CustodyTransactionType.Destroy));
    }

    [Theory]
    [InlineData(CustodyTransactionStatus.PendingApproval, CustodyTransactionStatus.Approved, true)]
    [InlineData(CustodyTransactionStatus.Approved, CustodyTransactionStatus.HandedOver, true)]
    [InlineData(CustodyTransactionStatus.HandedOver, CustodyTransactionStatus.Received, true)]
    [InlineData(CustodyTransactionStatus.Completed, CustodyTransactionStatus.Approved, false)]
    [InlineData(CustodyTransactionStatus.Reversed, CustodyTransactionStatus.Completed, false)]
    public void CanTransition_EnforcesCustodyStateMachine(
        CustodyTransactionStatus current,
        CustodyTransactionStatus next,
        bool expected) =>
        Assert.Equal(expected, SensitiveCustodyTransactionPolicy.CanTransition(current, next));

    [Fact]
    public void AmmunitionLedger_PreventsNegativeBalances()
    {
        Assert.Equal(15, AmmunitionLedgerPolicy.Apply(10, AmmunitionTransactionType.Receipt, 5));
        Assert.Equal(7, AmmunitionLedgerPolicy.Apply(10, AmmunitionTransactionType.Issue, 3));
        Assert.Throws<InvalidOperationException>(() => AmmunitionLedgerPolicy.Apply(2, AmmunitionTransactionType.Issue, 3));
        Assert.Throws<InvalidOperationException>(() => AmmunitionLedgerPolicy.Apply(2, AmmunitionTransactionType.Receipt, 0));
    }

    [Fact]
    public void SerialProtection_DoesNotExposeRawSerial()
    {
        const string serial = "SN-REAL-12345";
        var hash = SensitiveSerialProtection.Hash(serial);
        var protectedValue = SensitiveSerialProtection.ProtectForStorage(serial);
        var masked = SensitiveSerialProtection.Mask(hash);

        Assert.DoesNotContain(serial, hash, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(serial, protectedValue, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("***-", masked, StringComparison.Ordinal);
        Assert.NotEqual(serial, masked);
    }

    [Theory]
    [InlineData(EmploymentStatus.Active, true, true)]
    [InlineData(EmploymentStatus.SecondedIn, true, true)]
    [InlineData(EmploymentStatus.Suspended, true, false)]
    [InlineData(EmploymentStatus.Retired, true, false)]
    [InlineData(EmploymentStatus.Active, false, false)]
    public void Eligibility_RequiresActiveOperationalMember(
        EmploymentStatus employmentStatus,
        bool isOperational,
        bool expected) =>
        Assert.Equal(expected, SensitiveCustodyEligibilityPolicy.IsEligibleMember(employmentStatus, isOperational));
}
