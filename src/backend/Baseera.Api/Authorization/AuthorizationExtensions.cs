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
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
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

public static class AuthorizationExtensions
{
    public static IServiceCollection AddBaseeraAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            void AddPerm(string policy, string permission) =>
                options.AddPolicy(policy, p => p.Requirements.Add(new PermissionRequirement(permission)));

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
        });
        return services;
    }
}
