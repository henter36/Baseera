namespace Baseera.Application.Abstractions;

using Baseera.Domain.Audit;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;

public interface IBaseeraDbContext
{
    IQueryable<Organization> Organizations { get; }
    IQueryable<Region> Regions { get; }
    IQueryable<Facility> Facilities { get; }
    IQueryable<FacilityUnit> FacilityUnits { get; }
    IQueryable<Building> Buildings { get; }
    IQueryable<FacilityAssetLocation> FacilityAssetLocations { get; }
    IQueryable<Department> Departments { get; }
    IQueryable<User> Users { get; }
    IQueryable<Role> Roles { get; }
    IQueryable<Permission> Permissions { get; }
    IQueryable<UserRole> UserRoles { get; }
    IQueryable<RolePermission> RolePermissions { get; }
    IQueryable<UserScope> UserScopes { get; }
    IQueryable<AuditLog> AuditLogs { get; }
    IQueryable<Attachment> Attachments { get; }

    void Add<TEntity>(TEntity entity) where TEntity : class;
    void Update<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? ExternalSubject { get; }
    string? DisplayName { get; }
    string? IpAddress { get; }
    string? CorrelationId { get; }
    IReadOnlyCollection<string> Permissions { get; }
    IReadOnlyCollection<UserScopeSnapshot> Scopes { get; }
    bool HasPermission(string permissionCode);
    bool IsGlobalScope { get; }
    bool HasHeadquartersScope { get; }
}

public sealed record UserScopeSnapshot(
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId);

public interface IOrganizationalScopeService
{
    bool HasNationalAccess { get; }
    bool HasHeadquartersAccess { get; }
    bool CanAccessRegion(Guid regionId);
    bool CanAccessFacility(Guid facilityId);
    bool CanAccessFacilityUnit(Guid facilityUnitId);
    IQueryable<Region> FilterRegions(IQueryable<Region> query);
    IQueryable<Facility> FilterFacilities(IQueryable<Facility> query);
    bool CanAccess(IScopedEntity entity);
    string SummarizeScopes();
}

public interface IAuditService
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}

public sealed class AuditEntry
{
    public required string Action { get; init; }
    public required string Module { get; init; }
    public required string EntityType { get; init; }
    public string? EntityId { get; init; }
    public object? OldValues { get; init; }
    public object? NewValues { get; init; }
    public string? Reason { get; init; }
    public string Outcome { get; init; } = "Success";
    public bool IsSensitiveView { get; init; }
}

public interface IFileStorage
{
    Task<StoredFileResult> SaveAsync(Stream content, string storedFileName, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}

public sealed record StoredFileResult(string StoragePath);

public interface IAttachmentService
{
    Task<Attachment> UploadAsync(UploadAttachmentRequest request, CancellationToken cancellationToken = default);
    Task<(Attachment Attachment, Stream Content)> DownloadAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}

public sealed class UploadAttachmentRequest
{
    public required string EntityType { get; init; }
    public required Guid EntityId { get; init; }
    public required string OriginalFileName { get; init; }
    public required string ContentType { get; init; }
    public required Stream Content { get; init; }
    public required long SizeBytes { get; init; }
    public ClassificationLevel Classification { get; init; } = ClassificationLevel.Internal;
    public string? UploadReason { get; init; }
}
