namespace Baseera.Infrastructure.Persistence;

using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        bool seedDemoData,
        bool applyMigrations,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        if (applyMigrations)
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

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
        await EnsureNoteTypesAndRoleGrantsAsync(db, cancellationToken);
        await EnsureFormGovernancePolicyAsync(db, cancellationToken);
    }

    private static async Task EnsureRolePermissionsAsync(BaseeraDbContext db, CancellationToken cancellationToken)
    {
        var permissionMap = await db.Permissions.ToDictionaryAsync(p => p.Code, cancellationToken);
        var roles = await db.Roles.ToListAsync(cancellationToken);

        void Grant(Role role, params object[] codes)
        {
            foreach (var code in ExpandCodes(codes))
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

        static IEnumerable<string> ExpandCodes(IEnumerable<object> codes)
        {
            foreach (var code in codes)
            {
                if (code is string value)
                {
                    yield return value;
                }
                else if (code is IEnumerable<string> values)
                {
                    foreach (var nested in values)
                    {
                        yield return nested;
                    }
                }
            }
        }

        var admin = roles.First(r => r.Code == RoleCodes.SystemAdministrator);
        Grant(admin, permissionMap.Keys.ToArray());

        string[] caViewOnly = [PermissionCodes.CorrectiveActionsView];
        string[] caReviewer =
        [
            PermissionCodes.CorrectiveActionsView,
            PermissionCodes.CorrectiveActionsViewSensitive,
            PermissionCodes.CorrectiveActionsAssign,
            PermissionCodes.CorrectiveActionsVerifyCompletion,
            PermissionCodes.CorrectiveActionsReturnForRework,
            PermissionCodes.CorrectiveActionsReopen,
            PermissionCodes.CorrectiveActionsCancel,
            PermissionCodes.CorrectiveActionsArchive,
            PermissionCodes.CorrectiveActionsRestore
        ];
        string[] caDirector =
        [
            PermissionCodes.CorrectiveActionsView,
            PermissionCodes.CorrectiveActionsCreate,
            PermissionCodes.CorrectiveActionsUpdate,
            PermissionCodes.CorrectiveActionsAssign,
            PermissionCodes.CorrectiveActionsVerifyCompletion,
            PermissionCodes.CorrectiveActionsReturnForRework,
            PermissionCodes.CorrectiveActionsReopen,
            PermissionCodes.CorrectiveActionsCancel
        ];
        string[] caCoordinator =
        [
            PermissionCodes.CorrectiveActionsView,
            PermissionCodes.CorrectiveActionsCreate,
            PermissionCodes.CorrectiveActionsUpdate,
            PermissionCodes.CorrectiveActionsAssign,
            PermissionCodes.CorrectiveActionsStartWork,
            PermissionCodes.CorrectiveActionsSubmitForVerification,
            PermissionCodes.CorrectiveActionsCancel
        ];
        string[] ownNotifications =
        [
            PermissionCodes.NotificationsViewOwn,
            PermissionCodes.NotificationsMarkRead,
            PermissionCodes.NotificationsArchiveOwn
        ];
        string[] escalationViewer =
        [
            PermissionCodes.EscalationsView,
            PermissionCodes.EscalationsViewOccurrences
        ];
        string[] escalationManager =
        [
            PermissionCodes.EscalationsView,
            PermissionCodes.EscalationsManage,
            PermissionCodes.EscalationsActivate,
            PermissionCodes.EscalationsRun,
            PermissionCodes.EscalationsViewOccurrences,
            PermissionCodes.EscalationsRetryFailed
        ];
        string[] noteTypeManagers =
        [
            PermissionCodes.NotesManageTypes,
            PermissionCodes.NotesManageRoleTypeAccess,
            PermissionCodes.NotesManageUserTypeOverrides,
            PermissionCodes.NotesManageIntakeProfiles
        ];
        string[] routingViewer =
        [
            PermissionCodes.NotesViewRouting,
            PermissionCodes.NotesViewRoutingDiagnostics
        ];
        string[] routingManager =
        [
            PermissionCodes.NotesViewRouting,
            PermissionCodes.NotesManageRoutingRules,
            PermissionCodes.NotesActivateRoutingRules,
            PermissionCodes.NotesRunRouting,
            PermissionCodes.NotesViewRoutingDiagnostics
        ];
        string[] dashboardFull =
        [
            PermissionCodes.DashboardViewOperational,
            PermissionCodes.DashboardViewRisk,
            PermissionCodes.DashboardViewRouting,
            PermissionCodes.DashboardViewCorrectiveActions
        ];
        string[] dashboardScoped =
        [
            PermissionCodes.DashboardViewOperational,
            PermissionCodes.DashboardViewRisk,
            PermissionCodes.DashboardViewCorrectiveActions
        ];
        string[] dashboardReadOnly = [PermissionCodes.DashboardViewOperational];

        var auditor = roles.First(r => r.Code == RoleCodes.Auditor);
        Grant(auditor,
            PermissionCodes.OrganizationView,
            PermissionCodes.UsersView,
            PermissionCodes.AuditView,
            PermissionCodes.AttachmentsDownload,
            PermissionCodes.AttachmentsDownloadSensitive,
            PermissionCodes.NotesView,
            caViewOnly,
            ownNotifications,
            dashboardReadOnly);

        var readonlyUser = roles.First(r => r.Code == RoleCodes.ReadOnlyUser);
        Grant(readonlyUser, PermissionCodes.OrganizationView, PermissionCodes.NotesView, caViewOnly, ownNotifications, dashboardReadOnly);

        var hq = roles.First(r => r.Code == RoleCodes.HeadquartersExecutive);
        Grant(hq,
            PermissionCodes.OrganizationView,
            PermissionCodes.UsersView,
            PermissionCodes.AuditView,
            PermissionCodes.NotesView,
            PermissionCodes.NotesViewSensitive,
            PermissionCodes.NotesAssign,
            PermissionCodes.NotesVerifyClosure,
            PermissionCodes.NotesReopen,
            PermissionCodes.NotesCancel,
            PermissionCodes.NotesArchive,
            PermissionCodes.NotesRestore,
            caReviewer,
            routingViewer,
            escalationViewer,
            ownNotifications,
            dashboardFull);

        var decisionDirector = roles.First(r => r.Code == RoleCodes.DecisionSupportDirector);
        Grant(decisionDirector,
            PermissionCodes.NotesView,
            PermissionCodes.NotesCreate,
            PermissionCodes.NotesUpdate,
            PermissionCodes.NotesAssign,
            PermissionCodes.NotesVerifyClosure,
            PermissionCodes.NotesReturnForRework,
            PermissionCodes.NotesReopen,
            PermissionCodes.NotesCancel,
            noteTypeManagers,
            routingManager,
            caDirector,
            escalationManager,
            ownNotifications,
            dashboardFull);

        var regional = roles.First(r => r.Code == RoleCodes.RegionalDirector);
        Grant(regional,
            PermissionCodes.OrganizationView,
            PermissionCodes.AttachmentsUpload,
            PermissionCodes.AttachmentsDownload,
            PermissionCodes.NotesView,
            PermissionCodes.NotesCreate,
            PermissionCodes.NotesUpdate,
            PermissionCodes.NotesAssign,
            PermissionCodes.NotesVerifyClosure,
            PermissionCodes.NotesReturnForRework,
            PermissionCodes.NotesReopen,
            PermissionCodes.NotesCancel,
            PermissionCodes.NotesArchive,
            PermissionCodes.NotesRestore,
            PermissionCodes.NotesManageUserTypeOverrides,
            PermissionCodes.NotesManageIntakeProfiles,
            routingManager,
            caDirector,
            PermissionCodes.CorrectiveActionsArchive,
            PermissionCodes.CorrectiveActionsRestore,
            escalationViewer,
            ownNotifications,
            dashboardScoped);

        var regionalCoordinator = roles.First(r => r.Code == RoleCodes.RegionalCoordinator);
        Grant(regionalCoordinator,
            PermissionCodes.NotesView,
            PermissionCodes.NotesCreate,
            PermissionCodes.NotesUpdate,
            PermissionCodes.NotesAssign,
            PermissionCodes.NotesStartWork,
            PermissionCodes.NotesSubmitForVerification,
            PermissionCodes.NotesCancel,
            caCoordinator,
            ownNotifications);

        var facilityDirector = roles.First(r => r.Code == RoleCodes.FacilityDirector);
        Grant(facilityDirector,
            PermissionCodes.OrganizationView,
            PermissionCodes.AttachmentsUpload,
            PermissionCodes.AttachmentsDownload,
            PermissionCodes.NotesView,
            PermissionCodes.NotesCreate,
            PermissionCodes.NotesUpdate,
            PermissionCodes.NotesAssign,
            PermissionCodes.NotesVerifyClosure,
            PermissionCodes.NotesReturnForRework,
            PermissionCodes.NotesReopen,
            PermissionCodes.NotesCancel,
            PermissionCodes.NotesArchive,
            PermissionCodes.NotesRestore,
            PermissionCodes.NotesManageUserTypeOverrides,
            PermissionCodes.NotesManageIntakeProfiles,
            routingManager,
            caDirector,
            PermissionCodes.CorrectiveActionsArchive,
            PermissionCodes.CorrectiveActionsRestore,
            escalationViewer,
            ownNotifications,
            dashboardScoped);

        string[] formsDesigner =
        [
            PermissionCodes.FormsView,
            PermissionCodes.FormsCreate,
            PermissionCodes.FormsUpdateDraft,
            PermissionCodes.FormsSubmitForReview,
            PermissionCodes.FormsCloneVersion,
            PermissionCodes.FormsViewVersionHistory,
            PermissionCodes.FormsManageTemplates
        ];
        string[] formsReviewer =
        [
            PermissionCodes.FormsView,
            PermissionCodes.FormsReview,
            PermissionCodes.FormsRequestChanges,
            PermissionCodes.FormsReject
        ];
        string[] formsApprover =
        [
            PermissionCodes.FormsView,
            PermissionCodes.FormsApprove,
            PermissionCodes.FormsReject
        ];
        string[] formsPublisher = [PermissionCodes.FormsView, PermissionCodes.FormsPublish];
        string[] formsRegionalMonitor = [PermissionCodes.FormsView, PermissionCodes.FormsMonitorRegion];
        string[] formsHqMonitor =
        [
            PermissionCodes.FormsView,
            PermissionCodes.FormsMonitorHeadquarters
        ];
        string[] formsAnalyst = [PermissionCodes.FormsView, PermissionCodes.FormsAnalyze];
        string[] formsAuditorView = [PermissionCodes.FormsView];

        var formDesigner = roles.First(r => r.Code == RoleCodes.FormDesigner);
        Grant(formDesigner, formsDesigner);
        var formReviewer = roles.First(r => r.Code == RoleCodes.FormReviewer);
        Grant(formReviewer, formsReviewer);
        var formApprover = roles.First(r => r.Code == RoleCodes.FormApprover);
        Grant(formApprover, formsApprover);
        var formPublisher = roles.First(r => r.Code == RoleCodes.FormPublisher);
        Grant(formPublisher, formsPublisher);
        var formRegionalMonitor = roles.First(r => r.Code == RoleCodes.FormRegionalMonitor);
        Grant(formRegionalMonitor, formsRegionalMonitor);
        var formHqMonitor = roles.First(r => r.Code == RoleCodes.FormHeadquartersMonitor);
        Grant(formHqMonitor, formsHqMonitor);
        var formAnalyst = roles.First(r => r.Code == RoleCodes.FormAnalyst);
        Grant(formAnalyst, formsAnalyst);
        Grant(auditor, formsAuditorView);

        var facilityCoordinator = roles.First(r => r.Code == RoleCodes.FacilityCoordinator);
        Grant(facilityCoordinator,
            PermissionCodes.NotesView,
            PermissionCodes.NotesCreate,
            PermissionCodes.NotesUpdate,
            PermissionCodes.NotesStartWork,
            PermissionCodes.NotesSubmitForVerification,
            PermissionCodes.NotesCancel,
            PermissionCodes.CorrectiveActionsView,
            PermissionCodes.CorrectiveActionsCreate,
            PermissionCodes.CorrectiveActionsUpdate,
            PermissionCodes.CorrectiveActionsStartWork,
            PermissionCodes.CorrectiveActionsSubmitForVerification,
            PermissionCodes.CorrectiveActionsCancel,
            ownNotifications);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureFormGovernancePolicyAsync(BaseeraDbContext db, CancellationToken cancellationToken)
    {
        if (await db.FormGovernancePolicies.AnyAsync(cancellationToken))
        {
            return;
        }

        db.FormGovernancePolicies.Add(new FormGovernancePolicy
        {
            Id = SeedIds.FormGovernancePolicy,
            RequireReviewBeforeApproval = true,
            RequireSeparationOfDuties = true,
            AllowDesignerToReviewOwnForm = false,
            AllowReviewerToApproveOwnReview = false,
            AllowApproverToPublish = true,
            DefaultRetentionDays = 365,
            SensitiveRetentionDays = 730,
            MinimumRetentionDays = 30,
            AuditSensitiveViews = true,
            AuditExports = true,
            RequireReasonForArchive = true,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureNoteTypesAndRoleGrantsAsync(BaseeraDbContext db, CancellationToken cancellationToken)
    {
        var definitions = InitialNoteTypes();
        foreach (var definition in definitions)
        {
            var existing = await db.NoteTypes.FirstOrDefaultAsync(t => t.Code == definition.Code, cancellationToken);
            if (existing is null)
            {
                db.NoteTypes.Add(new NoteType
                {
                    Id = definition.Id,
                    Code = definition.Code,
                    NameAr = definition.NameAr,
                    DescriptionAr = definition.DescriptionAr,
                    EntryInstructionsAr = definition.EntryInstructionsAr,
                    SortOrder = definition.SortOrder,
                    IsActive = true,
                    DefaultSeverity = definition.DefaultSeverity,
                    DefaultDueDays = definition.DefaultDueDays,
                    CreatedBy = "seed"
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var noteTypeIds = await db.NoteTypes
            .Select(noteType => noteType.Id)
            .ToListAsync(cancellationToken);
        var roles = await db.Roles.ToListAsync(cancellationToken);
        var existingGrantPairs = await db.RoleNoteTypeGrants
            .AsNoTracking()
            .Select(grant => new
            {
                grant.RoleId,
                grant.NoteTypeId
            })
            .ToListAsync(cancellationToken);
        var existingGrantKeys = existingGrantPairs
            .Select(grant => (grant.RoleId, grant.NoteTypeId))
            .ToHashSet();

        foreach (var role in roles)
        {
            foreach (var noteTypeId in noteTypeIds)
            {
                if (!existingGrantKeys.Add((role.Id, noteTypeId)))
                {
                    continue;
                }

                db.RoleNoteTypeGrants.Add(
                    BuildDefaultGrant(
                        role.Code,
                        role.Id,
                        noteTypeId));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    [Flags]
    private enum NoteTypeSeedCapabilities
    {
        None = 0,
        View = 1 << 0,
        Create = 1 << 1,
        Assign = 1 << 2,
        Process = 1 << 3,
        SubmitForVerification = 1 << 4,
        Review = 1 << 5,
        Cancel = 1 << 6,
        Reopen = 1 << 7,
        Archive = 1 << 8,
        Restore = 1 << 9,

        Viewer = View,

        HeadquartersReviewer =
            View |
            Create |
            Assign |
            Review |
            Cancel |
            Reopen,

        ScopedReviewer =
            HeadquartersReviewer |
            Archive |
            Restore,

        RegionalCoordinator =
            View |
            Create |
            Assign |
            Process |
            SubmitForVerification |
            Cancel,

        FacilityCoordinator =
            View |
            Create |
            Process |
            SubmitForVerification |
            Cancel,

        All =
            View |
            Create |
            Assign |
            Process |
            SubmitForVerification |
            Review |
            Cancel |
            Reopen |
            Archive |
            Restore
    }

    private static readonly IReadOnlyDictionary<string, NoteTypeSeedCapabilities>
        DefaultNoteTypeCapabilities =
            new Dictionary<string, NoteTypeSeedCapabilities>
            {
                [RoleCodes.SystemAdministrator] =
                    NoteTypeSeedCapabilities.All,

                [RoleCodes.DecisionSupportDirector] =
                    NoteTypeSeedCapabilities.All,

                [RoleCodes.HeadquartersExecutive] =
                    NoteTypeSeedCapabilities.HeadquartersReviewer,

                [RoleCodes.RegionalDirector] =
                    NoteTypeSeedCapabilities.ScopedReviewer,

                [RoleCodes.FacilityDirector] =
                    NoteTypeSeedCapabilities.ScopedReviewer,

                [RoleCodes.RegionalCoordinator] =
                    NoteTypeSeedCapabilities.RegionalCoordinator,

                [RoleCodes.FacilityCoordinator] =
                    NoteTypeSeedCapabilities.FacilityCoordinator,

                [RoleCodes.Auditor] =
                    NoteTypeSeedCapabilities.Viewer,

                [RoleCodes.ReadOnlyUser] =
                    NoteTypeSeedCapabilities.Viewer
            };

    private static RoleNoteTypeGrant BuildDefaultGrant(
        string roleCode,
        Guid roleId,
        Guid noteTypeId)
    {
        var capabilities =
            DefaultNoteTypeCapabilities.GetValueOrDefault(roleCode);

        return new RoleNoteTypeGrant
        {
            RoleId = roleId,
            NoteTypeId = noteTypeId,
            CanView = HasCapability(
                capabilities,
                NoteTypeSeedCapabilities.View),
            CanCreate = HasCapability(
                capabilities,
                NoteTypeSeedCapabilities.Create),
            CanAssign = HasCapability(
                capabilities,
                NoteTypeSeedCapabilities.Assign),
            CanProcess = HasCapability(
                capabilities,
                NoteTypeSeedCapabilities.Process),
            CanSubmitForVerification = HasCapability(
                capabilities,
                NoteTypeSeedCapabilities.SubmitForVerification),
            CanReview = HasCapability(
                capabilities,
                NoteTypeSeedCapabilities.Review),
            CanCancel = HasCapability(
                capabilities,
                NoteTypeSeedCapabilities.Cancel),
            CanReopen = HasCapability(
                capabilities,
                NoteTypeSeedCapabilities.Reopen),
            CanArchive = HasCapability(
                capabilities,
                NoteTypeSeedCapabilities.Archive),
            CanRestore = HasCapability(
                capabilities,
                NoteTypeSeedCapabilities.Restore),
            IsActive = true,
            CreatedBy = "seed"
        };
    }

    private static bool HasCapability(
        NoteTypeSeedCapabilities capabilities,
        NoteTypeSeedCapabilities required) =>
        (capabilities & required) == required;

    private static (Guid Id, string Code, string NameAr, string DescriptionAr, string EntryInstructionsAr, int SortOrder, NoteSeverity DefaultSeverity, int? DefaultDueDays)[] InitialNoteTypes() =>
    [
        (SeedIds.NoteTypeSecurity, "SECURITY", "أمنية", "ملاحظات مرتبطة بالأمن والسلامة الأمنية.", "سجّل الوقائع الأمنية بدقة ودون كشف معلومات حساسة غير لازمة.", 10, NoteSeverity.High, 3),
        (SeedIds.NoteTypeTechnical, "TECHNICAL", "فنية", "ملاحظات الأعطال والاحتياجات الفنية.", "حدّد الموقع والأثر الفني وأي مرجع صيانة متاح.", 20, NoteSeverity.Medium, 7),
        (SeedIds.NoteTypeOperational, "OPERATIONAL", "تشغيلية", "ملاحظات سير العمل والتشغيل اليومي.", "اشرح الأثر التشغيلي والإجراء المطلوب.", 30, NoteSeverity.Medium, 5),
        (SeedIds.NoteTypeHealthSafety, "HEALTH_SAFETY", "صحة وسلامة", "ملاحظات الصحة والسلامة المهنية.", "اذكر الخطر والإجراءات الوقائية العاجلة إن وجدت.", 40, NoteSeverity.High, 3),
        (SeedIds.NoteTypeAdministrative, "ADMINISTRATIVE", "إدارية", "ملاحظات إدارية عامة.", "اكتب سياقًا مختصرًا والجهة المعنية.", 50, NoteSeverity.Low, 10),
        (SeedIds.NoteTypeOther, "OTHER", "أخرى", "ملاحظات لا تندرج تحت نوع آخر.", "استخدم هذا النوع عند عدم انطباق الأنواع الأخرى.", 60, NoteSeverity.Medium, 7)
    ];

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

    private const string OrganizationModule = "Organization";
    private const string NotesModule = "Notes";
    private const string CorrectiveActionsModule = "CorrectiveActions";
    private const string EscalationsModule = "Escalations";
    private const string NotificationsModule = "Notifications";
    private const string DashboardModule = "Dashboard";
    private const string FormsModule = "Forms";

    private static List<Permission> BuildPermissions()
    {
        (string Code, string NameAr, string Module)[] items =
        [
            (PermissionCodes.OrganizationView, "عرض الهيكل التنظيمي", OrganizationModule),
            (PermissionCodes.OrganizationManage, "إدارة الهيكل التنظيمي", OrganizationModule),
            (PermissionCodes.UsersView, "عرض المستخدمين", "Identity"),
            (PermissionCodes.UsersManage, "إدارة المستخدمين", "Identity"),
            (PermissionCodes.RolesManage, "إدارة الأدوار", "Identity"),
            (PermissionCodes.ScopesManage, "إدارة النطاقات", "Identity"),
            (PermissionCodes.AuditView, "عرض سجل التدقيق", "Audit"),
            (PermissionCodes.AttachmentsUpload, "رفع المرفقات", "Attachments"),
            (PermissionCodes.AttachmentsDownload, "تنزيل المرفقات", "Attachments"),
            (PermissionCodes.AttachmentsDownloadSensitive, "تنزيل المرفقات الحساسة", "Attachments"),
            (PermissionCodes.UsersArchive, "أرشفة مستخدم", "Identity"),
            (PermissionCodes.UsersRestore, "استعادة مستخدم", "Identity"),
            (PermissionCodes.OrganizationArchive, "أرشفة تنظيمي", OrganizationModule),
            (PermissionCodes.OrganizationRestore, "استعادة تنظيمي", OrganizationModule),
            (PermissionCodes.GrantGlobalScope, "منح نطاق وطني", "Identity"),
            (PermissionCodes.GrantHeadquartersScope, "منح نطاق المستوى الرئيسي", "Identity"),
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
            (PermissionCodes.NotesAssign, "إسناد ملاحظة", NotesModule),
            (PermissionCodes.NotesVerifyClosure, "اعتماد إغلاق ملاحظة", NotesModule),
            (PermissionCodes.NotesView, "عرض الملاحظات", NotesModule),
            (PermissionCodes.NotesViewSensitive, "عرض الملاحظات الحساسة", NotesModule),
            (PermissionCodes.NotesCreate, "إنشاء ملاحظة", NotesModule),
            (PermissionCodes.NotesUpdate, "تحديث ملاحظة", NotesModule),
            (PermissionCodes.NotesStartWork, "بدء معالجة ملاحظة", NotesModule),
            (PermissionCodes.NotesSubmitForVerification, "إرسال ملاحظة للتحقق", NotesModule),
            (PermissionCodes.NotesReturnForRework, "إعادة ملاحظة للمعالجة", NotesModule),
            (PermissionCodes.NotesReopen, "إعادة فتح ملاحظة", NotesModule),
            (PermissionCodes.NotesCancel, "إلغاء ملاحظة", NotesModule),
            (PermissionCodes.NotesArchive, "أرشفة ملاحظة", NotesModule),
            (PermissionCodes.NotesRestore, "استعادة ملاحظة", NotesModule),
            (PermissionCodes.NotesManageTypes, "إدارة أنواع الملاحظات", NotesModule),
            (PermissionCodes.NotesManageRoleTypeAccess, "إدارة صلاحيات أنواع الملاحظات للأدوار", NotesModule),
            (PermissionCodes.NotesManageUserTypeOverrides, "إدارة استثناءات أنواع الملاحظات للمستخدمين", NotesModule),
            (PermissionCodes.NotesManageIntakeProfiles, "إدارة سياق إدخال الملاحظات", NotesModule),
            (PermissionCodes.NotesViewRouting, "عرض قواعد توجيه الملاحظات", NotesModule),
            (PermissionCodes.NotesManageRoutingRules, "إدارة قواعد توجيه الملاحظات", NotesModule),
            (PermissionCodes.NotesActivateRoutingRules, "تفعيل وتعطيل قواعد توجيه الملاحظات", NotesModule),
            (PermissionCodes.NotesRunRouting, "تشغيل توجيه الملاحظات", NotesModule),
            (PermissionCodes.NotesViewRoutingDiagnostics, "عرض تشخيصات توجيه الملاحظات", NotesModule),
            (PermissionCodes.CorrectiveActionsView, "عرض الإجراءات التصحيحية", CorrectiveActionsModule),
            (PermissionCodes.CorrectiveActionsViewSensitive, "عرض الإجراءات التصحيحية الحساسة", CorrectiveActionsModule),
            (PermissionCodes.CorrectiveActionsCreate, "إنشاء إجراء تصحيحي", CorrectiveActionsModule),
            (PermissionCodes.CorrectiveActionsUpdate, "تحديث إجراء تصحيحي", CorrectiveActionsModule),
            (PermissionCodes.CorrectiveActionsAssign, "تكليف إجراء تصحيحي", CorrectiveActionsModule),
            (PermissionCodes.CorrectiveActionsStartWork, "بدء معالجة إجراء تصحيحي", CorrectiveActionsModule),
            (PermissionCodes.CorrectiveActionsSubmitForVerification, "إرسال إجراء تصحيحي للتحقق", CorrectiveActionsModule),
            (PermissionCodes.CorrectiveActionsVerifyCompletion, "اعتماد إنجاز إجراء تصحيحي", CorrectiveActionsModule),
            (PermissionCodes.CorrectiveActionsReturnForRework, "إعادة إجراء تصحيحي للمعالجة", CorrectiveActionsModule),
            (PermissionCodes.CorrectiveActionsReopen, "إعادة فتح إجراء تصحيحي", CorrectiveActionsModule),
            (PermissionCodes.CorrectiveActionsCancel, "إلغاء إجراء تصحيحي", CorrectiveActionsModule),
            (PermissionCodes.CorrectiveActionsArchive, "أرشفة إجراء تصحيحي", CorrectiveActionsModule),
            (PermissionCodes.CorrectiveActionsRestore, "استعادة إجراء تصحيحي", CorrectiveActionsModule),
            (PermissionCodes.EscalationsView, "عرض سياسات التصعيد", EscalationsModule),
            (PermissionCodes.EscalationsManage, "إدارة سياسات التصعيد", EscalationsModule),
            (PermissionCodes.EscalationsActivate, "تفعيل وتعطيل سياسات التصعيد", EscalationsModule),
            (PermissionCodes.EscalationsRun, "تشغيل التصعيد يدويًا", EscalationsModule),
            (PermissionCodes.EscalationsViewOccurrences, "عرض حوادث التصعيد", EscalationsModule),
            (PermissionCodes.EscalationsRetryFailed, "إعادة محاولة التصعيد الفاشل", EscalationsModule),
            (PermissionCodes.NotificationsViewOwn, "عرض إشعاراتي", NotificationsModule),
            (PermissionCodes.NotificationsMarkRead, "تعليم إشعاراتي كمقروءة", NotificationsModule),
            (PermissionCodes.NotificationsArchiveOwn, "أرشفة إشعاراتي", NotificationsModule),
            (PermissionCodes.IncidentsApprove, "اعتماد واقعة", "Incidents"),
            (PermissionCodes.FormsView, "عرض النماذج", FormsModule),
            (PermissionCodes.FormsViewSensitive, "عرض النماذج الحساسة", FormsModule),
            (PermissionCodes.FormsCreate, "إنشاء نموذج", FormsModule),
            (PermissionCodes.FormsUpdateDraft, "تحديث مسودة نموذج", FormsModule),
            (PermissionCodes.FormsSubmitForReview, "إرسال نموذج للمراجعة", FormsModule),
            (PermissionCodes.FormsReview, "مراجعة نموذج", FormsModule),
            (PermissionCodes.FormsApprove, "اعتماد نموذج", FormsModule),
            (PermissionCodes.FormsReject, "رفض نموذج", FormsModule),
            (PermissionCodes.FormsRequestChanges, "طلب تعديلات على نموذج", FormsModule),
            (PermissionCodes.FormsArchive, "أرشفة نموذج", FormsModule),
            (PermissionCodes.FormsRestore, "استعادة نموذج", FormsModule),
            (PermissionCodes.FormsManageAccess, "إدارة وصول النماذج", FormsModule),
            (PermissionCodes.FormsManageGovernance, "إدارة حوكمة النماذج", FormsModule),
            (PermissionCodes.FormsManageRetention, "إدارة احتفاظ النماذج", FormsModule),
            (PermissionCodes.FormsPublish, "نشر نموذج", FormsModule),
            (PermissionCodes.FormsRespond, "الرد على نموذج", FormsModule),
            (PermissionCodes.FormsMonitorRegion, "مراقبة نماذج المنطقة", FormsModule),
            (PermissionCodes.FormsMonitorHeadquarters, "مراقبة نماذج المقر", FormsModule),
            (PermissionCodes.FormsApproveResponses, "اعتماد ردود النماذج", FormsModule),
            (PermissionCodes.FormsAnalyze, "تحليل النماذج", FormsModule),
            (PermissionCodes.FormsExport, "تصدير النماذج", FormsModule),
            (PermissionCodes.FormsCloneVersion, "استنساخ إصدار نموذج", FormsModule),
            (PermissionCodes.FormsViewVersionHistory, "عرض سجل إصدارات النموذج", FormsModule),
            (PermissionCodes.FormsManageTemplates, "إدارة قوالب النماذج", FormsModule),
            (PermissionCodes.ProjectsApprove, "اعتماد مشروع", "Projects"),
            (PermissionCodes.StrategyManage, "إدارة الاستراتيجية", "Strategy"),
            (PermissionCodes.ReportsExportSensitive, "تصدير تقارير حساسة", "Reports"),
            (PermissionCodes.DashboardViewOperational, "عرض لوحة المتابعة التشغيلية", DashboardModule),
            (PermissionCodes.DashboardViewRisk, "عرض مؤشرات المخاطر في لوحة المتابعة", DashboardModule),
            (PermissionCodes.DashboardViewRouting, "عرض مؤشرات التوجيه في لوحة المتابعة", DashboardModule),
            (PermissionCodes.DashboardViewCorrectiveActions, "عرض مؤشرات الإجراءات التصحيحية في لوحة المتابعة", DashboardModule)
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
            (RoleCodes.FormPublisher, "ناشر نماذج"),
            (RoleCodes.FormRespondent, "مستجيب نماذج"),
            (RoleCodes.FormRegionalMonitor, "مراقب نماذج إقليمي"),
            (RoleCodes.FormHeadquartersMonitor, "مراقب نماذج المقر"),
            (RoleCodes.FormApprover, "معتمد نماذج"),
            (RoleCodes.FormAnalyst, "محلل نماذج"),
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
    public static readonly Guid NoteTypeSecurity = Guid.Parse("44444444-4444-4444-4444-444444444401");
    public static readonly Guid NoteTypeTechnical = Guid.Parse("44444444-4444-4444-4444-444444444402");
    public static readonly Guid NoteTypeOperational = Guid.Parse("44444444-4444-4444-4444-444444444403");
    public static readonly Guid NoteTypeHealthSafety = Guid.Parse("44444444-4444-4444-4444-444444444404");
    public static readonly Guid NoteTypeAdministrative = Guid.Parse("44444444-4444-4444-4444-444444444405");
    public static readonly Guid NoteTypeOther = Guid.Parse("44444444-4444-4444-4444-444444444406");
    public static readonly Guid FormGovernancePolicy = Guid.Parse("55555555-5555-5555-5555-555555555501");
}
