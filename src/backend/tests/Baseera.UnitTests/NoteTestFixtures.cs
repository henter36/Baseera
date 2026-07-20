using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests;

/// <summary>
/// Shared scaffolding for Notes unit tests (InMemory EF provider). Each caller supplies a
/// fresh <see cref="CreateDb"/> instance so tests never leak state across each other.
/// </summary>
internal static class NoteTestFixtures
{
    public static readonly Guid DefaultNoteTypeId = Guid.Parse("44444444-4444-4444-4444-444444444403");

    public static BaseeraDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<BaseeraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static User AddUser(BaseeraDbContext db, string name = "user", bool active = true)
    {
        var user = new User
        {
            ExternalSubject = Guid.NewGuid().ToString(),
            UserName = Guid.NewGuid().ToString("N"),
            DisplayNameAr = name,
            IsActive = active,
            ProvisioningStatus = active ? UserProvisioningStatus.Active : UserProvisioningStatus.Disabled
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static void GrantPermissions(BaseeraDbContext db, Guid userId, string roleCode, params string[] permissionCodes)
    {
        var role = db.Roles.FirstOrDefault(r => r.Code == roleCode);
        if (role is null)
        {
            role = new Role { Code = roleCode, NameAr = roleCode, IsSystem = true };
            db.Roles.Add(role);
            db.SaveChanges();
        }

        if (!db.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == role.Id))
        {
            db.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });
        }

        EnsureDefaultNoteTypeGrant(db, role.Id);

        foreach (var code in permissionCodes)
        {
            var permission = db.Permissions.FirstOrDefault(p => p.Code == code);
            if (permission is null)
            {
                permission = new Permission { Code = code, NameAr = code, Module = "Notes" };
                db.Permissions.Add(permission);
                db.SaveChanges();
            }

            if (!db.RolePermissions.Any(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id))
            {
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            }
        }

        db.SaveChanges();
    }

    public static OperationalNote NewNote(
        ScopeType scopeType,
        Guid reportedBy,
        Guid? regionId = null,
        Guid? facilityId = null,
        Guid? facilityUnitId = null,
        NoteStatus status = NoteStatus.Draft,
        NoteSeverity severity = NoteSeverity.Medium,
        ClassificationLevel classification = ClassificationLevel.Internal,
        string reference = "OBS-00000001") => new()
    {
        ReferenceNumber = reference,
        Title = "عنوان تجريبي",
        Description = "وصف تجريبي",
        NoteTypeId = DefaultNoteTypeId,
        Severity = severity,
        Status = status,
        SourceType = NoteSourceType.Manual,
        Classification = classification,
        ScopeType = scopeType,
        RegionId = regionId,
        FacilityId = facilityId,
        FacilityUnitId = facilityUnitId,
        ReportedByUserId = reportedBy,
        ReportedAtUtc = DateTimeOffset.UtcNow
    };

    private static void EnsureDefaultNoteTypeGrant(BaseeraDbContext db, Guid roleId)
    {
        if (!db.NoteTypes.Any(t => t.Id == DefaultNoteTypeId))
        {
            db.NoteTypes.Add(new NoteType
            {
                Id = DefaultNoteTypeId,
                Code = "OPERATIONAL",
                NameAr = "تشغيلية",
                IsActive = true,
                SortOrder = 30,
                DefaultSeverity = NoteSeverity.Medium
            });
        }

        if (!db.RoleNoteTypeGrants.Any(g => g.RoleId == roleId && g.NoteTypeId == DefaultNoteTypeId))
        {
            db.RoleNoteTypeGrants.Add(new RoleNoteTypeGrant
            {
                RoleId = roleId,
                NoteTypeId = DefaultNoteTypeId,
                CanView = true,
                CanCreate = true,
                CanAssign = true,
                CanProcess = true,
                CanSubmitForVerification = true,
                CanReview = true,
                CanCancel = true,
                CanReopen = true,
                CanArchive = true,
                CanRestore = true,
                IsActive = true
            });
        }
    }
}
