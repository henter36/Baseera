namespace Baseera.Domain.RiskManagement;

using Baseera.Domain.Common;
using Baseera.Domain.Organization;

public class RiskCategory : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public RiskCategory? ParentCategory { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public ICollection<RiskCategory> ChildCategories { get; set; } = [];
    public ICollection<RiskRecord> RiskRecords { get; set; } = [];
}
