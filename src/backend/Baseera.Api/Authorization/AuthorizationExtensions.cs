namespace Baseera.Api.Authorization;

using Baseera.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

public static class AuthPolicies
{
    public const string PermissionPrefix = "perm:";

    public const string OrganizationView = PermissionPrefix + PermissionCodes.OrganizationView;
    public const string OrganizationManage = PermissionPrefix + PermissionCodes.OrganizationManage;
    public const string UsersView = PermissionPrefix + PermissionCodes.UsersView;
    public const string RolesManage = PermissionPrefix + PermissionCodes.RolesManage;
    public const string ScopesManage = PermissionPrefix + PermissionCodes.ScopesManage;
    public const string AuditView = PermissionPrefix + PermissionCodes.AuditView;
    public const string AttachmentsUpload = PermissionPrefix + PermissionCodes.AttachmentsUpload;
    public const string AttachmentsDownload = PermissionPrefix + PermissionCodes.AttachmentsDownload;

    public const string NotesView = PermissionPrefix + PermissionCodes.NotesView;
    public const string NotesViewSensitive = PermissionPrefix + PermissionCodes.NotesViewSensitive;
    public const string NotesCreate = PermissionPrefix + PermissionCodes.NotesCreate;
    public const string NotesUpdate = PermissionPrefix + PermissionCodes.NotesUpdate;
    public const string NotesAssign = PermissionPrefix + PermissionCodes.NotesAssign;
    public const string NotesStartWork = PermissionPrefix + PermissionCodes.NotesStartWork;
    public const string NotesSubmitForVerification = PermissionPrefix + PermissionCodes.NotesSubmitForVerification;
    public const string NotesVerifyClosure = PermissionPrefix + PermissionCodes.NotesVerifyClosure;
    public const string NotesReturnForRework = PermissionPrefix + PermissionCodes.NotesReturnForRework;
    public const string NotesReopen = PermissionPrefix + PermissionCodes.NotesReopen;
    public const string NotesCancel = PermissionPrefix + PermissionCodes.NotesCancel;
    public const string NotesArchive = PermissionPrefix + PermissionCodes.NotesArchive;
    public const string NotesRestore = PermissionPrefix + PermissionCodes.NotesRestore;
    public const string NotesManageTypes = PermissionPrefix + PermissionCodes.NotesManageTypes;
    public const string NotesManageRoleTypeAccess = PermissionPrefix + PermissionCodes.NotesManageRoleTypeAccess;
    public const string NotesManageUserTypeOverrides = PermissionPrefix + PermissionCodes.NotesManageUserTypeOverrides;
    public const string NotesManageIntakeProfiles = PermissionPrefix + PermissionCodes.NotesManageIntakeProfiles;
    public const string NotesViewRouting = PermissionPrefix + PermissionCodes.NotesViewRouting;
    public const string NotesManageRoutingRules = PermissionPrefix + PermissionCodes.NotesManageRoutingRules;
    public const string NotesActivateRoutingRules = PermissionPrefix + PermissionCodes.NotesActivateRoutingRules;
    public const string NotesRunRouting = PermissionPrefix + PermissionCodes.NotesRunRouting;
    public const string NotesViewRoutingDiagnostics = PermissionPrefix + PermissionCodes.NotesViewRoutingDiagnostics;
    /// <summary>Coarse endpoint-level gate for the generic approve/return-decision routes — the specific
    /// Notes.Approve{Invalid,Duplicate,NoAction} permission for the request's DecisionType is the real,
    /// fine-grained check enforced inside NoteDecisionApprovalService.</summary>
    public const string NotesApproveAnyDecision = PermissionPrefix + "Notes.ApproveAnyDecision";
    public const string NotesProposeInvalid = PermissionPrefix + PermissionCodes.NotesProposeInvalid;
    public const string NotesApproveInvalid = PermissionPrefix + PermissionCodes.NotesApproveInvalid;
    public const string NotesProposeDuplicate = PermissionPrefix + PermissionCodes.NotesProposeDuplicate;
    public const string NotesApproveDuplicate = PermissionPrefix + PermissionCodes.NotesApproveDuplicate;
    public const string NotesProposeNoAction = PermissionPrefix + PermissionCodes.NotesProposeNoAction;
    public const string NotesApproveNoAction = PermissionPrefix + PermissionCodes.NotesApproveNoAction;
    public const string NotesApproveSlaPause = PermissionPrefix + PermissionCodes.NotesApproveSlaPause;
    public const string CorrectiveActionsView = PermissionPrefix + PermissionCodes.CorrectiveActionsView;
    public const string CorrectiveActionsViewSensitive = PermissionPrefix + PermissionCodes.CorrectiveActionsViewSensitive;
    public const string CorrectiveActionsCreate = PermissionPrefix + PermissionCodes.CorrectiveActionsCreate;
    public const string CorrectiveActionsUpdate = PermissionPrefix + PermissionCodes.CorrectiveActionsUpdate;
    public const string CorrectiveActionsAssign = PermissionPrefix + PermissionCodes.CorrectiveActionsAssign;
    public const string CorrectiveActionsStartWork = PermissionPrefix + PermissionCodes.CorrectiveActionsStartWork;
    public const string CorrectiveActionsSubmitForVerification = PermissionPrefix + PermissionCodes.CorrectiveActionsSubmitForVerification;
    public const string CorrectiveActionsVerifyCompletion = PermissionPrefix + PermissionCodes.CorrectiveActionsVerifyCompletion;
    public const string CorrectiveActionsReturnForRework = PermissionPrefix + PermissionCodes.CorrectiveActionsReturnForRework;
    public const string CorrectiveActionsReopen = PermissionPrefix + PermissionCodes.CorrectiveActionsReopen;
    public const string CorrectiveActionsCancel = PermissionPrefix + PermissionCodes.CorrectiveActionsCancel;
    public const string CorrectiveActionsArchive = PermissionPrefix + PermissionCodes.CorrectiveActionsArchive;
    public const string CorrectiveActionsRestore = PermissionPrefix + PermissionCodes.CorrectiveActionsRestore;
    public const string EscalationsView = PermissionPrefix + PermissionCodes.EscalationsView;
    public const string EscalationsManage = PermissionPrefix + PermissionCodes.EscalationsManage;
    public const string EscalationsActivate = PermissionPrefix + PermissionCodes.EscalationsActivate;
    public const string EscalationsRun = PermissionPrefix + PermissionCodes.EscalationsRun;
    public const string EscalationsViewOccurrences = PermissionPrefix + PermissionCodes.EscalationsViewOccurrences;
    public const string EscalationsRetryFailed = PermissionPrefix + PermissionCodes.EscalationsRetryFailed;
    public const string NotificationsViewOwn = PermissionPrefix + PermissionCodes.NotificationsViewOwn;
    public const string NotificationsMarkRead = PermissionPrefix + PermissionCodes.NotificationsMarkRead;
    public const string NotificationsArchiveOwn = PermissionPrefix + PermissionCodes.NotificationsArchiveOwn;
    public const string DashboardViewOperational = PermissionPrefix + PermissionCodes.DashboardViewOperational;
    public const string DashboardViewRisk = PermissionPrefix + PermissionCodes.DashboardViewRisk;
    public const string DashboardViewRouting = PermissionPrefix + PermissionCodes.DashboardViewRouting;
    public const string DashboardViewCorrectiveActions = PermissionPrefix + PermissionCodes.DashboardViewCorrectiveActions;
    public const string WorkspacesView = PermissionPrefix + PermissionCodes.WorkspacesView;
    public const string WorkspacesViewDomain = PermissionPrefix + PermissionCodes.WorkspacesViewDomain;
    public const string WorkspacesViewFacility = PermissionPrefix + PermissionCodes.WorkspacesViewFacility;
    public const string WorkspacesViewRegion = PermissionPrefix + PermissionCodes.WorkspacesViewRegion;
    public const string WorkspacesViewHeadquarters = PermissionPrefix + PermissionCodes.WorkspacesViewHeadquarters;
    public const string WorkspacesConfigureOwnView = PermissionPrefix + PermissionCodes.WorkspacesConfigureOwnView;
    public const string OccupancyViewSummary = PermissionPrefix + PermissionCodes.OccupancyViewSummary;
    public const string OccupancyViewUnitBreakdown = PermissionPrefix + PermissionCodes.OccupancyViewUnitBreakdown;
    public const string OccupancyViewMovements = PermissionPrefix + PermissionCodes.OccupancyViewMovements;
    public const string OccupancyViewSensitiveMovements = PermissionPrefix + PermissionCodes.OccupancyViewSensitiveMovements;
    public const string OccupancyManageCapacity = PermissionPrefix + PermissionCodes.OccupancyManageCapacity;
    public const string OccupancyRecordSnapshot = PermissionPrefix + PermissionCodes.OccupancyRecordSnapshot;
    public const string OccupancyImport = PermissionPrefix + PermissionCodes.OccupancyImport;
    public const string OccupancyExport = PermissionPrefix + PermissionCodes.OccupancyExport;
    public const string OccupancyReconcile = PermissionPrefix + PermissionCodes.OccupancyReconcile;
    public const string ResourcesViewSummary = PermissionPrefix + PermissionCodes.ResourcesViewSummary;
    public const string ResourcesViewAssets = PermissionPrefix + PermissionCodes.ResourcesViewAssets;
    public const string ResourcesViewVehicles = PermissionPrefix + PermissionCodes.ResourcesViewVehicles;
    public const string ResourcesViewCommunicationDevices = PermissionPrefix + PermissionCodes.ResourcesViewCommunicationDevices;
    public const string ResourcesViewEquipment = PermissionPrefix + PermissionCodes.ResourcesViewEquipment;
    public const string ResourcesViewFacilityAssets = PermissionPrefix + PermissionCodes.ResourcesViewFacilityAssets;
    public const string ResourcesManageAssets = PermissionPrefix + PermissionCodes.ResourcesManageAssets;
    public const string ResourcesManagePlacements = PermissionPrefix + PermissionCodes.ResourcesManagePlacements;
    public const string ResourcesManageStatus = PermissionPrefix + PermissionCodes.ResourcesManageStatus;
    public const string ResourcesViewMaintenance = PermissionPrefix + PermissionCodes.ResourcesViewMaintenance;
    public const string ResourcesManageMaintenance = PermissionPrefix + PermissionCodes.ResourcesManageMaintenance;
    public const string ResourcesViewRequirements = PermissionPrefix + PermissionCodes.ResourcesViewRequirements;
    public const string ResourcesManageRequirements = PermissionPrefix + PermissionCodes.ResourcesManageRequirements;
    public const string ResourcesImport = PermissionPrefix + PermissionCodes.ResourcesImport;
    public const string ResourcesExport = PermissionPrefix + PermissionCodes.ResourcesExport;
    public const string ResourcesReconcile = PermissionPrefix + PermissionCodes.ResourcesReconcile;
    public const string WorkforceViewSummary = PermissionPrefix + PermissionCodes.WorkforceViewSummary;
    public const string WorkforceViewCoverage = PermissionPrefix + PermissionCodes.WorkforceViewCoverage;
    public const string WorkforceViewMembers = PermissionPrefix + PermissionCodes.WorkforceViewMembers;
    public const string WorkforceViewSensitiveRestrictions = PermissionPrefix + PermissionCodes.WorkforceViewSensitiveRestrictions;
    public const string WorkforceManageMembers = PermissionPrefix + PermissionCodes.WorkforceManageMembers;
    public const string WorkforceManageAssignments = PermissionPrefix + PermissionCodes.WorkforceManageAssignments;
    public const string WorkforceManageQualifications = PermissionPrefix + PermissionCodes.WorkforceManageQualifications;
    public const string WorkforceManageRequirements = PermissionPrefix + PermissionCodes.WorkforceManageRequirements;
    public const string WorkforceManageRosters = PermissionPrefix + PermissionCodes.WorkforceManageRosters;
    public const string WorkforceRecordAvailability = PermissionPrefix + PermissionCodes.WorkforceRecordAvailability;
    public const string WorkforceImport = PermissionPrefix + PermissionCodes.WorkforceImport;
    public const string WorkforceExport = PermissionPrefix + PermissionCodes.WorkforceExport;
    public const string WorkforceReconcile = PermissionPrefix + PermissionCodes.WorkforceReconcile;
    public const string SensitiveCustodyViewSummary = PermissionPrefix + PermissionCodes.SensitiveCustodyViewSummary;
    public const string SensitiveCustodyViewWeapons = PermissionPrefix + PermissionCodes.SensitiveCustodyViewWeapons;
    public const string SensitiveCustodyViewSerialNumbers = PermissionPrefix + PermissionCodes.SensitiveCustodyViewSerialNumbers;
    public const string SensitiveCustodyViewArmoryLocations = PermissionPrefix + PermissionCodes.SensitiveCustodyViewArmoryLocations;
    public const string SensitiveCustodyViewAmmunition = PermissionPrefix + PermissionCodes.SensitiveCustodyViewAmmunition;
    public const string SensitiveCustodyViewCustodyTransactions = PermissionPrefix + PermissionCodes.SensitiveCustodyViewCustodyTransactions;
    public const string SensitiveCustodyManageWeapons = PermissionPrefix + PermissionCodes.SensitiveCustodyManageWeapons;
    public const string SensitiveCustodyIssueWeapons = PermissionPrefix + PermissionCodes.SensitiveCustodyIssueWeapons;
    public const string SensitiveCustodyReceiveWeapons = PermissionPrefix + PermissionCodes.SensitiveCustodyReceiveWeapons;
    public const string SensitiveCustodyApproveTransactions = PermissionPrefix + PermissionCodes.SensitiveCustodyApproveTransactions;
    public const string SensitiveCustodyManageAmmunition = PermissionPrefix + PermissionCodes.SensitiveCustodyManageAmmunition;
    public const string SensitiveCustodyConductInventory = PermissionPrefix + PermissionCodes.SensitiveCustodyConductInventory;
    public const string SensitiveCustodyApproveInventory = PermissionPrefix + PermissionCodes.SensitiveCustodyApproveInventory;
    public const string SensitiveCustodyManageInspections = PermissionPrefix + PermissionCodes.SensitiveCustodyManageInspections;
    public const string SensitiveCustodyManageMaintenance = PermissionPrefix + PermissionCodes.SensitiveCustodyManageMaintenance;
    public const string SensitiveCustodyViewDiscrepancies = PermissionPrefix + PermissionCodes.SensitiveCustodyViewDiscrepancies;
    public const string SensitiveCustodyExport = PermissionPrefix + PermissionCodes.SensitiveCustodyExport;
    public const string SensitiveCustodyImport = PermissionPrefix + PermissionCodes.SensitiveCustodyImport;
    public const string SensitiveCustodyReconcile = PermissionPrefix + PermissionCodes.SensitiveCustodyReconcile;
    public const string RisksViewSummary = PermissionPrefix + PermissionCodes.RisksViewSummary;
    public const string RisksView = PermissionPrefix + PermissionCodes.RisksView;
    public const string RisksViewSensitive = PermissionPrefix + PermissionCodes.RisksViewSensitive;
    public const string RisksCreate = PermissionPrefix + PermissionCodes.RisksCreate;
    public const string RisksUpdate = PermissionPrefix + PermissionCodes.RisksUpdate;
    public const string RisksAssignOwner = PermissionPrefix + PermissionCodes.RisksAssignOwner;
    public const string RisksManageCategories = PermissionPrefix + PermissionCodes.RisksManageCategories;
    public const string RisksAssess = PermissionPrefix + PermissionCodes.RisksAssess;
    public const string RisksReviewAssessment = PermissionPrefix + PermissionCodes.RisksReviewAssessment;
    public const string RisksApproveAssessment = PermissionPrefix + PermissionCodes.RisksApproveAssessment;
    public const string RisksManageControls = PermissionPrefix + PermissionCodes.RisksManageControls;
    public const string RisksManageTreatments = PermissionPrefix + PermissionCodes.RisksManageTreatments;
    public const string RisksCompleteTreatmentActions = PermissionPrefix + PermissionCodes.RisksCompleteTreatmentActions;
    public const string RisksVerifyTreatmentActions = PermissionPrefix + PermissionCodes.RisksVerifyTreatmentActions;
    public const string RisksRequestAcceptance = PermissionPrefix + PermissionCodes.RisksRequestAcceptance;
    public const string RisksApproveAcceptance = PermissionPrefix + PermissionCodes.RisksApproveAcceptance;
    public const string RisksRequestClosure = PermissionPrefix + PermissionCodes.RisksRequestClosure;
    public const string RisksApproveClosure = PermissionPrefix + PermissionCodes.RisksApproveClosure;
    public const string RisksReopen = PermissionPrefix + PermissionCodes.RisksReopen;
    public const string RisksEscalate = PermissionPrefix + PermissionCodes.RisksEscalate;
    public const string RisksLinkSources = PermissionPrefix + PermissionCodes.RisksLinkSources;
    public const string RisksExport = PermissionPrefix + PermissionCodes.RisksExport;
    public const string RisksImport = PermissionPrefix + PermissionCodes.RisksImport;
    public const string RisksManageMatrices = PermissionPrefix + PermissionCodes.RisksManageMatrices;
    public const string RisksApproveMatrices = PermissionPrefix + PermissionCodes.RisksApproveMatrices;
    public const string FormsView = PermissionPrefix + PermissionCodes.FormsView;
    public const string FormsViewSensitive = PermissionPrefix + PermissionCodes.FormsViewSensitive;
    public const string FormsCreate = PermissionPrefix + PermissionCodes.FormsCreate;
    public const string FormsUpdateDraft = PermissionPrefix + PermissionCodes.FormsUpdateDraft;
    public const string FormsSubmitForReview = PermissionPrefix + PermissionCodes.FormsSubmitForReview;
    public const string FormsReview = PermissionPrefix + PermissionCodes.FormsReview;
    public const string FormsApprove = PermissionPrefix + PermissionCodes.FormsApprove;
    public const string FormsReject = PermissionPrefix + PermissionCodes.FormsReject;
    public const string FormsRequestChanges = PermissionPrefix + PermissionCodes.FormsRequestChanges;
    public const string FormsArchive = PermissionPrefix + PermissionCodes.FormsArchive;
    public const string FormsRestore = PermissionPrefix + PermissionCodes.FormsRestore;
    public const string FormsManageAccess = PermissionPrefix + PermissionCodes.FormsManageAccess;
    public const string FormsManageGovernance = PermissionPrefix + PermissionCodes.FormsManageGovernance;
    public const string FormsManageRetention = PermissionPrefix + PermissionCodes.FormsManageRetention;
    public const string FormsCloneVersion = PermissionPrefix + PermissionCodes.FormsCloneVersion;
    public const string FormsViewVersionHistory = PermissionPrefix + PermissionCodes.FormsViewVersionHistory;
    public const string FormsManageTemplates = PermissionPrefix + PermissionCodes.FormsManageTemplates;
    public const string FormsPublish = PermissionPrefix + PermissionCodes.FormsPublish;
    public const string FormsManageCampaigns = PermissionPrefix + PermissionCodes.FormsManageCampaigns;
    public const string FormsPreviewTargets = PermissionPrefix + PermissionCodes.FormsPreviewTargets;
    public const string FormsPauseCampaign = PermissionPrefix + PermissionCodes.FormsPauseCampaign;
    public const string FormsCancelCampaign = PermissionPrefix + PermissionCodes.FormsCancelCampaign;
    public const string FormsViewCampaignAssignments = PermissionPrefix + PermissionCodes.FormsViewCampaignAssignments;
    public const string FormsMonitorRegion = PermissionPrefix + PermissionCodes.FormsMonitorRegion;
    public const string FormsMonitorHeadquarters = PermissionPrefix + PermissionCodes.FormsMonitorHeadquarters;
    public const string FormsRespond = PermissionPrefix + PermissionCodes.FormsRespond;
    public const string FormsViewResponses = PermissionPrefix + PermissionCodes.FormsViewResponses;
    public const string FormsReviewResponses = PermissionPrefix + PermissionCodes.FormsReviewResponses;
    public const string FormsApproveResponses = PermissionPrefix + PermissionCodes.FormsApproveResponses;
    public const string FormsCloseResponses = PermissionPrefix + PermissionCodes.FormsCloseResponses;
    public const string FormsViewSensitiveResponses = PermissionPrefix + PermissionCodes.FormsViewSensitiveResponses;
    public const string FormsViewComplianceDashboard = PermissionPrefix + PermissionCodes.FormsViewComplianceDashboard;
    public const string FormsExportComplianceDashboard = PermissionPrefix + PermissionCodes.FormsExportComplianceDashboard;
    public const string FormsViewResponseDetail = "perm:Forms.ViewResponseDetail";
}


public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class AnyPermissionRequirement(params string[] permissions) : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; } = permissions;
}

public sealed class PermissionAuthorizationHandler(Application.Abstractions.ICurrentUser currentUser)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (currentUser.IsAuthenticated && currentUser.HasPermission(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed class AnyPermissionAuthorizationHandler(Application.Abstractions.ICurrentUser currentUser)
    : AuthorizationHandler<AnyPermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AnyPermissionRequirement requirement)
    {
        if (currentUser.IsAuthenticated
            && requirement.Permissions.Any(p => currentUser.HasPermission(p)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public static class AuthorizationExtensions
{
    public static IServiceCollection AddBaseeraAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, AnyPermissionAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            void AddPerm(string policy, string permission) =>
                options.AddPolicy(policy, p => p.Requirements.Add(new PermissionRequirement(permission)));

            options.AddPolicy(AuthPolicies.FormsViewResponseDetail, p => p.Requirements.Add(new AnyPermissionRequirement(
                PermissionCodes.FormsViewResponses,
                PermissionCodes.FormsReviewResponses,
                PermissionCodes.FormsApproveResponses,
                PermissionCodes.FormsCloseResponses)));

            AddPerm(AuthPolicies.OrganizationView, PermissionCodes.OrganizationView);
            AddPerm(AuthPolicies.OrganizationManage, PermissionCodes.OrganizationManage);
            AddPerm(AuthPolicies.UsersView, PermissionCodes.UsersView);
            AddPerm(AuthPolicies.RolesManage, PermissionCodes.RolesManage);
            AddPerm(AuthPolicies.ScopesManage, PermissionCodes.ScopesManage);
            AddPerm(AuthPolicies.AuditView, PermissionCodes.AuditView);
            AddPerm(AuthPolicies.AttachmentsUpload, PermissionCodes.AttachmentsUpload);
            AddPerm(AuthPolicies.AttachmentsDownload, PermissionCodes.AttachmentsDownload);

            AddPerm(AuthPolicies.NotesView, PermissionCodes.NotesView);
            AddPerm(AuthPolicies.NotesViewSensitive, PermissionCodes.NotesViewSensitive);
            AddPerm(AuthPolicies.NotesCreate, PermissionCodes.NotesCreate);
            AddPerm(AuthPolicies.NotesUpdate, PermissionCodes.NotesUpdate);
            AddPerm(AuthPolicies.NotesAssign, PermissionCodes.NotesAssign);
            AddPerm(AuthPolicies.NotesStartWork, PermissionCodes.NotesStartWork);
            AddPerm(AuthPolicies.NotesSubmitForVerification, PermissionCodes.NotesSubmitForVerification);
            AddPerm(AuthPolicies.NotesVerifyClosure, PermissionCodes.NotesVerifyClosure);
            AddPerm(AuthPolicies.NotesReturnForRework, PermissionCodes.NotesReturnForRework);
            AddPerm(AuthPolicies.NotesReopen, PermissionCodes.NotesReopen);
            AddPerm(AuthPolicies.NotesCancel, PermissionCodes.NotesCancel);
            AddPerm(AuthPolicies.NotesArchive, PermissionCodes.NotesArchive);
            AddPerm(AuthPolicies.NotesRestore, PermissionCodes.NotesRestore);
            AddPerm(AuthPolicies.NotesManageTypes, PermissionCodes.NotesManageTypes);
            AddPerm(AuthPolicies.NotesManageRoleTypeAccess, PermissionCodes.NotesManageRoleTypeAccess);
            AddPerm(AuthPolicies.NotesManageUserTypeOverrides, PermissionCodes.NotesManageUserTypeOverrides);
            AddPerm(AuthPolicies.NotesManageIntakeProfiles, PermissionCodes.NotesManageIntakeProfiles);
            AddPerm(AuthPolicies.NotesViewRouting, PermissionCodes.NotesViewRouting);
            AddPerm(AuthPolicies.NotesManageRoutingRules, PermissionCodes.NotesManageRoutingRules);
            AddPerm(AuthPolicies.NotesActivateRoutingRules, PermissionCodes.NotesActivateRoutingRules);
            AddPerm(AuthPolicies.NotesRunRouting, PermissionCodes.NotesRunRouting);
            AddPerm(AuthPolicies.NotesViewRoutingDiagnostics, PermissionCodes.NotesViewRoutingDiagnostics);
            options.AddPolicy(AuthPolicies.NotesApproveAnyDecision, p => p.Requirements.Add(new AnyPermissionRequirement(
                PermissionCodes.NotesApproveInvalid,
                PermissionCodes.NotesApproveDuplicate,
                PermissionCodes.NotesApproveNoAction)));
            AddPerm(AuthPolicies.NotesProposeInvalid, PermissionCodes.NotesProposeInvalid);
            AddPerm(AuthPolicies.NotesApproveInvalid, PermissionCodes.NotesApproveInvalid);
            AddPerm(AuthPolicies.NotesProposeDuplicate, PermissionCodes.NotesProposeDuplicate);
            AddPerm(AuthPolicies.NotesApproveDuplicate, PermissionCodes.NotesApproveDuplicate);
            AddPerm(AuthPolicies.NotesProposeNoAction, PermissionCodes.NotesProposeNoAction);
            AddPerm(AuthPolicies.NotesApproveNoAction, PermissionCodes.NotesApproveNoAction);
            AddPerm(AuthPolicies.NotesApproveSlaPause, PermissionCodes.NotesApproveSlaPause);
            AddPerm(AuthPolicies.CorrectiveActionsView, PermissionCodes.CorrectiveActionsView);
            AddPerm(AuthPolicies.CorrectiveActionsViewSensitive, PermissionCodes.CorrectiveActionsViewSensitive);
            AddPerm(AuthPolicies.CorrectiveActionsCreate, PermissionCodes.CorrectiveActionsCreate);
            AddPerm(AuthPolicies.CorrectiveActionsUpdate, PermissionCodes.CorrectiveActionsUpdate);
            AddPerm(AuthPolicies.CorrectiveActionsAssign, PermissionCodes.CorrectiveActionsAssign);
            AddPerm(AuthPolicies.CorrectiveActionsStartWork, PermissionCodes.CorrectiveActionsStartWork);
            AddPerm(AuthPolicies.CorrectiveActionsSubmitForVerification, PermissionCodes.CorrectiveActionsSubmitForVerification);
            AddPerm(AuthPolicies.CorrectiveActionsVerifyCompletion, PermissionCodes.CorrectiveActionsVerifyCompletion);
            AddPerm(AuthPolicies.CorrectiveActionsReturnForRework, PermissionCodes.CorrectiveActionsReturnForRework);
            AddPerm(AuthPolicies.CorrectiveActionsReopen, PermissionCodes.CorrectiveActionsReopen);
            AddPerm(AuthPolicies.CorrectiveActionsCancel, PermissionCodes.CorrectiveActionsCancel);
            AddPerm(AuthPolicies.CorrectiveActionsArchive, PermissionCodes.CorrectiveActionsArchive);
            AddPerm(AuthPolicies.CorrectiveActionsRestore, PermissionCodes.CorrectiveActionsRestore);
            AddPerm(AuthPolicies.EscalationsView, PermissionCodes.EscalationsView);
            AddPerm(AuthPolicies.EscalationsManage, PermissionCodes.EscalationsManage);
            AddPerm(AuthPolicies.EscalationsActivate, PermissionCodes.EscalationsActivate);
            AddPerm(AuthPolicies.EscalationsRun, PermissionCodes.EscalationsRun);
            AddPerm(AuthPolicies.EscalationsViewOccurrences, PermissionCodes.EscalationsViewOccurrences);
            AddPerm(AuthPolicies.EscalationsRetryFailed, PermissionCodes.EscalationsRetryFailed);
            AddPerm(AuthPolicies.NotificationsViewOwn, PermissionCodes.NotificationsViewOwn);
            AddPerm(AuthPolicies.NotificationsMarkRead, PermissionCodes.NotificationsMarkRead);
            AddPerm(AuthPolicies.NotificationsArchiveOwn, PermissionCodes.NotificationsArchiveOwn);
            AddPerm(AuthPolicies.DashboardViewOperational, PermissionCodes.DashboardViewOperational);
            AddPerm(AuthPolicies.DashboardViewRisk, PermissionCodes.DashboardViewRisk);
            AddPerm(AuthPolicies.DashboardViewRouting, PermissionCodes.DashboardViewRouting);
            AddPerm(AuthPolicies.DashboardViewCorrectiveActions, PermissionCodes.DashboardViewCorrectiveActions);
            AddPerm(AuthPolicies.WorkspacesView, PermissionCodes.WorkspacesView);
            AddPerm(AuthPolicies.WorkspacesViewDomain, PermissionCodes.WorkspacesViewDomain);
            AddPerm(AuthPolicies.WorkspacesViewFacility, PermissionCodes.WorkspacesViewFacility);
            AddPerm(AuthPolicies.WorkspacesViewRegion, PermissionCodes.WorkspacesViewRegion);
            AddPerm(AuthPolicies.WorkspacesViewHeadquarters, PermissionCodes.WorkspacesViewHeadquarters);
            AddPerm(AuthPolicies.WorkspacesConfigureOwnView, PermissionCodes.WorkspacesConfigureOwnView);
            AddPerm(AuthPolicies.OccupancyViewSummary, PermissionCodes.OccupancyViewSummary);
            AddPerm(AuthPolicies.OccupancyViewUnitBreakdown, PermissionCodes.OccupancyViewUnitBreakdown);
            AddPerm(AuthPolicies.OccupancyViewMovements, PermissionCodes.OccupancyViewMovements);
            AddPerm(AuthPolicies.OccupancyViewSensitiveMovements, PermissionCodes.OccupancyViewSensitiveMovements);
            AddPerm(AuthPolicies.OccupancyManageCapacity, PermissionCodes.OccupancyManageCapacity);
            AddPerm(AuthPolicies.OccupancyRecordSnapshot, PermissionCodes.OccupancyRecordSnapshot);
            AddPerm(AuthPolicies.OccupancyImport, PermissionCodes.OccupancyImport);
            AddPerm(AuthPolicies.OccupancyExport, PermissionCodes.OccupancyExport);
            AddPerm(AuthPolicies.OccupancyReconcile, PermissionCodes.OccupancyReconcile);
            AddPerm(AuthPolicies.ResourcesViewSummary, PermissionCodes.ResourcesViewSummary);
            AddPerm(AuthPolicies.ResourcesViewAssets, PermissionCodes.ResourcesViewAssets);
            AddPerm(AuthPolicies.ResourcesViewVehicles, PermissionCodes.ResourcesViewVehicles);
            AddPerm(AuthPolicies.ResourcesViewCommunicationDevices, PermissionCodes.ResourcesViewCommunicationDevices);
            AddPerm(AuthPolicies.ResourcesViewEquipment, PermissionCodes.ResourcesViewEquipment);
            AddPerm(AuthPolicies.ResourcesViewFacilityAssets, PermissionCodes.ResourcesViewFacilityAssets);
            AddPerm(AuthPolicies.ResourcesManageAssets, PermissionCodes.ResourcesManageAssets);
            AddPerm(AuthPolicies.ResourcesManagePlacements, PermissionCodes.ResourcesManagePlacements);
            AddPerm(AuthPolicies.ResourcesManageStatus, PermissionCodes.ResourcesManageStatus);
            AddPerm(AuthPolicies.ResourcesViewMaintenance, PermissionCodes.ResourcesViewMaintenance);
            AddPerm(AuthPolicies.ResourcesManageMaintenance, PermissionCodes.ResourcesManageMaintenance);
            AddPerm(AuthPolicies.ResourcesViewRequirements, PermissionCodes.ResourcesViewRequirements);
            AddPerm(AuthPolicies.ResourcesManageRequirements, PermissionCodes.ResourcesManageRequirements);
            AddPerm(AuthPolicies.ResourcesImport, PermissionCodes.ResourcesImport);
            AddPerm(AuthPolicies.ResourcesExport, PermissionCodes.ResourcesExport);
            AddPerm(AuthPolicies.ResourcesReconcile, PermissionCodes.ResourcesReconcile);
            AddPerm(AuthPolicies.WorkforceViewSummary, PermissionCodes.WorkforceViewSummary);
            AddPerm(AuthPolicies.WorkforceViewCoverage, PermissionCodes.WorkforceViewCoverage);
            AddPerm(AuthPolicies.WorkforceViewMembers, PermissionCodes.WorkforceViewMembers);
            AddPerm(AuthPolicies.WorkforceViewSensitiveRestrictions, PermissionCodes.WorkforceViewSensitiveRestrictions);
            AddPerm(AuthPolicies.WorkforceManageMembers, PermissionCodes.WorkforceManageMembers);
            AddPerm(AuthPolicies.WorkforceManageAssignments, PermissionCodes.WorkforceManageAssignments);
            AddPerm(AuthPolicies.WorkforceManageQualifications, PermissionCodes.WorkforceManageQualifications);
            AddPerm(AuthPolicies.WorkforceManageRequirements, PermissionCodes.WorkforceManageRequirements);
            AddPerm(AuthPolicies.WorkforceManageRosters, PermissionCodes.WorkforceManageRosters);
            AddPerm(AuthPolicies.WorkforceRecordAvailability, PermissionCodes.WorkforceRecordAvailability);
            AddPerm(AuthPolicies.WorkforceImport, PermissionCodes.WorkforceImport);
            AddPerm(AuthPolicies.WorkforceExport, PermissionCodes.WorkforceExport);
            AddPerm(AuthPolicies.WorkforceReconcile, PermissionCodes.WorkforceReconcile);
            AddPerm(AuthPolicies.SensitiveCustodyViewSummary, PermissionCodes.SensitiveCustodyViewSummary);
            AddPerm(AuthPolicies.SensitiveCustodyViewWeapons, PermissionCodes.SensitiveCustodyViewWeapons);
            AddPerm(AuthPolicies.SensitiveCustodyViewSerialNumbers, PermissionCodes.SensitiveCustodyViewSerialNumbers);
            AddPerm(AuthPolicies.SensitiveCustodyViewArmoryLocations, PermissionCodes.SensitiveCustodyViewArmoryLocations);
            AddPerm(AuthPolicies.SensitiveCustodyViewAmmunition, PermissionCodes.SensitiveCustodyViewAmmunition);
            AddPerm(AuthPolicies.SensitiveCustodyViewCustodyTransactions, PermissionCodes.SensitiveCustodyViewCustodyTransactions);
            AddPerm(AuthPolicies.SensitiveCustodyManageWeapons, PermissionCodes.SensitiveCustodyManageWeapons);
            AddPerm(AuthPolicies.SensitiveCustodyIssueWeapons, PermissionCodes.SensitiveCustodyIssueWeapons);
            AddPerm(AuthPolicies.SensitiveCustodyReceiveWeapons, PermissionCodes.SensitiveCustodyReceiveWeapons);
            AddPerm(AuthPolicies.SensitiveCustodyApproveTransactions, PermissionCodes.SensitiveCustodyApproveTransactions);
            AddPerm(AuthPolicies.SensitiveCustodyManageAmmunition, PermissionCodes.SensitiveCustodyManageAmmunition);
            AddPerm(AuthPolicies.SensitiveCustodyConductInventory, PermissionCodes.SensitiveCustodyConductInventory);
            AddPerm(AuthPolicies.SensitiveCustodyApproveInventory, PermissionCodes.SensitiveCustodyApproveInventory);
            AddPerm(AuthPolicies.SensitiveCustodyManageInspections, PermissionCodes.SensitiveCustodyManageInspections);
            AddPerm(AuthPolicies.SensitiveCustodyManageMaintenance, PermissionCodes.SensitiveCustodyManageMaintenance);
            AddPerm(AuthPolicies.SensitiveCustodyViewDiscrepancies, PermissionCodes.SensitiveCustodyViewDiscrepancies);
            AddPerm(AuthPolicies.SensitiveCustodyExport, PermissionCodes.SensitiveCustodyExport);
            AddPerm(AuthPolicies.SensitiveCustodyImport, PermissionCodes.SensitiveCustodyImport);
            AddPerm(AuthPolicies.SensitiveCustodyReconcile, PermissionCodes.SensitiveCustodyReconcile);
            AddPerm(AuthPolicies.RisksViewSummary, PermissionCodes.RisksViewSummary);
            AddPerm(AuthPolicies.RisksView, PermissionCodes.RisksView);
            AddPerm(AuthPolicies.RisksViewSensitive, PermissionCodes.RisksViewSensitive);
            AddPerm(AuthPolicies.RisksCreate, PermissionCodes.RisksCreate);
            AddPerm(AuthPolicies.RisksUpdate, PermissionCodes.RisksUpdate);
            AddPerm(AuthPolicies.RisksAssignOwner, PermissionCodes.RisksAssignOwner);
            AddPerm(AuthPolicies.RisksManageCategories, PermissionCodes.RisksManageCategories);
            AddPerm(AuthPolicies.RisksAssess, PermissionCodes.RisksAssess);
            AddPerm(AuthPolicies.RisksReviewAssessment, PermissionCodes.RisksReviewAssessment);
            AddPerm(AuthPolicies.RisksApproveAssessment, PermissionCodes.RisksApproveAssessment);
            AddPerm(AuthPolicies.RisksManageControls, PermissionCodes.RisksManageControls);
            AddPerm(AuthPolicies.RisksManageTreatments, PermissionCodes.RisksManageTreatments);
            AddPerm(AuthPolicies.RisksCompleteTreatmentActions, PermissionCodes.RisksCompleteTreatmentActions);
            AddPerm(AuthPolicies.RisksVerifyTreatmentActions, PermissionCodes.RisksVerifyTreatmentActions);
            AddPerm(AuthPolicies.RisksRequestAcceptance, PermissionCodes.RisksRequestAcceptance);
            AddPerm(AuthPolicies.RisksApproveAcceptance, PermissionCodes.RisksApproveAcceptance);
            AddPerm(AuthPolicies.RisksRequestClosure, PermissionCodes.RisksRequestClosure);
            AddPerm(AuthPolicies.RisksApproveClosure, PermissionCodes.RisksApproveClosure);
            AddPerm(AuthPolicies.RisksReopen, PermissionCodes.RisksReopen);
            AddPerm(AuthPolicies.RisksEscalate, PermissionCodes.RisksEscalate);
            AddPerm(AuthPolicies.RisksLinkSources, PermissionCodes.RisksLinkSources);
            AddPerm(AuthPolicies.RisksExport, PermissionCodes.RisksExport);
            AddPerm(AuthPolicies.RisksImport, PermissionCodes.RisksImport);
            AddPerm(AuthPolicies.RisksManageMatrices, PermissionCodes.RisksManageMatrices);
            AddPerm(AuthPolicies.RisksApproveMatrices, PermissionCodes.RisksApproveMatrices);
            AddPerm(AuthPolicies.FormsView, PermissionCodes.FormsView);
            AddPerm(AuthPolicies.FormsViewSensitive, PermissionCodes.FormsViewSensitive);
            AddPerm(AuthPolicies.FormsCreate, PermissionCodes.FormsCreate);
            AddPerm(AuthPolicies.FormsUpdateDraft, PermissionCodes.FormsUpdateDraft);
            AddPerm(AuthPolicies.FormsSubmitForReview, PermissionCodes.FormsSubmitForReview);
            AddPerm(AuthPolicies.FormsReview, PermissionCodes.FormsReview);
            AddPerm(AuthPolicies.FormsApprove, PermissionCodes.FormsApprove);
            AddPerm(AuthPolicies.FormsReject, PermissionCodes.FormsReject);
            AddPerm(AuthPolicies.FormsRequestChanges, PermissionCodes.FormsRequestChanges);
            AddPerm(AuthPolicies.FormsArchive, PermissionCodes.FormsArchive);
            AddPerm(AuthPolicies.FormsRestore, PermissionCodes.FormsRestore);
            AddPerm(AuthPolicies.FormsManageAccess, PermissionCodes.FormsManageAccess);
            AddPerm(AuthPolicies.FormsManageGovernance, PermissionCodes.FormsManageGovernance);
            AddPerm(AuthPolicies.FormsManageRetention, PermissionCodes.FormsManageRetention);
            AddPerm(AuthPolicies.FormsCloneVersion, PermissionCodes.FormsCloneVersion);
            AddPerm(AuthPolicies.FormsViewVersionHistory, PermissionCodes.FormsViewVersionHistory);
            AddPerm(AuthPolicies.FormsManageTemplates, PermissionCodes.FormsManageTemplates);
            AddPerm(AuthPolicies.FormsPublish, PermissionCodes.FormsPublish);
            AddPerm(AuthPolicies.FormsManageCampaigns, PermissionCodes.FormsManageCampaigns);
            AddPerm(AuthPolicies.FormsPreviewTargets, PermissionCodes.FormsPreviewTargets);
            AddPerm(AuthPolicies.FormsPauseCampaign, PermissionCodes.FormsPauseCampaign);
            AddPerm(AuthPolicies.FormsCancelCampaign, PermissionCodes.FormsCancelCampaign);
            AddPerm(AuthPolicies.FormsViewCampaignAssignments, PermissionCodes.FormsViewCampaignAssignments);
            AddPerm(AuthPolicies.FormsMonitorRegion, PermissionCodes.FormsMonitorRegion);
            AddPerm(AuthPolicies.FormsMonitorHeadquarters, PermissionCodes.FormsMonitorHeadquarters);
            AddPerm(AuthPolicies.FormsRespond, PermissionCodes.FormsRespond);
            AddPerm(AuthPolicies.FormsViewResponses, PermissionCodes.FormsViewResponses);
            AddPerm(AuthPolicies.FormsReviewResponses, PermissionCodes.FormsReviewResponses);
            AddPerm(AuthPolicies.FormsApproveResponses, PermissionCodes.FormsApproveResponses);
            AddPerm(AuthPolicies.FormsCloseResponses, PermissionCodes.FormsCloseResponses);
            AddPerm(AuthPolicies.FormsViewSensitiveResponses, PermissionCodes.FormsViewSensitiveResponses);
            AddPerm(AuthPolicies.FormsViewComplianceDashboard, PermissionCodes.FormsViewComplianceDashboard);
            AddPerm(AuthPolicies.FormsExportComplianceDashboard, PermissionCodes.FormsExportComplianceDashboard);
        });
        return services;
    }
}
