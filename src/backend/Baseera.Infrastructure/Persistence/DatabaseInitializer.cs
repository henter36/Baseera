namespace Baseera.Infrastructure.Persistence;

using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Domain.Occupancy;
using Baseera.Domain.Organization;
using Baseera.Domain.Resources;
using Baseera.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class DatabaseInitializer
{
    private const string SecurityOfficerNameAr = "ضابط أمن";

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
        string[] workspaceHeadquarters =
        [
            PermissionCodes.WorkspacesView,
            PermissionCodes.WorkspacesViewDomain,
            PermissionCodes.WorkspacesViewHeadquarters,
            PermissionCodes.WorkspacesConfigureOwnView
        ];
        string[] workspaceRegion =
        [
            PermissionCodes.WorkspacesView,
            PermissionCodes.WorkspacesViewDomain,
            PermissionCodes.WorkspacesViewRegion,
            PermissionCodes.WorkspacesConfigureOwnView
        ];
        string[] workspaceFacility =
        [
            PermissionCodes.WorkspacesView,
            PermissionCodes.WorkspacesViewDomain,
            PermissionCodes.WorkspacesViewFacility,
            PermissionCodes.WorkspacesConfigureOwnView
        ];
        string[] occupancySummary =
        [
            PermissionCodes.OccupancyViewSummary,
            PermissionCodes.OccupancyViewUnitBreakdown,
            PermissionCodes.OccupancyViewMovements
        ];
        string[] occupancyManager =
        [
            PermissionCodes.OccupancyViewSummary,
            PermissionCodes.OccupancyViewUnitBreakdown,
            PermissionCodes.OccupancyViewMovements,
            PermissionCodes.OccupancyManageCapacity,
            PermissionCodes.OccupancyRecordSnapshot,
            PermissionCodes.OccupancyImport,
            PermissionCodes.OccupancyReconcile
        ];
        string[] occupancySensitive =
        [
            PermissionCodes.OccupancyViewSensitiveMovements,
            PermissionCodes.OccupancyExport
        ];
        string[] resourceSummary =
        [
            PermissionCodes.ResourcesViewSummary,
            PermissionCodes.ResourcesViewAssets,
            PermissionCodes.ResourcesViewVehicles,
            PermissionCodes.ResourcesViewCommunicationDevices,
            PermissionCodes.ResourcesViewEquipment,
            PermissionCodes.ResourcesViewFacilityAssets,
            PermissionCodes.ResourcesViewMaintenance,
            PermissionCodes.ResourcesViewRequirements
        ];
        string[] resourceManager =
        [
            PermissionCodes.ResourcesViewSummary,
            PermissionCodes.ResourcesViewAssets,
            PermissionCodes.ResourcesViewVehicles,
            PermissionCodes.ResourcesViewCommunicationDevices,
            PermissionCodes.ResourcesViewEquipment,
            PermissionCodes.ResourcesViewFacilityAssets,
            PermissionCodes.ResourcesManageAssets,
            PermissionCodes.ResourcesManagePlacements,
            PermissionCodes.ResourcesManageStatus,
            PermissionCodes.ResourcesViewMaintenance,
            PermissionCodes.ResourcesManageMaintenance,
            PermissionCodes.ResourcesViewRequirements,
            PermissionCodes.ResourcesManageRequirements,
            PermissionCodes.ResourcesImport,
            PermissionCodes.ResourcesReconcile
        ];
        string[] workforceSummary =
        [
            PermissionCodes.WorkforceViewSummary,
            PermissionCodes.WorkforceViewCoverage,
            PermissionCodes.WorkforceViewMembers
        ];
        string[] workforceManager =
        [
            PermissionCodes.WorkforceViewSummary,
            PermissionCodes.WorkforceViewCoverage,
            PermissionCodes.WorkforceViewMembers,
            PermissionCodes.WorkforceViewSensitiveRestrictions,
            PermissionCodes.WorkforceManageMembers,
            PermissionCodes.WorkforceManageAssignments,
            PermissionCodes.WorkforceManageQualifications,
            PermissionCodes.WorkforceManageRequirements,
            PermissionCodes.WorkforceManageRosters,
            PermissionCodes.WorkforceRecordAvailability,
            PermissionCodes.WorkforceImport,
            PermissionCodes.WorkforceExport,
            PermissionCodes.WorkforceReconcile
        ];
        string[] sensitiveCustodySummary =
        [
            PermissionCodes.SensitiveCustodyViewSummary
        ];
        string[] sensitiveCustodyViewer =
        [
            PermissionCodes.SensitiveCustodyViewSummary,
            PermissionCodes.SensitiveCustodyViewWeapons,
            PermissionCodes.SensitiveCustodyViewAmmunition,
            PermissionCodes.SensitiveCustodyViewCustodyTransactions,
            PermissionCodes.SensitiveCustodyViewDiscrepancies
        ];
        string[] sensitiveCustodyManager =
        [
            PermissionCodes.SensitiveCustodyViewSummary,
            PermissionCodes.SensitiveCustodyViewWeapons,
            PermissionCodes.SensitiveCustodyViewSerialNumbers,
            PermissionCodes.SensitiveCustodyViewArmoryLocations,
            PermissionCodes.SensitiveCustodyViewAmmunition,
            PermissionCodes.SensitiveCustodyViewCustodyTransactions,
            PermissionCodes.SensitiveCustodyManageWeapons,
            PermissionCodes.SensitiveCustodyIssueWeapons,
            PermissionCodes.SensitiveCustodyReceiveWeapons,
            PermissionCodes.SensitiveCustodyManageAmmunition,
            PermissionCodes.SensitiveCustodyConductInventory,
            PermissionCodes.SensitiveCustodyManageInspections,
            PermissionCodes.SensitiveCustodyManageMaintenance,
            PermissionCodes.SensitiveCustodyViewDiscrepancies,
            PermissionCodes.SensitiveCustodyImport,
            PermissionCodes.SensitiveCustodyReconcile
        ];
        string[] sensitiveCustodyApprover =
        [
            PermissionCodes.SensitiveCustodyApproveTransactions,
            PermissionCodes.SensitiveCustodyApproveInventory,
            PermissionCodes.SensitiveCustodyExport
        ];

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
            dashboardReadOnly,
            PermissionCodes.WorkspacesView);

        var readonlyUser = roles.First(r => r.Code == RoleCodes.ReadOnlyUser);
        Grant(readonlyUser, PermissionCodes.OrganizationView, PermissionCodes.NotesView, caViewOnly, ownNotifications, dashboardReadOnly, PermissionCodes.WorkspacesView);

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
            dashboardFull,
            workspaceHeadquarters,
            occupancySummary,
            resourceSummary,
            workforceSummary,
            sensitiveCustodySummary);

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
            dashboardFull,
            workspaceHeadquarters,
            occupancyManager,
            occupancySensitive,
            resourceManager,
            PermissionCodes.ResourcesExport,
            workforceManager,
            sensitiveCustodyViewer,
            sensitiveCustodyApprover);

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
            dashboardScoped,
            workspaceRegion,
            occupancyManager,
            resourceManager,
            sensitiveCustodySummary);

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
            ownNotifications,
            workspaceRegion,
            occupancySummary,
            resourceSummary);

        var facilityDirector = roles.First(r => r.Code == RoleCodes.FacilityDirector);
        Grant(facilityDirector,
            PermissionCodes.OrganizationView,
            PermissionCodes.AttachmentsUpload,
            PermissionCodes.AttachmentsDownload,
            PermissionCodes.FormsRespond,
            PermissionCodes.FormsViewResponses,
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
            dashboardScoped,
            workspaceFacility,
            occupancyManager,
            resourceManager,
            workforceManager,
            sensitiveCustodyViewer,
            sensitiveCustodyApprover);

        string[] formsDesigner =
        [
            PermissionCodes.FormsView,
            PermissionCodes.FormsCreate,
            PermissionCodes.FormsUpdateDraft,
            PermissionCodes.FormsSubmitForReview,
            PermissionCodes.FormsCloneVersion,
            PermissionCodes.FormsViewVersionHistory,
            PermissionCodes.FormsManageTemplates,
            PermissionCodes.FormsManageCampaigns,
            PermissionCodes.FormsPreviewTargets
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
        string[] formsPublisher =
        [
            PermissionCodes.FormsView,
            PermissionCodes.FormsPublish,
            PermissionCodes.FormsPreviewTargets,
            PermissionCodes.FormsPauseCampaign,
            PermissionCodes.FormsCancelCampaign,
            PermissionCodes.FormsViewCampaignAssignments,
            PermissionCodes.FormsManageCampaigns
        ];
        string[] formsRegionalMonitor =
        [
            PermissionCodes.FormsView,
            PermissionCodes.FormsMonitorRegion,
            PermissionCodes.FormsViewCampaignAssignments,
            PermissionCodes.FormsViewResponses,
            PermissionCodes.FormsReviewResponses,
            PermissionCodes.FormsApproveResponses,
            PermissionCodes.FormsCloseResponses,
            PermissionCodes.FormsViewComplianceDashboard
        ];
        string[] formsHqMonitor =
        [
            PermissionCodes.FormsView,
            PermissionCodes.FormsMonitorHeadquarters,
            PermissionCodes.FormsViewCampaignAssignments,
            PermissionCodes.FormsViewResponses,
            PermissionCodes.FormsReviewResponses,
            PermissionCodes.FormsApproveResponses,
            PermissionCodes.FormsCloseResponses,
            PermissionCodes.FormsViewSensitiveResponses,
            PermissionCodes.FormsViewComplianceDashboard,
            PermissionCodes.FormsExportComplianceDashboard
        ];
        string[] formsAnalyst =
        [
            PermissionCodes.FormsView,
            PermissionCodes.FormsAnalyze,
            PermissionCodes.FormsViewComplianceDashboard,
            PermissionCodes.FormsExportComplianceDashboard
        ];
        string[] formsAuditorView = [PermissionCodes.FormsView, PermissionCodes.FormsViewCampaignAssignments];

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
            PermissionCodes.FormsRespond,
            PermissionCodes.FormsViewResponses,
            PermissionCodes.AttachmentsUpload,
            PermissionCodes.AttachmentsDownload,
            ownNotifications,
            workspaceFacility,
            occupancySummary,
            resourceSummary);

        var prisonerCaseOfficer = roles.First(r => r.Code == RoleCodes.PrisonerCaseOfficer);
        Grant(prisonerCaseOfficer,
            PermissionCodes.OrganizationView,
            workspaceFacility,
            occupancyManager,
            occupancySensitive,
            ownNotifications);

        var fleetOfficer = roles.First(r => r.Code == RoleCodes.FleetOfficer);
        Grant(fleetOfficer,
            PermissionCodes.OrganizationView,
            workspaceFacility,
            resourceManager);

        var armamentOfficer = roles.First(r => r.Code == RoleCodes.ArmamentOfficer);
        Grant(armamentOfficer,
            PermissionCodes.OrganizationView,
            workspaceFacility,
            sensitiveCustodyManager,
            ownNotifications);

        var workforceOfficer = roles.First(r => r.Code == RoleCodes.WorkforceOfficer);
        Grant(workforceOfficer,
            PermissionCodes.OrganizationView,
            workspaceFacility,
            workforceManager,
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

        await EnsureDemoOccupancyAsync(db, cancellationToken);
        await EnsureDemoResourcesAsync(db, cancellationToken);
        await EnsureDemoWorkforceAsync(db, cancellationToken);
        await EnsureDevAdminAsync(db, cancellationToken);
    }

    private static async Task EnsureDemoOccupancyAsync(BaseeraDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Facilities.AnyAsync(f => f.Id == SeedIds.FacilityA1, cancellationToken))
        {
            return;
        }

        if (!await db.FacilityUnits.AnyAsync(u => u.FacilityId == SeedIds.FacilityA1, cancellationToken))
        {
            db.FacilityUnits.AddRange(
                new FacilityUnit { Id = SeedIds.FacilityA1UnitNorth, FacilityId = SeedIds.FacilityA1, Code = "A1-N", NameAr = "عنبر الشمال" },
                new FacilityUnit { Id = SeedIds.FacilityA1UnitSouth, FacilityId = SeedIds.FacilityA1, Code = "A1-S", NameAr = "عنبر الجنوب" },
                new FacilityUnit { Id = SeedIds.FacilityA1UnitMedical, FacilityId = SeedIds.FacilityA1, Code = "A1-M", NameAr = "وحدة العزل الطبي" });
            await db.SaveChangesAsync(cancellationToken);
        }

        if (await db.FacilityCapacityBaselines.AnyAsync(c => c.FacilityId == SeedIds.FacilityA1, cancellationToken))
        {
            return;
        }

        var capturedAt = DateTimeOffset.UtcNow;
        var effectiveFrom = capturedAt.AddMonths(-6);
        db.FacilityCapacityBaselines.AddRange(
            DemoCapacity(null, 300, "CAP-FAC-A1", effectiveFrom),
            DemoCapacity(SeedIds.FacilityA1UnitNorth, 120, "CAP-A1-N", effectiveFrom),
            DemoCapacity(SeedIds.FacilityA1UnitSouth, 120, "CAP-A1-S", effectiveFrom),
            DemoCapacity(SeedIds.FacilityA1UnitMedical, 24, "CAP-A1-M", effectiveFrom));
        db.InmateCensusSnapshots.AddRange(
            DemoSnapshot(null, 286, "CEN-FAC-A1-20260724", capturedAt),
            DemoSnapshot(SeedIds.FacilityA1UnitNorth, 118, "CEN-A1-N-20260724", capturedAt),
            DemoSnapshot(SeedIds.FacilityA1UnitSouth, 129, "CEN-A1-S-20260724", capturedAt),
            DemoSnapshot(SeedIds.FacilityA1UnitMedical, 21, "CEN-A1-M-20260724", capturedAt));
        db.InmateMovementEvents.AddRange(
            DemoMovement("MOV-A1-001", MovementType.Admission, null, SeedIds.FacilityA1, null, SeedIds.FacilityA1UnitNorth, capturedAt.AddHours(-8)),
            DemoMovement("MOV-A1-002", MovementType.Release, SeedIds.FacilityA1, null, SeedIds.FacilityA1UnitSouth, null, capturedAt.AddHours(-5)),
            DemoMovement("MOV-A1-003", MovementType.InternalTransfer, SeedIds.FacilityA1, SeedIds.FacilityA1, SeedIds.FacilityA1UnitNorth, SeedIds.FacilityA1UnitMedical, capturedAt.AddHours(-3)));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static FacilityCapacityBaseline DemoCapacity(Guid? unitId, int capacity, string reference, DateTimeOffset effectiveFrom) =>
        new()
        {
            OrganizationId = SeedIds.Organization,
            FacilityId = SeedIds.FacilityA1,
            FacilityUnitId = unitId,
            CapacityType = CapacityType.ApprovedOperational,
            ApprovedCapacity = capacity,
            EffectiveFromUtc = effectiveFrom,
            SourceType = OccupancySourceType.Manual,
            SourceReference = reference,
            ApprovalReference = reference,
            ApprovalDateUtc = effectiveFrom
        };

    private static InmateCensusSnapshot DemoSnapshot(Guid? unitId, int count, string reference, DateTimeOffset capturedAt) =>
        new()
        {
            OrganizationId = SeedIds.Organization,
            FacilityId = SeedIds.FacilityA1,
            FacilityUnitId = unitId,
            CapturedAtUtc = capturedAt,
            InmateCount = count,
            SourceType = OccupancySourceType.Manual,
            SourceReference = reference,
            IsAuthoritative = true,
            QualityStatus = CensusQualityStatus.Complete
        };

    private static InmateMovementEvent DemoMovement(
        string externalEventId,
        MovementType type,
        Guid? fromFacilityId,
        Guid? toFacilityId,
        Guid? fromUnitId,
        Guid? toUnitId,
        DateTimeOffset occurredAt) =>
        new()
        {
            OrganizationId = SeedIds.Organization,
            FacilityId = SeedIds.FacilityA1,
            InmateReferenceHash = $"demo-hash-{externalEventId}",
            MovementType = type,
            FromFacilityId = fromFacilityId,
            ToFacilityId = toFacilityId,
            FromFacilityUnitId = fromUnitId,
            ToFacilityUnitId = toUnitId,
            OccurredAtUtc = occurredAt,
            RecordedAtUtc = occurredAt.AddMinutes(5),
            SourceType = OccupancySourceType.Import,
            SourceReference = "demo-census",
            ExternalEventId = externalEventId
        };

    private static async Task EnsureDemoResourcesAsync(BaseeraDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Facilities.AnyAsync(f => f.Id == SeedIds.FacilityA1, cancellationToken))
        {
            return;
        }

        if (await db.ResourceAssets.AnyAsync(asset => asset.OperationalFacilityId == SeedIds.FacilityA1, cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var verifiedAt = now.AddHours(-4);
        var staleVerifiedAt = now.AddDays(-45);

        var patrolVehicle = DemoResourceAsset(new DemoResourceAssetSeed(
            ResourceType.Vehicle,
            "VEH-A1-001",
            "دورية نقل داخلية",
            SeedIds.FacilityA1UnitNorth,
            new DemoResourceAssetState(ResourceStatus.InUse, ResourceCondition.Good, ResourceCriticality.High, verifiedAt)));
        var transportVehicle = DemoResourceAsset(new DemoResourceAssetSeed(
            ResourceType.Vehicle,
            "VEH-A1-002",
            "حافلة نقل نزلاء",
            SeedIds.FacilityA1UnitSouth,
            new DemoResourceAssetState(ResourceStatus.UnderMaintenance, ResourceCondition.Fair, ResourceCriticality.MissionCritical, verifiedAt)));
        var radioSet = DemoResourceAsset(new DemoResourceAssetSeed(
            ResourceType.CommunicationDevice,
            "COM-A1-010",
            "جهاز اتصال مناوبة",
            SeedIds.FacilityA1UnitNorth,
            new DemoResourceAssetState(ResourceStatus.Available, ResourceCondition.Good, ResourceCriticality.High, verifiedAt)));
        var screeningGate = DemoResourceAsset(new DemoResourceAssetSeed(
            ResourceType.SecurityEquipment,
            "SEC-A1-020",
            "بوابة تفتيش إلكترونية",
            SeedIds.FacilityA1UnitSouth,
            new DemoResourceAssetState(ResourceStatus.AwaitingParts, ResourceCondition.Poor, ResourceCriticality.MissionCritical, verifiedAt)));
        var kitchenEquipment = DemoResourceAsset(new DemoResourceAssetSeed(
            ResourceType.OperationalEquipment,
            "OPE-A1-030",
            "معدات مطبخ تشغيلية",
            null,
            new DemoResourceAssetState(ResourceStatus.Unknown, ResourceCondition.Unknown, ResourceCriticality.Medium, null)));
        var generator = DemoResourceAsset(new DemoResourceAssetSeed(
            ResourceType.FacilityAsset,
            "FAC-A1-040",
            "مولد احتياطي رئيسي",
            SeedIds.FacilityA1UnitMedical,
            new DemoResourceAssetState(ResourceStatus.Standby, ResourceCondition.Good, ResourceCriticality.MissionCritical, staleVerifiedAt)));

        db.ResourceAssets.AddRange(patrolVehicle, transportVehicle, radioSet, screeningGate, kitchenEquipment, generator);
        db.VehicleProfiles.AddRange(
            new VehicleProfile
            {
                ResourceAssetId = patrolVehicle.Id,
                PlateNumber = "د و ر 101",
                VehicleCategory = VehicleCategory.Patrol,
                FuelType = FuelType.Gasoline,
                Odometer = 48210,
                OdometerRecordedAtUtc = verifiedAt,
                RegistrationExpiresAtUtc = now.AddMonths(8),
                InsuranceExpiresAtUtc = now.AddMonths(6),
                InspectionExpiresAtUtc = now.AddMonths(2),
                TrackerInstalled = true,
                OperationalRole = "دوريات داخلية",
                PassengerCapacity = 5
            },
            new VehicleProfile
            {
                ResourceAssetId = transportVehicle.Id,
                PlateNumber = "ن ز ل 220",
                VehicleCategory = VehicleCategory.PrisonerTransport,
                FuelType = FuelType.Diesel,
                Odometer = 118400,
                OdometerRecordedAtUtc = verifiedAt,
                RegistrationExpiresAtUtc = now.AddMonths(3),
                InsuranceExpiresAtUtc = now.AddMonths(2),
                InspectionExpiresAtUtc = now.AddDays(-5),
                TrackerInstalled = true,
                OperationalRole = "نقل نزلاء",
                PassengerCapacity = 24,
                PrisonerTransportCapacity = 16
            });
        db.CommunicationDeviceProfiles.Add(new CommunicationDeviceProfile
        {
            ResourceAssetId = radioSet.Id,
            DeviceCategory = CommunicationDeviceCategory.HandheldRadio,
            NetworkType = "TETRA",
            CallSign = "A1-N-10",
            FrequencyGroup = "Operations",
            BatteryCondition = "جيدة",
            CoverageStatus = "مغطى",
            EncryptionCapability = true,
            AssignedUnitId = SeedIds.FacilityA1UnitNorth
        });
        db.EquipmentProfiles.AddRange(
            new EquipmentProfile
            {
                ResourceAssetId = screeningGate.Id,
                EquipmentCategory = EquipmentCategory.Screening,
                Specification = "بوابة تفتيش ثابتة",
                CalibrationRequired = true,
                CalibrationDueAtUtc = now.AddDays(-3),
                InspectionRequired = true,
                InspectionDueAtUtc = now.AddDays(-1),
                Portable = false,
                SafetyCritical = true
            },
            new EquipmentProfile
            {
                ResourceAssetId = kitchenEquipment.Id,
                EquipmentCategory = EquipmentCategory.Kitchen,
                Specification = "خط تجهيز وجبات",
                QuantityUnit = "مجموعة",
                CalibrationRequired = false,
                InspectionRequired = true,
                InspectionDueAtUtc = now.AddDays(20),
                Portable = false,
                SafetyCritical = false
            });
        db.FacilityAssetProfiles.Add(new FacilityAssetProfile
        {
            ResourceAssetId = generator.Id,
            AssetCategory = FacilityAssetCategory.Generator,
            FacilityUnitId = SeedIds.FacilityA1UnitMedical,
            InstalledAtLocation = "غرفة الخدمات الطبية",
            FixedAsset = true,
            CapacityValue = 250,
            CapacityUnit = "kVA",
            RequiresPeriodicInspection = true,
            InspectionDueAtUtc = now.AddDays(12)
        });

        var demoAssets = new[] { patrolVehicle, transportVehicle, radioSet, screeningGate, kitchenEquipment, generator };
        db.ResourceStatusEvents.AddRange(demoAssets.Select(asset => new ResourceStatusEvent
        {
            ResourceAssetId = asset.Id,
            PreviousStatus = null,
            NewStatus = asset.CurrentStatus,
            OccurredAtUtc = asset.LastVerifiedAtUtc ?? now,
            Reason = "بيانات تطوير أولية لمركز الموارد",
            SourceType = ResourceSourceType.Manual,
            SourceReference = "demo-resource-seed",
            RecordedAtUtc = now
        }));
        db.ResourcePlacements.AddRange(demoAssets.Select(asset => new ResourcePlacement
        {
            ResourceAssetId = asset.Id,
            OwnershipOrganizationId = SeedIds.Organization,
            OperationalFacilityId = SeedIds.FacilityA1,
            OperationalFacilityUnitId = asset.OperationalFacilityUnitId,
            EffectiveFromUtc = now.AddMonths(-3),
            AssignmentType = ResourceAssignmentType.Permanent,
            SourceReference = "demo-resource-seed",
            Reason = "موضع تطوير افتتاحي"
        }));
        db.MaintenanceWorkOrders.AddRange(
            new MaintenanceWorkOrder
            {
                OrganizationId = SeedIds.Organization,
                ResourceAssetId = transportVehicle.Id,
                WorkOrderNumber = "MWO-DEMO-0001",
                MaintenanceType = MaintenanceType.Corrective,
                Priority = MaintenancePriority.Critical,
                Status = MaintenanceStatus.InProgress,
                ReportedAtUtc = now.AddDays(-2),
                ProblemDescription = "فحص فني عاجل لحافلة النقل بعد تعطل متكرر.",
                ExpectedCompletionAtUtc = now.AddHours(-6),
                PartsRequired = false
            },
            new MaintenanceWorkOrder
            {
                OrganizationId = SeedIds.Organization,
                ResourceAssetId = screeningGate.Id,
                WorkOrderNumber = "MWO-DEMO-0002",
                MaintenanceType = MaintenanceType.Corrective,
                Priority = MaintenancePriority.High,
                Status = MaintenanceStatus.AwaitingParts,
                ReportedAtUtc = now.AddDays(-5),
                ProblemDescription = "تعطل حساس البوابة ويتطلب قطعة بديلة.",
                ExpectedCompletionAtUtc = now.AddDays(-1),
                PartsRequired = true,
                WaitingForPartsSinceUtc = now.AddDays(-4)
            });
        db.ResourceRequirements.AddRange(
            DemoRequirement(ResourceType.Vehicle, "PrisonerTransport", 3, 2, now.AddMonths(-6)),
            DemoRequirement(ResourceType.CommunicationDevice, "HandheldRadio", 8, 6, now.AddMonths(-6)),
            DemoRequirement(ResourceType.OperationalEquipment, "Kitchen", 2, 1, now.AddMonths(-6)),
            DemoRequirement(ResourceType.SecurityEquipment, "Screening", 2, 2, now.AddMonths(-6)),
            DemoRequirement(ResourceType.FacilityAsset, "Generator", 2, 1, now.AddMonths(-6)));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureDemoWorkforceAsync(BaseeraDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Facilities.AnyAsync(f => f.Id == SeedIds.FacilityA1, cancellationToken))
        {
            return;
        }

        if (await db.WorkforceMembers.AnyAsync(m => m.CurrentOperationalFacilityId == SeedIds.FacilityA1, cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var effectiveFrom = now.AddMonths(-6);

        var shiftCommander = new WorkforceRoleDefinition
        {
            OrganizationId = SeedIds.Organization,
            Code = "ShiftCommander",
            NameAr = "قائد الوردية",
            NameEn = "Shift Commander",
            Category = WorkforceRoleCategory.Command,
            Criticality = WorkforceRoleCriticality.MissionCritical,
            RequiresCertification = true,
            IsShiftBased = true,
            IsSensitive = true
        };
        var controlOperator = new WorkforceRoleDefinition
        {
            OrganizationId = SeedIds.Organization,
            Code = "ControlRoomOperator",
            NameAr = "مشغل غرفة التحكم",
            NameEn = "Control Room Operator",
            Category = WorkforceRoleCategory.Control,
            Criticality = WorkforceRoleCriticality.High,
            RequiresCertification = true,
            IsShiftBased = true
        };
        var securityOfficer = new WorkforceRoleDefinition
        {
            OrganizationId = SeedIds.Organization,
            Code = "SecurityOfficer",
            NameAr = SecurityOfficerNameAr,
            NameEn = "Security Officer",
            Category = WorkforceRoleCategory.Security,
            Criticality = WorkforceRoleCriticality.High,
            IsShiftBased = true
        };
        var escortOfficer = new WorkforceRoleDefinition
        {
            OrganizationId = SeedIds.Organization,
            Code = "EscortOfficer",
            NameAr = "ضابط مرافقة",
            NameEn = "Escort Officer",
            Category = WorkforceRoleCategory.Escort,
            Criticality = WorkforceRoleCriticality.Medium,
            IsShiftBased = true
        };
        var vehicleDriver = new WorkforceRoleDefinition
        {
            OrganizationId = SeedIds.Organization,
            Code = "VehicleDriver",
            NameAr = "سائق مركبة",
            NameEn = "Vehicle Driver",
            Category = WorkforceRoleCategory.Logistics,
            Criticality = WorkforceRoleCriticality.Medium,
            RequiresCertification = true,
            IsShiftBased = true
        };
        db.WorkforceRoleDefinitions.AddRange(shiftCommander, controlOperator, securityOfficer, escortOfficer, vehicleDriver);

        var morning = new ShiftDefinition
        {
            OrganizationId = SeedIds.Organization,
            FacilityId = SeedIds.FacilityA1,
            Code = "MORNING",
            Name = "الوردية الصباحية",
            StartLocalTime = new TimeOnly(6, 0),
            EndLocalTime = new TimeOnly(18, 0),
            CrossesMidnight = false,
            Timezone = "Asia/Riyadh",
            IsActive = true
        };
        var night = new ShiftDefinition
        {
            OrganizationId = SeedIds.Organization,
            FacilityId = SeedIds.FacilityA1,
            Code = "NIGHT",
            Name = "الوردية الليلية",
            StartLocalTime = new TimeOnly(18, 0),
            EndLocalTime = new TimeOnly(6, 0),
            CrossesMidnight = true,
            Timezone = "Asia/Riyadh",
            IsActive = true
        };
        db.ShiftDefinitions.AddRange(morning, night);

        WorkforceMember DemoMember(string number, string name, string title, string specialty, Guid? unitId) =>
            new()
            {
                OrganizationId = SeedIds.Organization,
                DisplayName = name,
                EmployeeNumber = number,
                EmploymentStatus = EmploymentStatus.Active,
                JobTitle = title,
                PrimarySpecialty = specialty,
                AdministrativeOrganizationId = SeedIds.Organization,
                HomeFacilityId = SeedIds.FacilityA1,
                CurrentOperationalFacilityId = SeedIds.FacilityA1,
                CurrentOperationalUnitId = unitId,
                IsOperational = true,
                SourceType = WorkforceSourceType.Manual,
                SourceReference = "demo-workforce-seed",
                LastVerifiedAtUtc = now.AddDays(-2)
            };

        var m1 = DemoMember("WF-A1-001", "أحمد القحطاني", "قائد وردية", "قيادة", SeedIds.FacilityA1UnitNorth);
        var m2 = DemoMember("WF-A1-002", "سعد العتيبي", "مشغل تحكم", "تحكم", SeedIds.FacilityA1UnitNorth);
        var m3 = DemoMember("WF-A1-003", "فهد الشمري", SecurityOfficerNameAr, "أمن", SeedIds.FacilityA1UnitSouth);
        var m4 = DemoMember("WF-A1-004", "خالد الدوسري", "ضابط مرافقة", "مرافقة", SeedIds.FacilityA1UnitSouth);
        var m5 = DemoMember("WF-A1-005", "ناصر الحربي", "سائق", "نقل", SeedIds.FacilityA1UnitMedical);
        var m6 = DemoMember("WF-A1-006", "عبدالله المطيري", SecurityOfficerNameAr, "أمن", null);
        db.WorkforceMembers.AddRange(m1, m2, m3, m4, m5, m6);

        db.WorkforceAssignments.AddRange(
            new WorkforceAssignment
            {
                WorkforceMemberId = m1.Id,
                FacilityId = SeedIds.FacilityA1,
                FacilityUnitId = SeedIds.FacilityA1UnitNorth,
                RoleDefinitionId = shiftCommander.Id,
                AssignmentType = AssignmentType.Permanent,
                EffectiveFromUtc = effectiveFrom,
                IsPrimary = true
            },
            new WorkforceAssignment
            {
                WorkforceMemberId = m2.Id,
                FacilityId = SeedIds.FacilityA1,
                FacilityUnitId = SeedIds.FacilityA1UnitNorth,
                RoleDefinitionId = controlOperator.Id,
                AssignmentType = AssignmentType.Permanent,
                EffectiveFromUtc = effectiveFrom,
                IsPrimary = true
            },
            new WorkforceAssignment
            {
                WorkforceMemberId = m3.Id,
                FacilityId = SeedIds.FacilityA1,
                FacilityUnitId = SeedIds.FacilityA1UnitSouth,
                RoleDefinitionId = securityOfficer.Id,
                AssignmentType = AssignmentType.Permanent,
                EffectiveFromUtc = effectiveFrom,
                IsPrimary = true
            },
            new WorkforceAssignment
            {
                WorkforceMemberId = m4.Id,
                FacilityId = SeedIds.FacilityA1,
                RoleDefinitionId = escortOfficer.Id,
                AssignmentType = AssignmentType.Permanent,
                EffectiveFromUtc = effectiveFrom,
                IsPrimary = true
            },
            new WorkforceAssignment
            {
                WorkforceMemberId = m5.Id,
                FacilityId = SeedIds.FacilityA1,
                RoleDefinitionId = vehicleDriver.Id,
                AssignmentType = AssignmentType.Permanent,
                EffectiveFromUtc = effectiveFrom,
                IsPrimary = true
            });

        db.StaffingRequirements.AddRange(
            new StaffingRequirement
            {
                OrganizationId = SeedIds.Organization,
                FacilityId = SeedIds.FacilityA1,
                RoleDefinitionId = shiftCommander.Id,
                ShiftDefinitionId = morning.Id,
                RequiredHeadcount = 1,
                MinimumSafeHeadcount = 1,
                EffectiveFromUtc = effectiveFrom,
                SourceReference = "STAFF-A1-CMD"
            },
            new StaffingRequirement
            {
                OrganizationId = SeedIds.Organization,
                FacilityId = SeedIds.FacilityA1,
                RoleDefinitionId = controlOperator.Id,
                ShiftDefinitionId = morning.Id,
                RequiredHeadcount = 2,
                MinimumSafeHeadcount = 1,
                EffectiveFromUtc = effectiveFrom,
                SourceReference = "STAFF-A1-CTRL"
            },
            new StaffingRequirement
            {
                OrganizationId = SeedIds.Organization,
                FacilityId = SeedIds.FacilityA1,
                RoleDefinitionId = securityOfficer.Id,
                ShiftDefinitionId = morning.Id,
                RequiredHeadcount = 4,
                MinimumSafeHeadcount = 3,
                EffectiveFromUtc = effectiveFrom,
                SourceReference = "STAFF-A1-SEC"
            });

        db.CriticalPositionRequirements.Add(new CriticalPositionRequirement
        {
            FacilityId = SeedIds.FacilityA1,
            RoleDefinitionId = shiftCommander.Id,
            ShiftDefinitionId = morning.Id,
            RequiredPrimaryCount = 1,
            RequiredAlternateCount = 1,
            Criticality = WorkforceRoleCriticality.MissionCritical,
            EffectiveFromUtc = effectiveFrom
        });

        db.WorkforceQualifications.AddRange(
            new WorkforceQualification
            {
                WorkforceMemberId = m1.Id,
                QualificationType = QualificationType.RoleCertification,
                RoleDefinitionId = shiftCommander.Id,
                Name = "شهادة قيادة وردية",
                Status = QualificationStatus.Valid,
                IssuedAtUtc = now.AddYears(-1),
                ExpiresAtUtc = now.AddYears(1)
            },
            new WorkforceQualification
            {
                WorkforceMemberId = m5.Id,
                QualificationType = QualificationType.License,
                RoleDefinitionId = vehicleDriver.Id,
                Name = "رخصة قيادة مهنية",
                Status = QualificationStatus.ExpiringSoon,
                IssuedAtUtc = now.AddYears(-2),
                ExpiresAtUtc = now.AddDays(20)
            });

        db.WorkforceAvailabilityEvents.Add(new WorkforceAvailabilityEvent
        {
            WorkforceMemberId = m6.Id,
            AvailabilityType = AvailabilityType.AnnualLeave,
            StartsAtUtc = now.AddDays(-1),
            EndsAtUtc = now.AddDays(5),
            AffectsOperationalAvailability = true,
            SourceType = WorkforceSourceType.Manual,
            RecordedAtUtc = now,
            RecordedBy = "seed"
        });

        var roster = new DutyRoster
        {
            FacilityId = SeedIds.FacilityA1,
            ShiftDefinitionId = morning.Id,
            DutyDate = today,
            Status = DutyRosterStatuses.Published,
            PublishedAtUtc = now.AddHours(-2),
            PublishedBy = "seed"
        };
        db.DutyRosters.Add(roster);
        db.DutyRosterAssignments.AddRange(
            new DutyRosterAssignment
            {
                DutyRosterId = roster.Id,
                WorkforceMemberId = m1.Id,
                RoleDefinitionId = shiftCommander.Id,
                Status = RosterAssignmentStatus.Present
            },
            new DutyRosterAssignment
            {
                DutyRosterId = roster.Id,
                WorkforceMemberId = m2.Id,
                RoleDefinitionId = controlOperator.Id,
                Status = RosterAssignmentStatus.Confirmed
            },
            new DutyRosterAssignment
            {
                DutyRosterId = roster.Id,
                WorkforceMemberId = m3.Id,
                RoleDefinitionId = securityOfficer.Id,
                Status = RosterAssignmentStatus.Planned
            });

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record DemoResourceAssetState(
        ResourceStatus Status,
        ResourceCondition Condition,
        ResourceCriticality Criticality,
        DateTimeOffset? VerifiedAt);

    private sealed record DemoResourceAssetSeed(
        ResourceType Type,
        string Code,
        string Name,
        Guid? UnitId,
        DemoResourceAssetState State);

    private static ResourceAsset DemoResourceAsset(DemoResourceAssetSeed seed) =>
        new()
        {
            OrganizationId = SeedIds.Organization,
            ResourceType = seed.Type,
            AssetCode = seed.Code,
            DisplayName = seed.Name,
            OwnershipOrganizationId = SeedIds.Organization,
            OperationalFacilityId = SeedIds.FacilityA1,
            OperationalFacilityUnitId = seed.UnitId,
            CurrentStatus = seed.State.Status,
            Condition = seed.State.Condition,
            Criticality = seed.State.Criticality,
            SourceType = ResourceSourceType.Manual,
            SourceReference = "demo-resource-seed",
            LastVerifiedAtUtc = seed.State.VerifiedAt,
            LastVerifiedBy = "seed"
        };

    private static ResourceRequirement DemoRequirement(
        ResourceType resourceType,
        string category,
        int required,
        int minimum,
        DateTimeOffset effectiveFrom) =>
        new()
        {
            OrganizationId = SeedIds.Organization,
            FacilityId = SeedIds.FacilityA1,
            ResourceType = resourceType,
            ResourceCategory = category,
            RequiredQuantity = required,
            MinimumOperationalQuantity = minimum,
            EffectiveFromUtc = effectiveFrom,
            SourceReference = "demo-resource-baseline",
            ApprovalReference = "demo-resource-baseline"
        };

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
    private const string IdentityModule = "Identity";
    private const string VehiclesModule = "Vehicles";
    private const string ArmamentModule = "Armament";
    private const string SensitiveCustodyModule = "SensitiveCustody";
    private const string NotesModule = "Notes";
    private const string CorrectiveActionsModule = "CorrectiveActions";
    private const string EscalationsModule = "Escalations";
    private const string NotificationsModule = "Notifications";
    private const string DashboardModule = "Dashboard";
    private const string WorkspacesModule = "Workspaces";
    private const string OccupancyModule = "Occupancy";
    private const string ResourcesModule = "Resources";
    private const string WorkforceModule = "Workforce";
    private const string FormsModule = "Forms";

    private static List<Permission> BuildPermissions()
    {
        (string Code, string NameAr, string Module)[] items =
        [
            (PermissionCodes.OrganizationView, "عرض الهيكل التنظيمي", OrganizationModule),
            (PermissionCodes.OrganizationManage, "إدارة الهيكل التنظيمي", OrganizationModule),
            (PermissionCodes.UsersView, "عرض المستخدمين", IdentityModule),
            (PermissionCodes.UsersManage, "إدارة المستخدمين", IdentityModule),
            (PermissionCodes.RolesManage, "إدارة الأدوار", IdentityModule),
            (PermissionCodes.ScopesManage, "إدارة النطاقات", IdentityModule),
            (PermissionCodes.AuditView, "عرض سجل التدقيق", "Audit"),
            (PermissionCodes.AttachmentsUpload, "رفع المرفقات", "Attachments"),
            (PermissionCodes.AttachmentsDownload, "تنزيل المرفقات", "Attachments"),
            (PermissionCodes.AttachmentsDownloadSensitive, "تنزيل المرفقات الحساسة", "Attachments"),
            (PermissionCodes.UsersArchive, "أرشفة مستخدم", IdentityModule),
            (PermissionCodes.UsersRestore, "استعادة مستخدم", IdentityModule),
            (PermissionCodes.OrganizationArchive, "أرشفة تنظيمي", OrganizationModule),
            (PermissionCodes.OrganizationRestore, "استعادة تنظيمي", OrganizationModule),
            (PermissionCodes.GrantGlobalScope, "منح نطاق وطني", IdentityModule),
            (PermissionCodes.GrantHeadquartersScope, "منح نطاق المستوى الرئيسي", IdentityModule),
            (PermissionCodes.VehiclesView, "عرض المركبات", VehiclesModule),
            (PermissionCodes.VehiclesCreate, "إضافة مركبة", VehiclesModule),
            (PermissionCodes.VehiclesUpdate, "تحديث مركبة", VehiclesModule),
            (PermissionCodes.VehiclesTransfer, "نقل مركبة", VehiclesModule),
            (PermissionCodes.VehiclesDecommission, "استبعاد مركبة", VehiclesModule),
            (PermissionCodes.ArmamentView, "عرض التسليح", ArmamentModule),
            (PermissionCodes.ArmamentIssue, "صرف تسليح", ArmamentModule),
            (PermissionCodes.ArmamentReceive, "استلام تسليح", ArmamentModule),
            (PermissionCodes.ArmamentInventory, "جرد تسليح", ArmamentModule),
            (PermissionCodes.ArmamentAdjust, "تسوية تسليح", ArmamentModule),
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
            (PermissionCodes.FormsViewResponses, "عرض ردود النماذج", FormsModule),
            (PermissionCodes.FormsReviewResponses, "مراجعة ردود النماذج", FormsModule),
            (PermissionCodes.FormsCloseResponses, "إغلاق ردود النماذج", FormsModule),
            (PermissionCodes.FormsViewSensitiveResponses, "عرض الردود الحساسة", FormsModule),
            (PermissionCodes.FormsMonitorRegion, "مراقبة نماذج المنطقة", FormsModule),
            (PermissionCodes.FormsMonitorHeadquarters, "مراقبة نماذج المقر", FormsModule),
            (PermissionCodes.FormsApproveResponses, "اعتماد ردود النماذج", FormsModule),
            (PermissionCodes.FormsAnalyze, "تحليل النماذج", FormsModule),
            (PermissionCodes.FormsExport, "تصدير النماذج", FormsModule),
            (PermissionCodes.FormsViewComplianceDashboard, "عرض لوحة التزام النماذج", FormsModule),
            (PermissionCodes.FormsExportComplianceDashboard, "تصدير لوحة التزام النماذج", FormsModule),
            (PermissionCodes.FormsCloneVersion, "استنساخ إصدار نموذج", FormsModule),
            (PermissionCodes.FormsViewVersionHistory, "عرض سجل إصدارات النموذج", FormsModule),
            (PermissionCodes.FormsManageTemplates, "إدارة قوالب النماذج", FormsModule),
            (PermissionCodes.FormsManageCampaigns, "إدارة حملات النماذج", FormsModule),
            (PermissionCodes.FormsPreviewTargets, "معاينة استهداف حملات النماذج", FormsModule),
            (PermissionCodes.FormsPauseCampaign, "إيقاف/استئناف حملات النماذج", FormsModule),
            (PermissionCodes.FormsCancelCampaign, "إلغاء حملات النماذج", FormsModule),
            (PermissionCodes.FormsViewCampaignAssignments, "عرض تعيينات دورات النماذج", FormsModule),
            (PermissionCodes.ProjectsApprove, "اعتماد مشروع", "Projects"),
            (PermissionCodes.StrategyManage, "إدارة الاستراتيجية", "Strategy"),
            (PermissionCodes.ReportsExportSensitive, "تصدير تقارير حساسة", "Reports"),
            (PermissionCodes.DashboardViewOperational, "عرض لوحة المتابعة التشغيلية", DashboardModule),
            (PermissionCodes.DashboardViewRisk, "عرض مؤشرات المخاطر في لوحة المتابعة", DashboardModule),
            (PermissionCodes.DashboardViewRouting, "عرض مؤشرات التوجيه في لوحة المتابعة", DashboardModule),
            (PermissionCodes.DashboardViewCorrectiveActions, "عرض مؤشرات الإجراءات التصحيحية في لوحة المتابعة", DashboardModule),
            (PermissionCodes.WorkspacesView, "عرض مساحات العمل", WorkspacesModule),
            (PermissionCodes.WorkspacesViewDomain, "عرض مساحة عمل نطاق تخصصي", WorkspacesModule),
            (PermissionCodes.WorkspacesViewFacility, "عرض مساحة عمل المنشأة", WorkspacesModule),
            (PermissionCodes.WorkspacesViewRegion, "عرض مساحة عمل المنطقة", WorkspacesModule),
            (PermissionCodes.WorkspacesViewHeadquarters, "عرض مساحة عمل المركز", WorkspacesModule),
            (PermissionCodes.WorkspacesConfigureOwnView, "تخصيص العرض الشخصي لمساحة العمل", WorkspacesModule),
            (PermissionCodes.OccupancyViewSummary, "عرض ملخص الإشغال", OccupancyModule),
            (PermissionCodes.OccupancyViewUnitBreakdown, "عرض تفصيل إشغال الوحدات", OccupancyModule),
            (PermissionCodes.OccupancyViewMovements, "عرض ملخص حركة النزلاء", OccupancyModule),
            (PermissionCodes.OccupancyViewSensitiveMovements, "عرض تفاصيل حركة نزلاء حساسة", OccupancyModule),
            (PermissionCodes.OccupancyManageCapacity, "إدارة الطاقة الاستيعابية", OccupancyModule),
            (PermissionCodes.OccupancyRecordSnapshot, "تسجيل Snapshot إشغال", OccupancyModule),
            (PermissionCodes.OccupancyImport, "استيراد بيانات إشغال وحركة", OccupancyModule),
            (PermissionCodes.OccupancyExport, "تصدير بيانات الإشغال", OccupancyModule),
            (PermissionCodes.OccupancyReconcile, "مصالحة بيانات الإشغال", OccupancyModule),
            (PermissionCodes.ResourcesViewSummary, "عرض ملخص جاهزية الموارد", ResourcesModule),
            (PermissionCodes.ResourcesViewAssets, "عرض سجلات الموارد", ResourcesModule),
            (PermissionCodes.ResourcesViewVehicles, "عرض المركبات", ResourcesModule),
            (PermissionCodes.ResourcesViewCommunicationDevices, "عرض أجهزة الاتصال", ResourcesModule),
            (PermissionCodes.ResourcesViewEquipment, "عرض المعدات", ResourcesModule),
            (PermissionCodes.ResourcesViewFacilityAssets, "عرض المرافق والأصول الثابتة", ResourcesModule),
            (PermissionCodes.ResourcesManageAssets, "إدارة تعريف الموارد", ResourcesModule),
            (PermissionCodes.ResourcesManagePlacements, "إدارة مواقع الموارد", ResourcesModule),
            (PermissionCodes.ResourcesManageStatus, "إدارة حالات الموارد", ResourcesModule),
            (PermissionCodes.ResourcesViewMaintenance, "عرض صيانة الموارد", ResourcesModule),
            (PermissionCodes.ResourcesManageMaintenance, "إدارة صيانة الموارد", ResourcesModule),
            (PermissionCodes.ResourcesViewRequirements, "عرض احتياجات الموارد", ResourcesModule),
            (PermissionCodes.ResourcesManageRequirements, "إدارة احتياجات الموارد", ResourcesModule),
            (PermissionCodes.ResourcesImport, "استيراد الموارد", ResourcesModule),
            (PermissionCodes.ResourcesExport, "تصدير الموارد", ResourcesModule),
            (PermissionCodes.ResourcesReconcile, "مصالحة بيانات الموارد", ResourcesModule),
            (PermissionCodes.WorkforceViewSummary, "عرض ملخص جاهزية القوى البشرية", WorkforceModule),
            (PermissionCodes.WorkforceViewCoverage, "عرض تغطية المناوبات والاحتياج", WorkforceModule),
            (PermissionCodes.WorkforceViewMembers, "عرض أعضاء القوى البشرية", WorkforceModule),
            (PermissionCodes.WorkforceViewSensitiveRestrictions, "عرض قيود تشغيلية حساسة", WorkforceModule),
            (PermissionCodes.WorkforceManageMembers, "إدارة أعضاء القوى البشرية", WorkforceModule),
            (PermissionCodes.WorkforceManageAssignments, "إدارة تكليفات القوى البشرية", WorkforceModule),
            (PermissionCodes.WorkforceManageQualifications, "إدارة مؤهلات القوى البشرية", WorkforceModule),
            (PermissionCodes.WorkforceManageRequirements, "إدارة احتياجات التغطية", WorkforceModule),
            (PermissionCodes.WorkforceManageRosters, "إدارة جداول المناوبات", WorkforceModule),
            (PermissionCodes.WorkforceRecordAvailability, "تسجيل التوفر والغياب", WorkforceModule),
            (PermissionCodes.WorkforceImport, "استيراد القوى البشرية", WorkforceModule),
            (PermissionCodes.WorkforceExport, "تصدير القوى البشرية", WorkforceModule),
            (PermissionCodes.WorkforceReconcile, "مصالحة بيانات القوى البشرية", WorkforceModule),
            (PermissionCodes.SensitiveCustodyViewSummary, "عرض ملخص الأسلحة والعهد الحساسة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyViewWeapons, "عرض سجلات الأسلحة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyViewSerialNumbers, "عرض أرقام السجلات الحساسة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyViewArmoryLocations, "عرض مواقع الخزن الحساسة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyViewAmmunition, "عرض الذخيرة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyViewCustodyTransactions, "عرض سلسلة العهدة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyManageWeapons, "إدارة الأسلحة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyIssueWeapons, "إصدار عهد الأسلحة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyReceiveWeapons, "استلام عهد الأسلحة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyApproveTransactions, "اعتماد عمليات العهدة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyManageAmmunition, "إدارة الذخيرة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyConductInventory, "تنفيذ الجرد الحساس", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyApproveInventory, "اعتماد الجرد الحساس", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyManageInspections, "إدارة فحص الأسلحة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyManageMaintenance, "إدارة صيانة الأسلحة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyViewDiscrepancies, "عرض فروقات الجرد الحساس", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyExport, "تصدير بيانات العهد الحساسة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyImport, "استيراد بيانات العهد الحساسة", SensitiveCustodyModule),
            (PermissionCodes.SensitiveCustodyReconcile, "مصالحة بيانات العهد الحساسة", SensitiveCustodyModule)
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
            (RoleCodes.SecurityOfficer, SecurityOfficerNameAr),
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
    public static readonly Guid FacilityA1UnitNorth = Guid.Parse("33333333-3333-3333-3333-333333333311");
    public static readonly Guid FacilityA1UnitSouth = Guid.Parse("33333333-3333-3333-3333-333333333312");
    public static readonly Guid FacilityA1UnitMedical = Guid.Parse("33333333-3333-3333-3333-333333333313");
    public static readonly Guid NoteTypeSecurity = Guid.Parse("44444444-4444-4444-4444-444444444401");
    public static readonly Guid NoteTypeTechnical = Guid.Parse("44444444-4444-4444-4444-444444444402");
    public static readonly Guid NoteTypeOperational = Guid.Parse("44444444-4444-4444-4444-444444444403");
    public static readonly Guid NoteTypeHealthSafety = Guid.Parse("44444444-4444-4444-4444-444444444404");
    public static readonly Guid NoteTypeAdministrative = Guid.Parse("44444444-4444-4444-4444-444444444405");
    public static readonly Guid NoteTypeOther = Guid.Parse("44444444-4444-4444-4444-444444444406");
    public static readonly Guid FormGovernancePolicy = Guid.Parse("55555555-5555-5555-5555-555555555501");
}
