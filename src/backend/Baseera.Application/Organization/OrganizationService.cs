namespace Baseera.Application.Organization;

using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Domain.Organization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record RegionDto(Guid Id, string Code, string NameAr, bool IsActive, DateTimeOffset CreatedAtUtc, string RowVersion);
public sealed record FacilityDto(Guid Id, Guid RegionId, string Code, string NameAr, string? FacilityType, bool IsActive, string RowVersion);
public sealed record FacilityUnitDto(Guid Id, Guid FacilityId, Guid? ParentUnitId, string Code, string NameAr, bool IsActive);
public sealed record DepartmentDto(Guid Id, Guid OrganizationId, Guid? ParentDepartmentId, string Code, string NameAr, bool IsActive);
public sealed record UpdateRegionRequest(string NameAr, bool IsActive, string RowVersion);
public sealed record CreateFacilityRequest(Guid RegionId, string Code, string NameAr, string? FacilityType);

public sealed class UpdateRegionRequestValidator : AbstractValidator<UpdateRegionRequest>
{
    public UpdateRegionRequestValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}

public sealed class CreateFacilityRequestValidator : AbstractValidator<CreateFacilityRequest>
{
    public CreateFacilityRequestValidator()
    {
        RuleFor(x => x.RegionId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
    }
}

public interface IOrganizationService
{
    Task<PagedResult<RegionDto>> ListRegionsAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<RegionDto?> GetRegionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RegionDto> UpdateRegionAsync(Guid id, UpdateRegionRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<FacilityDto>> ListFacilitiesAsync(PagedQuery query, Guid? regionId, CancellationToken cancellationToken = default);
    Task<FacilityDto?> GetFacilityAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FacilityDto> CreateFacilityAsync(CreateFacilityRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<FacilityUnitDto>> ListFacilityUnitsAsync(Guid facilityId, PagedQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<DepartmentDto>> ListDepartmentsAsync(PagedQuery query, CancellationToken cancellationToken = default);
}

public sealed class OrganizationService(
    IBaseeraDbContext db,
    IOrganizationalScopeService scope,
    ICurrentUser currentUser,
    IAuditService audit) : IOrganizationService
{
    public async Task<PagedResult<RegionDto>> ListRegionsAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        EnsurePermission("Organization.View");
        var q = scope.FilterRegions(db.Regions.Where(r => !r.IsDeleted));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(r => r.NameAr.Contains(term) || r.Code.Contains(term));
        }

        var total = q.Count();
        var items = q.OrderBy(r => r.Code)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToList()
            .Select(r => new RegionDto(r.Id, r.Code, r.NameAr, r.IsActive, r.CreatedAtUtc, Convert.ToBase64String(r.RowVersion)))
            .ToList();

        return new PagedResult<RegionDto> { Items = items, Page = query.Page, PageSize = query.Take, TotalCount = total };
    }

    public Task<RegionDto?> GetRegionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsurePermission("Organization.View");
        if (!scope.CanAccessRegion(id))
        {
            return Task.FromResult<RegionDto?>(null);
        }

        var r = db.Regions.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
        return Task.FromResult(r is null
            ? null
            : new RegionDto(r.Id, r.Code, r.NameAr, r.IsActive, r.CreatedAtUtc, Convert.ToBase64String(r.RowVersion)));
    }

    public async Task<RegionDto> UpdateRegionAsync(Guid id, UpdateRegionRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission("Organization.Manage");
        if (!scope.CanAccessRegion(id))
        {
            throw new UnauthorizedAccessException("لا صلاحية على نطاق هذه المنطقة.");
        }

        var region = db.Regions.FirstOrDefault(x => x.Id == id && !x.IsDeleted)
            ?? throw new KeyNotFoundException("المنطقة غير موجودة.");

        var incoming = Convert.FromBase64String(request.RowVersion);
        if (!region.RowVersion.SequenceEqual(incoming))
        {
            throw new InvalidOperationException("تم تعديل السجل بواسطة مستخدم آخر. أعد التحميل ثم حاول مجددًا.");
        }

        var old = new { region.NameAr, region.IsActive };
        region.NameAr = request.NameAr.Trim();
        region.IsActive = request.IsActive;
        region.UpdatedAtUtc = DateTimeOffset.UtcNow;
        region.UpdatedBy = currentUser.ExternalSubject;
        db.Update(region);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "Update",
            Module = "Organization",
            EntityType = nameof(Region),
            EntityId = region.Id.ToString(),
            OldValues = old,
            NewValues = new { region.NameAr, region.IsActive },
            Reason = "تحديث بيانات المنطقة"
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return new RegionDto(region.Id, region.Code, region.NameAr, region.IsActive, region.CreatedAtUtc, Convert.ToBase64String(region.RowVersion));
    }

    public Task<PagedResult<FacilityDto>> ListFacilitiesAsync(PagedQuery query, Guid? regionId, CancellationToken cancellationToken = default)
    {
        EnsurePermission("Organization.View");
        var q = scope.FilterFacilities(db.Facilities.Where(f => !f.IsDeleted));
        if (regionId.HasValue)
        {
            if (!scope.CanAccessRegion(regionId.Value))
            {
                throw new UnauthorizedAccessException("لا صلاحية على نطاق هذه المنطقة.");
            }

            q = q.Where(f => f.RegionId == regionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(f => f.NameAr.Contains(term) || f.Code.Contains(term));
        }

        var total = q.Count();
        var items = q.OrderBy(f => f.Code)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToList()
            .Select(f => new FacilityDto(f.Id, f.RegionId, f.Code, f.NameAr, f.FacilityType, f.IsActive, Convert.ToBase64String(f.RowVersion)))
            .ToList();

        return Task.FromResult(new PagedResult<FacilityDto> { Items = items, Page = query.Page, PageSize = query.Take, TotalCount = total });
    }

    public Task<FacilityDto?> GetFacilityAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsurePermission("Organization.View");
        if (!scope.CanAccessFacility(id))
        {
            return Task.FromResult<FacilityDto?>(null);
        }

        var f = db.Facilities.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
        return Task.FromResult(f is null
            ? null
            : new FacilityDto(f.Id, f.RegionId, f.Code, f.NameAr, f.FacilityType, f.IsActive, Convert.ToBase64String(f.RowVersion)));
    }

    public async Task<FacilityDto> CreateFacilityAsync(CreateFacilityRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission("Organization.Manage");
        if (!scope.CanAccessRegion(request.RegionId))
        {
            throw new UnauthorizedAccessException("لا صلاحية على نطاق هذه المنطقة.");
        }

        if (!db.Regions.Any(r => r.Id == request.RegionId && !r.IsDeleted))
        {
            throw new KeyNotFoundException("المنطقة غير موجودة.");
        }

        var facility = new Facility
        {
            RegionId = request.RegionId,
            Code = request.Code.Trim(),
            NameAr = request.NameAr.Trim(),
            FacilityType = request.FacilityType,
            CreatedBy = currentUser.ExternalSubject
        };
        db.Add(facility);
        await audit.WriteAsync(new AuditEntry
        {
            Action = "Create",
            Module = "Organization",
            EntityType = nameof(Facility),
            EntityId = facility.Id.ToString(),
            NewValues = new { facility.Code, facility.NameAr, facility.RegionId }
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new FacilityDto(facility.Id, facility.RegionId, facility.Code, facility.NameAr, facility.FacilityType, facility.IsActive, Convert.ToBase64String(facility.RowVersion));
    }

    public async Task<PagedResult<FacilityUnitDto>> ListFacilityUnitsAsync(Guid facilityId, PagedQuery query, CancellationToken cancellationToken = default)
    {
        EnsurePermission("Organization.View");
        if (!scope.CanAccessFacility(facilityId))
        {
            throw new UnauthorizedAccessException("لا صلاحية على نطاق هذا السجن.");
        }

        var q = db.FacilityUnits.Where(u => u.FacilityId == facilityId && !u.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(u => u.NameAr.Contains(term) || u.Code.Contains(term));
        }

        var total = await q.CountAsync(cancellationToken);
        var items = await q.OrderBy(u => u.Code)
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(u => new FacilityUnitDto(u.Id, u.FacilityId, u.ParentUnitId, u.Code, u.NameAr, u.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<FacilityUnitDto> { Items = items, Page = query.Page, PageSize = query.Take, TotalCount = total };
    }

    public async Task<PagedResult<DepartmentDto>> ListDepartmentsAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        EnsurePermission("Organization.View");
        var q = db.Departments.Where(d => !d.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(d => d.NameAr.Contains(term) || d.Code.Contains(term));
        }

        var total = await q.CountAsync(cancellationToken);
        var items = await q.OrderBy(d => d.Code)
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(d => new DepartmentDto(d.Id, d.OrganizationId, d.ParentDepartmentId, d.Code, d.NameAr, d.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<DepartmentDto> { Items = items, Page = query.Page, PageSize = query.Take, TotalCount = total };
    }

    private void EnsurePermission(string code)
    {
        if (!currentUser.HasPermission(code))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تنفيذ هذه العملية.");
        }
    }
}
