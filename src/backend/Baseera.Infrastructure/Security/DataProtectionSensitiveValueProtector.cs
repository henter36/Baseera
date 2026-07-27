namespace Baseera.Infrastructure.Security;

using Baseera.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

public sealed class DataProtectionSensitiveValueProtector(IDataProtectionProvider provider) : ISensitiveValueProtector
{
    public const string SerialNumberPurpose = "Baseera.SensitiveCustody.SerialNumber.v1";

    private readonly IDataProtector protector = provider.CreateProtector(SerialNumberPurpose);

    public string Protect(string plaintext) => protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => protector.Unprotect(protectedValue);
}
