namespace Baseera.Application.Resources;

using Baseera.Domain.Identity;
using Baseera.Domain.Resources;

public static class ResourceAccessPolicy
{
    public static string? ViewPermissionFor(ResourceType resourceType) =>
        resourceType switch
        {
            ResourceType.Vehicle => PermissionCodes.ResourcesViewVehicles,
            ResourceType.CommunicationDevice => PermissionCodes.ResourcesViewCommunicationDevices,
            ResourceType.OperationalEquipment or ResourceType.SecurityEquipment => PermissionCodes.ResourcesViewEquipment,
            ResourceType.FacilityAsset => PermissionCodes.ResourcesViewFacilityAssets,
            _ => null
        };

    public static bool CanViewResourceType(IReadOnlyCollection<string> permissions, ResourceType resourceType)
    {
        var required = ViewPermissionFor(resourceType);
        return required is not null && permissions.Contains(required);
    }

    public static IReadOnlyList<ResourceType> ViewableResourceTypes(IReadOnlyCollection<string> permissions)
    {
        var types = new List<ResourceType>(5);
        foreach (ResourceType type in Enum.GetValues<ResourceType>())
        {
            if (CanViewResourceType(permissions, type))
            {
                types.Add(type);
            }
        }

        return types;
    }

    public static string NormalizeAssetCode(string value) =>
        value.Trim().ToUpperInvariant();

    public static bool PeriodsOverlap(
        DateTimeOffset existingFrom,
        DateTimeOffset? existingTo,
        DateTimeOffset candidateFrom,
        DateTimeOffset? candidateTo)
    {
        var existingEnd = existingTo ?? DateTimeOffset.MaxValue;
        var candidateEnd = candidateTo ?? DateTimeOffset.MaxValue;
        return existingFrom < candidateEnd && candidateFrom < existingEnd;
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

        if (string.Equals(status, "Confirmed", StringComparison.Ordinal))
        {
            return confirmedAtUtc.HasValue;
        }

        if (string.Equals(status, "Previewed", StringComparison.Ordinal))
        {
            return appliedRows == 0 && confirmedAtUtc is null;
        }

        return false;
    }

    public static bool IsResourceAssetsUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("IX_ResourceAssets_OrganizationId_AssetCode", StringComparison.OrdinalIgnoreCase)
                || (message.Contains("ResourceAssets", StringComparison.OrdinalIgnoreCase)
                    && message.Contains("AssetCode", StringComparison.OrdinalIgnoreCase)
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

    public static bool IsResourceImportBatchesUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("IX_ResourceImportBatches_FacilityId_SourceSystem_SourceReference_FileHash", StringComparison.OrdinalIgnoreCase)
                || (message.Contains("ResourceImportBatches", StringComparison.OrdinalIgnoreCase)
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
