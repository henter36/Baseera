namespace Baseera.UnitTests.SensitiveCustody;

using Baseera.Application.Abstractions;
using Baseera.Application.SensitiveCustody;
using Baseera.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;

public sealed class SensitiveSerialProtectionTests
{
    private readonly ISensitiveValueProtector protector =
        new DataProtectionSensitiveValueProtector(DataProtectionProvider.Create("Baseera.UnitTests"));

    [Fact]
    public void Protect_ProducesReversibleCiphertextDistinctFromPlaintext()
    {
        const string serial = "SN-REAL-12345";
        var ciphertext = protector.Protect(SensitiveSerialProtection.NormalizeSerial(serial));

        Assert.NotEqual(serial, ciphertext, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(serial, ciphertext, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            SensitiveSerialProtection.NormalizeSerial(serial),
            protector.Unprotect(ciphertext));
    }

    [Fact]
    public void Hash_IsDeterministicAfterNormalization_AndDiffersForDistinctSerials()
    {
        var left = SensitiveSerialProtection.Hash(" sn-abc-1 ");
        var right = SensitiveSerialProtection.Hash("SN-ABC-1");
        var other = SensitiveSerialProtection.Hash("SN-ABC-2");

        Assert.Equal(left, right);
        Assert.NotEqual(left, other);
    }

    [Fact]
    public void MaskPlaintext_UsesRealSerialSuffix()
    {
        var masked = SensitiveSerialProtection.MaskPlaintext("SN-REAL-9876");
        Assert.Equal("***-9876", masked);
    }

    [Fact]
    public void Unprotect_CorruptValue_DoesNotLeakCiphertextAsSerial()
    {
        Assert.ThrowsAny<Exception>(() => protector.Unprotect("not-a-valid-protected-payload"));
        Assert.Equal(SensitiveSerialProtection.UnavailableMask, SensitiveSerialProtection.MaskPlaintext(null));
    }

    [Fact]
    public void Purpose_IsVersionedForSerialNumbers() =>
        Assert.Equal("Baseera.SensitiveCustody.SerialNumber.v1", DataProtectionSensitiveValueProtector.SerialNumberPurpose);
}
