namespace Baseera.Domain.Identity;

using Baseera.Domain.Common;
using Baseera.Domain.Organization;

public class User : SoftDeletableEntity
{
    public string ExternalSubject { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayNameAr { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public UserProvisioningStatus ProvisioningStatus { get; set; } = UserProvisioningStatus.Active;
    public DateTimeOffset? LastLoginAtUtc { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserScope> UserScopes { get; set; } = new List<UserScope>();
}

public class Role : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? DescriptionAr { get; set; }
    public bool IsSystem { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class Permission : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class RolePermission
{
    private Role? _role;
    private Permission? _permission;

    public Guid RoleId { get; set; }
    public Role Role { get => _role ?? throw new InvalidOperationException("Role navigation has not been loaded."); set => _role = value; }
    public Guid PermissionId { get; set; }
    public Permission Permission { get => _permission ?? throw new InvalidOperationException("Permission navigation has not been loaded."); set => _permission = value; }
}

public class UserRole
{
    private User? _user;
    private Role? _role;

    public Guid UserId { get; set; }
    public User User { get => _user ?? throw new InvalidOperationException("User navigation has not been loaded."); set => _user = value; }
    public Guid RoleId { get; set; }
    public Role Role { get => _role ?? throw new InvalidOperationException("Role navigation has not been loaded."); set => _role = value; }
    public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? AssignedBy { get; set; }
}

public class UserScope : SoftDeletableEntity
{
    private User? _user;

    public Guid UserId { get; set; }
    public User User { get => _user ?? throw new InvalidOperationException("User navigation has not been loaded."); set => _user = value; }
    public ScopeType ScopeType { get; set; }
    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }
    public Guid? FacilityId { get; set; }
    public Facility? Facility { get; set; }
    public Guid? FacilityUnitId { get; set; }
    public FacilityUnit? FacilityUnit { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class PermissionCodes
{
    public const string OrganizationView = "Organization.View";
    public const string OrganizationManage = "Organization.Manage";
    public const string UsersView = "Users.View";
    public const string UsersManage = "Users.Manage";
    public const string RolesManage = "Roles.Manage";
    public const string ScopesManage = "Scopes.Manage";
    public const string AuditView = "Audit.View";
    public const string AttachmentsUpload = "Attachments.Upload";
    public const string AttachmentsDownload = "Attachments.Download";
    public const string AttachmentsDownloadSensitive = "Attachments.DownloadSensitive";
    public const string UsersArchive = "Users.Archive";
    public const string UsersRestore = "Users.Restore";
    public const string OrganizationArchive = "Organization.Archive";
    public const string OrganizationRestore = "Organization.Restore";
    public const string GrantGlobalScope = "Scopes.GrantGlobal";
    public const string GrantHeadquartersScope = "Scopes.GrantHeadquarters";

    public const string VehiclesView = "Vehicles.View";
    public const string VehiclesCreate = "Vehicles.Create";
    public const string VehiclesUpdate = "Vehicles.Update";
    public const string VehiclesTransfer = "Vehicles.Transfer";
    public const string VehiclesDecommission = "Vehicles.Decommission";
    public const string ArmamentView = "Armament.View";
    public const string ArmamentIssue = "Armament.Issue";
    public const string ArmamentReceive = "Armament.Receive";
    public const string ArmamentInventory = "Armament.Inventory";
    public const string ArmamentAdjust = "Armament.Adjust";
    public const string NotesAssign = "Notes.Assign";
    public const string NotesVerifyClosure = "Notes.VerifyClosure";
    public const string NotesView = "Notes.View";
    public const string NotesViewSensitive = "Notes.ViewSensitive";
    public const string NotesCreate = "Notes.Create";
    public const string NotesUpdate = "Notes.Update";
    public const string NotesStartWork = "Notes.StartWork";
    public const string NotesSubmitForVerification = "Notes.SubmitForVerification";
    public const string NotesReturnForRework = "Notes.ReturnForRework";
    public const string NotesReopen = "Notes.Reopen";
    public const string NotesCancel = "Notes.Cancel";
    public const string NotesArchive = "Notes.Archive";
    public const string NotesRestore = "Notes.Restore";
    public const string NotesManageTypes = "Notes.ManageTypes";
    public const string NotesManageRoleTypeAccess = "Notes.ManageRoleTypeAccess";
    public const string NotesManageUserTypeOverrides = "Notes.ManageUserTypeOverrides";
    public const string NotesManageIntakeProfiles = "Notes.ManageIntakeProfiles";
    public const string NotesViewRouting = "Notes.ViewRouting";
    public const string NotesManageRoutingRules = "Notes.ManageRoutingRules";
    public const string NotesActivateRoutingRules = "Notes.ActivateRoutingRules";
    public const string NotesRunRouting = "Notes.RunRouting";
    public const string NotesViewRoutingDiagnostics = "Notes.ViewRoutingDiagnostics";
    public const string CorrectiveActionsView = "CorrectiveActions.View";
    public const string CorrectiveActionsViewSensitive = "CorrectiveActions.ViewSensitive";
    public const string CorrectiveActionsCreate = "CorrectiveActions.Create";
    public const string CorrectiveActionsUpdate = "CorrectiveActions.Update";
    public const string CorrectiveActionsAssign = "CorrectiveActions.Assign";
    public const string CorrectiveActionsStartWork = "CorrectiveActions.StartWork";
    public const string CorrectiveActionsSubmitForVerification = "CorrectiveActions.SubmitForVerification";
    public const string CorrectiveActionsVerifyCompletion = "CorrectiveActions.VerifyCompletion";
    public const string CorrectiveActionsReturnForRework = "CorrectiveActions.ReturnForRework";
    public const string CorrectiveActionsReopen = "CorrectiveActions.Reopen";
    public const string CorrectiveActionsCancel = "CorrectiveActions.Cancel";
    public const string CorrectiveActionsArchive = "CorrectiveActions.Archive";
    public const string CorrectiveActionsRestore = "CorrectiveActions.Restore";
    public const string EscalationsView = "Escalations.View";
    public const string EscalationsManage = "Escalations.Manage";
    public const string EscalationsActivate = "Escalations.Activate";
    public const string EscalationsRun = "Escalations.Run";
    public const string EscalationsViewOccurrences = "Escalations.ViewOccurrences";
    public const string EscalationsRetryFailed = "Escalations.RetryFailed";
    public const string NotificationsViewOwn = "Notifications.ViewOwn";
    public const string NotificationsMarkRead = "Notifications.MarkRead";
    public const string NotificationsArchiveOwn = "Notifications.ArchiveOwn";
    public const string IncidentsApprove = "Incidents.Approve";
    public const string FormsView = "Forms.View";
    public const string FormsViewSensitive = "Forms.ViewSensitive";
    public const string FormsCreate = "Forms.Create";
    public const string FormsUpdateDraft = "Forms.UpdateDraft";
    public const string FormsSubmitForReview = "Forms.SubmitForReview";
    public const string FormsReview = "Forms.Review";
    public const string FormsApprove = "Forms.Approve";
    public const string FormsReject = "Forms.Reject";
    public const string FormsRequestChanges = "Forms.RequestChanges";
    public const string FormsArchive = "Forms.Archive";
    public const string FormsRestore = "Forms.Restore";
    public const string FormsManageAccess = "Forms.ManageAccess";
    public const string FormsManageGovernance = "Forms.ManageGovernance";
    public const string FormsManageRetention = "Forms.ManageRetention";
    public const string FormsPublish = "Forms.Publish";
    public const string FormsRespond = "Forms.Respond";
    public const string FormsMonitorRegion = "Forms.MonitorRegion";
    public const string FormsMonitorHeadquarters = "Forms.MonitorHeadquarters";
    public const string FormsApproveResponses = "Forms.ApproveResponses";
    public const string FormsViewResponses = "Forms.ViewResponses";
    public const string FormsReviewResponses = "Forms.ReviewResponses";
    public const string FormsCloseResponses = "Forms.CloseResponses";
    public const string FormsViewSensitiveResponses = "Forms.ViewSensitiveResponses";
    public const string FormsAnalyze = "Forms.Analyze";
    public const string FormsExport = "Forms.Export";
    public const string FormsViewComplianceDashboard = "Forms.ViewComplianceDashboard";
    public const string FormsExportComplianceDashboard = "Forms.ExportComplianceDashboard";
    public const string FormsCloneVersion = "Forms.CloneVersion";
    public const string FormsViewVersionHistory = "Forms.ViewVersionHistory";
    public const string FormsManageTemplates = "Forms.ManageTemplates";
    public const string FormsManageCampaigns = "Forms.ManageCampaigns";
    public const string FormsPreviewTargets = "Forms.PreviewTargets";
    public const string FormsPauseCampaign = "Forms.PauseCampaign";
    public const string FormsCancelCampaign = "Forms.CancelCampaign";
    public const string FormsViewCampaignAssignments = "Forms.ViewCampaignAssignments";
    public const string ProjectsApprove = "Projects.Approve";
    public const string StrategyManage = "Strategy.Manage";
    public const string ReportsExportSensitive = "Reports.ExportSensitive";
    public const string DashboardViewOperational = "Dashboard.ViewOperational";
    public const string DashboardViewRisk = "Dashboard.ViewRisk";
    public const string DashboardViewRouting = "Dashboard.ViewRouting";
    public const string DashboardViewCorrectiveActions = "Dashboard.ViewCorrectiveActions";
}

public static class RoleCodes
{
    public const string SystemAdministrator = "SystemAdministrator";
    public const string HeadquartersExecutive = "HeadquartersExecutive";
    public const string DecisionSupportDirector = "DecisionSupportDirector";
    public const string DecisionAnalyst = "DecisionAnalyst";
    public const string RegionalDirector = "RegionalDirector";
    public const string RegionalCoordinator = "RegionalCoordinator";
    public const string FacilityDirector = "FacilityDirector";
    public const string FacilityCoordinator = "FacilityCoordinator";
    public const string SecurityOfficer = "SecurityOfficer";
    public const string ArmamentOfficer = "ArmamentOfficer";
    public const string FleetOfficer = "FleetOfficer";
    public const string WorkforceOfficer = "WorkforceOfficer";
    public const string IncidentOfficer = "IncidentOfficer";
    public const string PrisonerCaseOfficer = "PrisonerCaseOfficer";
    public const string ProjectManager = "ProjectManager";
    public const string StrategyOfficer = "StrategyOfficer";
    public const string FormDesigner = "FormDesigner";
    public const string FormReviewer = "FormReviewer";
    public const string FormPublisher = "FormPublisher";
    public const string FormRespondent = "FormRespondent";
    public const string FormRegionalMonitor = "FormRegionalMonitor";
    public const string FormHeadquartersMonitor = "FormHeadquartersMonitor";
    public const string FormApprover = "FormApprover";
    public const string FormAnalyst = "FormAnalyst";
    public const string Auditor = "Auditor";
    public const string ReadOnlyUser = "ReadOnlyUser";
}
