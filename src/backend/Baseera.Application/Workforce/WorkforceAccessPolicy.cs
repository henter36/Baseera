namespace Baseera.Application.Workforce;

using Baseera.Domain.Identity;
using Baseera.Domain.Workforce;

public static class WorkforceAccessPolicy
{
    private const string InvalidRowVersionMessage = "إصدار السجل غير صالح.";

    public static bool CanViewSummary(IReadOnlyCollection<string> permissions) =>
        permissions.Contains(PermissionCodes.WorkforceViewSummary);

    public static bool CanViewMembers(IReadOnlyCollection<string> permissions) =>
        permissions.Contains(PermissionCodes.WorkforceViewMembers);

    public static bool CanViewSensitiveRestrictions(IReadOnlyCollection<string> permissions) =>
        permissions.Contains(PermissionCodes.WorkforceViewSensitiveRestrictions);

    public static bool IsValidImportBatchState(
        string status,
        int appliedRows,
        DateTimeOffset? confirmedAtUtc)
    {
        if (string.Equals(status, WorkforceImportBatchStatuses.Confirmed, StringComparison.Ordinal))
        {
            return confirmedAtUtc.HasValue;
        }

        if (string.Equals(status, WorkforceImportBatchStatuses.Previewed, StringComparison.Ordinal))
        {
            return appliedRows == 0 && confirmedAtUtc is null;
        }

        return false;
    }

    public static bool IsValidImportBatchCounts(
        int totalRows,
        int validRows,
        int rejectedRows,
        int duplicateRows,
        int appliedRows,
        string status,
        DateTimeOffset? confirmedAtUtc)
    {
        if (totalRows < 0 || validRows < 0 || rejectedRows < 0 || duplicateRows < 0 || appliedRows < 0)
        {
            return false;
        }

        if (validRows + rejectedRows + duplicateRows != totalRows)
        {
            return false;
        }

        if (appliedRows > validRows)
        {
            return false;
        }

        return IsValidImportBatchState(status, appliedRows, confirmedAtUtc);
    }

    public static string NormalizeEmployeeNumber(string value) =>
        value.Trim().ToUpperInvariant();

    public static void EnsureRowVersion(byte[] current, string? incomingBase64)
    {
        if (string.IsNullOrWhiteSpace(incomingBase64))
        {
            throw new InvalidOperationException(InvalidRowVersionMessage);
        }

        byte[] incoming;
        try
        {
            incoming = Convert.FromBase64String(incomingBase64);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(InvalidRowVersionMessage);
        }
        catch (ArgumentNullException)
        {
            throw new InvalidOperationException(InvalidRowVersionMessage);
        }

        EnsureRowVersion(current, incoming);
    }

    public static void EnsureRowVersion(byte[] current, byte[]? incoming)
    {
        if (incoming is null || incoming.Length == 0)
        {
            throw new InvalidOperationException(InvalidRowVersionMessage);
        }

        if (!current.SequenceEqual(incoming))
        {
            throw new InvalidOperationException("تم تعديل السجل بواسطة مستخدم آخر. أعد التحميل ثم حاول مجددًا.");
        }
    }

    public static bool IsWorkforceMembersUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("IX_WorkforceMembers_OrganizationId_EmployeeNumber", StringComparison.OrdinalIgnoreCase)
                || (message.Contains("WorkforceMembers", StringComparison.OrdinalIgnoreCase)
                    && message.Contains("EmployeeNumber", StringComparison.OrdinalIgnoreCase)
                    && (message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("2627", StringComparison.Ordinal)
                        || message.Contains("2601", StringComparison.Ordinal))))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsWorkforceImportBatchesUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("IX_WorkforceImportBatches_FacilityId_ImportKind_SourceSystem_SourceReference_FileHash", StringComparison.OrdinalIgnoreCase)
                || message.Contains("IX_WorkforceImportBatches_FacilityId_SourceSystem_SourceReference_FileHash", StringComparison.OrdinalIgnoreCase)
                || (message.Contains("WorkforceImportBatches", StringComparison.OrdinalIgnoreCase)
                    && (message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("2627", StringComparison.Ordinal)
                        || message.Contains("2601", StringComparison.Ordinal))))
            {
                return true;
            }
        }

        return false;
    }
}
