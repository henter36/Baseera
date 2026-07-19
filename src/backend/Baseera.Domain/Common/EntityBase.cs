namespace Baseera.Domain.Common;

public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public abstract class SoftDeletableEntity : EntityBase
{
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}

public interface IScopedEntity
{
    ScopeType ScopeType { get; }
    Guid? RegionId { get; }
    Guid? FacilityId { get; }
    Guid? FacilityUnitId { get; }
}

public enum ScopeType
{
    Global = 0,
    Headquarters = 1,
    Region = 2,
    Facility = 3,
    FacilityUnit = 4,
    MultipleRegions = 5,
    MultipleFacilities = 6
}
