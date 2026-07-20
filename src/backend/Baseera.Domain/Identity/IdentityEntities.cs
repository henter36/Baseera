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
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? AssignedBy { get; set; }
}

public class UserScope : SoftDeletableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
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
    public const string FormsDesign = "Forms.Design";
    public const string FormsPublish = "Forms.Publish";
    public const string FormsSubmit = "Forms.Submit";
    public const string FormsReview = "Forms.Review";
    public const string ProjectsApprove = "Projects.Approve";
    public const string StrategyManage = "Strategy.Manage";
    public const string ReportsExportSensitive = "Reports.ExportSensitive";
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
    public const string Auditor = "Auditor";
    public const string ReadOnlyUser = "ReadOnlyUser";
}
