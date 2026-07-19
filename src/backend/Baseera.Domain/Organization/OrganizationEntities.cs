namespace Baseera.Domain.Organization;

using Baseera.Domain.Common;

public class Organization : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Region> Regions { get; set; } = new List<Region>();
    public ICollection<Department> Departments { get; set; } = new List<Department>();
}

public class Region : SoftDeletableEntity, IScopedEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Facility> Facilities { get; set; } = new List<Facility>();

    public ScopeType ScopeType => ScopeType.Region;
    public Guid? RegionId => Id;
    public Guid? FacilityId => null;
    public Guid? FacilityUnitId => null;
}

public class Facility : SoftDeletableEntity, IScopedEntity
{
    public Guid RegionId { get; set; }
    public Region Region { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? FacilityType { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<FacilityUnit> Units { get; set; } = new List<FacilityUnit>();
    public ICollection<Building> Buildings { get; set; } = new List<Building>();

    public ScopeType ScopeType => ScopeType.Facility;
    Guid? IScopedEntity.RegionId => RegionId;
    public Guid? FacilityId => Id;
    public Guid? FacilityUnitId => null;
}

public class FacilityUnit : SoftDeletableEntity, IScopedEntity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public Guid? ParentUnitId { get; set; }
    public FacilityUnit? ParentUnit { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<FacilityUnit> Children { get; set; } = new List<FacilityUnit>();

    public ScopeType ScopeType => ScopeType.FacilityUnit;
    Guid? IScopedEntity.RegionId => Facility?.RegionId;
    Guid? IScopedEntity.FacilityId => FacilityId;
    public Guid? FacilityUnitId => Id;
}

public class Building : SoftDeletableEntity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<FacilityAssetLocation> Locations { get; set; } = new List<FacilityAssetLocation>();
}

public class FacilityAssetLocation : SoftDeletableEntity
{
    public Guid BuildingId { get; set; }
    public Building Building { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class Department : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid? ParentDepartmentId { get; set; }
    public Department? ParentDepartment { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Department> Children { get; set; } = new List<Department>();
}
