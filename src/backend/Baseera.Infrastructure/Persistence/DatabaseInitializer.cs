namespace Baseera.Infrastructure.Persistence;

using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, bool seedDemoData, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        await db.Database.MigrateAsync(cancellationToken);
        await SeedReferenceDataAsync(db, cancellationToken);

        if (seedDemoData)
        {
            await SeedDemoOrganizationAsync(db, logger, cancellationToken);
        }
    }

    public static async Task SeedReferenceDataAsync(BaseeraDbContext db, CancellationToken cancellationToken = default)
    {
        var permissions = BuildPermissions();
        foreach (var permission in permissions)
        {
            if (!await db.Permissions.AnyAsync(p => p.Code == permission.Code, cancellationToken))
            {
                db.Permissions.Add(permission);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var roles = BuildRoles();
        foreach (var role in roles)
        {
            if (!await db.Roles.AnyAsync(r => r.Code == role.Code, cancellationToken))
            {
                db.Roles.Add(role);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        await EnsureRolePermissionsAsync(db, cancellationToken);
    }

    private static async Task EnsureRolePermissionsAsync(BaseeraDbContext db, CancellationToken cancellationToken)
    {
        var permissionMap = await db.Permissions.ToDictionaryAsync(p => p.Code, cancellationToken);
        var roles = await db.Roles.ToListAsync(cancellationToken);

        void Grant(Role role, params string[] codes)
        {
            foreach (var code in codes)
            {
                if (!permissionMap.TryGetValue(code, out var permission))
                {
                    continue;
                }

                var exists = db.RolePermissions.Local.Any(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id)
                             || db.RolePermissions.Any(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id);
                if (exists)
                {
                    continue;
                }

                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            }
        }

        var admin = roles.First(r => r.Code == RoleCodes.SystemAdministrator);
        Grant(admin, permissionMap.Keys.ToArray());

        var auditor = roles.First(r => r.Code == RoleCodes.Auditor);
        Grant(auditor,
            PermissionCodes.OrganizationView,
            PermissionCodes.UsersView,
            PermissionCodes.AuditView,
            PermissionCodes.AttachmentsDownload,
            PermissionCodes.AttachmentsDownloadSensitive);

        var readonlyUser = roles.First(r => r.Code == RoleCodes.ReadOnlyUser);
        Grant(readonlyUser, PermissionCodes.OrganizationView);

        var hq = roles.First(r => r.Code == RoleCodes.HeadquartersExecutive);
        Grant(hq, PermissionCodes.OrganizationView, PermissionCodes.UsersView, PermissionCodes.AuditView);

        var regional = roles.First(r => r.Code == RoleCodes.RegionalDirector);
        Grant(regional, PermissionCodes.OrganizationView, PermissionCodes.AttachmentsUpload, PermissionCodes.AttachmentsDownload);

        var facilityDirector = roles.First(r => r.Code == RoleCodes.FacilityDirector);
        Grant(facilityDirector, PermissionCodes.OrganizationView, PermissionCodes.AttachmentsUpload, PermissionCodes.AttachmentsDownload);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDemoOrganizationAsync(BaseeraDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        if (!await db.Organizations.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seeding development organization hierarchy (not a production mock API path).");

            var org = new Organization
            {
                Id = SeedIds.Organization,
                Code = "HQ",
                NameAr = "المستوى الرئيسي",
                NameEn = "Headquarters"
            };
            var regionA = new Region
            {
                Id = SeedIds.RegionA,
                OrganizationId = org.Id,
                Code = "RG-A",
                NameAr = "منطقة أ"
            };
            var regionB = new Region
            {
                Id = SeedIds.RegionB,
                OrganizationId = org.Id,
                Code = "RG-B",
                NameAr = "منطقة ب"
            };
            var facilityA1 = new Facility
            {
                Id = SeedIds.FacilityA1,
                RegionId = regionA.Id,
                Code = "FAC-A1",
                NameAr = "سجن أ-1",
                FacilityType = "Prison"
            };
            var facilityA2 = new Facility
            {
                Id = SeedIds.FacilityA2,
                RegionId = regionA.Id,
                Code = "FAC-A2",
                NameAr = "سجن أ-2",
                FacilityType = "Prison"
            };
            var facilityB1 = new Facility
            {
                Id = SeedIds.FacilityB1,
                RegionId = regionB.Id,
                Code = "FAC-B1",
                NameAr = "سجن ب-1",
                FacilityType = "Prison"
            };

            db.Organizations.Add(org);
            db.Regions.AddRange(regionA, regionB);
            db.Facilities.AddRange(facilityA1, facilityA2, facilityB1);
            await db.SaveChangesAsync(cancellationToken);
        }

        await EnsureDevAdminAsync(db, cancellationToken);
    }

    private static async Task EnsureDevAdminAsync(BaseeraDbContext db, CancellationToken cancellationToken)
    {
        const string subject = "dev-admin";
        var user = await db.Users.FirstOrDefaultAsync(u => u.ExternalSubject == subject, cancellationToken);
        if (user is null)
        {
            user = new User
            {
                ExternalSubject = subject,
                UserName = subject,
                DisplayNameAr = "مسؤول التطوير",
                Email = "dev-admin@baseera.local",
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }

        var adminRole = await db.Roles.FirstAsync(r => r.Code == RoleCodes.SystemAdministrator, cancellationToken);
        if (!await db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == adminRole.Id, cancellationToken))
        {
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
        }

        if (!await db.UserScopes.AnyAsync(s => s.UserId == user.Id && s.ScopeType == ScopeType.Global, cancellationToken))
        {
            db.UserScopes.Add(new UserScope
            {
                UserId = user.Id,
                ScopeType = ScopeType.Global,
                IsActive = true
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static List<Permission> BuildPermissions()
    {
        (string Code, string NameAr, string Module)[] items =
        [
            (PermissionCodes.OrganizationView, "عرض الهيكل التنظيمي", "Organization"),
            (PermissionCodes.OrganizationManage, "إدارة الهيكل التنظيمي", "Organization"),
            (PermissionCodes.UsersView, "عرض المستخدمين", "Identity"),
            (PermissionCodes.UsersManage, "إدارة المستخدمين", "Identity"),
            (PermissionCodes.RolesManage, "إدارة الأدوار", "Identity"),
            (PermissionCodes.ScopesManage, "إدارة النطاقات", "Identity"),
            (PermissionCodes.AuditView, "عرض سجل التدقيق", "Audit"),
            (PermissionCodes.AttachmentsUpload, "رفع المرفقات", "Attachments"),
            (PermissionCodes.AttachmentsDownload, "تنزيل المرفقات", "Attachments"),
            (PermissionCodes.AttachmentsDownloadSensitive, "تنزيل المرفقات الحساسة", "Attachments"),
            (PermissionCodes.VehiclesView, "عرض المركبات", "Vehicles"),
            (PermissionCodes.VehiclesCreate, "إضافة مركبة", "Vehicles"),
            (PermissionCodes.VehiclesUpdate, "تحديث مركبة", "Vehicles"),
            (PermissionCodes.VehiclesTransfer, "نقل مركبة", "Vehicles"),
            (PermissionCodes.VehiclesDecommission, "استبعاد مركبة", "Vehicles"),
            (PermissionCodes.ArmamentView, "عرض التسليح", "Armament"),
            (PermissionCodes.ArmamentIssue, "صرف تسليح", "Armament"),
            (PermissionCodes.ArmamentReceive, "استلام تسليح", "Armament"),
            (PermissionCodes.ArmamentInventory, "جرد تسليح", "Armament"),
            (PermissionCodes.ArmamentAdjust, "تسوية تسليح", "Armament"),
            (PermissionCodes.NotesAssign, "إسناد ملاحظة", "Notes"),
            (PermissionCodes.NotesVerifyClosure, "اعتماد إغلاق ملاحظة", "Notes"),
            (PermissionCodes.IncidentsApprove, "اعتماد واقعة", "Incidents"),
            (PermissionCodes.FormsDesign, "تصميم نموذج", "Forms"),
            (PermissionCodes.FormsPublish, "نشر نموذج", "Forms"),
            (PermissionCodes.FormsSubmit, "إرسال نموذج", "Forms"),
            (PermissionCodes.FormsReview, "مراجعة نموذج", "Forms"),
            (PermissionCodes.ProjectsApprove, "اعتماد مشروع", "Projects"),
            (PermissionCodes.StrategyManage, "إدارة الاستراتيجية", "Strategy"),
            (PermissionCodes.ReportsExportSensitive, "تصدير تقارير حساسة", "Reports")
        ];

        return items.Select(i => new Permission
        {
            Id = Guid.NewGuid(),
            Code = i.Code,
            NameAr = i.NameAr,
            Module = i.Module
        }).ToList();
    }

    private static List<Role> BuildRoles()
    {
        (string Code, string NameAr)[] items =
        [
            (RoleCodes.SystemAdministrator, "مسؤول النظام"),
            (RoleCodes.HeadquartersExecutive, "تنفيذي المستوى الرئيسي"),
            (RoleCodes.DecisionSupportDirector, "مدير دعم القرار"),
            (RoleCodes.DecisionAnalyst, "محلل قرارات"),
            (RoleCodes.RegionalDirector, "مدير منطقة"),
            (RoleCodes.RegionalCoordinator, "منسق منطقة"),
            (RoleCodes.FacilityDirector, "مدير سجن"),
            (RoleCodes.FacilityCoordinator, "منسق سجن"),
            (RoleCodes.SecurityOfficer, "ضابط أمن"),
            (RoleCodes.ArmamentOfficer, "ضابط تسليح"),
            (RoleCodes.FleetOfficer, "ضابط أسطول"),
            (RoleCodes.WorkforceOfficer, "ضابط قوى عاملة"),
            (RoleCodes.IncidentOfficer, "ضابط وقائع"),
            (RoleCodes.PrisonerCaseOfficer, "ضابط حالات نزلاء"),
            (RoleCodes.ProjectManager, "مدير مشاريع"),
            (RoleCodes.StrategyOfficer, "ضابط استراتيجية"),
            (RoleCodes.FormDesigner, "مصمم نماذج"),
            (RoleCodes.FormReviewer, "مراجع نماذج"),
            (RoleCodes.Auditor, "مدقق"),
            (RoleCodes.ReadOnlyUser, "مستخدم قراءة فقط")
        ];

        return items.Select(i => new Role
        {
            Id = Guid.NewGuid(),
            Code = i.Code,
            NameAr = i.NameAr,
            IsSystem = true
        }).ToList();
    }
}

public static class SeedIds
{
    public static readonly Guid Organization = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid RegionA = Guid.Parse("22222222-2222-2222-2222-222222222201");
    public static readonly Guid RegionB = Guid.Parse("22222222-2222-2222-2222-222222222202");
    public static readonly Guid FacilityA1 = Guid.Parse("33333333-3333-3333-3333-333333333301");
    public static readonly Guid FacilityA2 = Guid.Parse("33333333-3333-3333-3333-333333333302");
    public static readonly Guid FacilityB1 = Guid.Parse("33333333-3333-3333-3333-333333333303");
}
