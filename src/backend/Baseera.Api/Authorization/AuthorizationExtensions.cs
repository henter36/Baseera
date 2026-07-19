namespace Baseera.Api.Authorization;

using Baseera.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

public static class AuthPolicies
{
    public const string OrganizationView = "perm:" + PermissionCodes.OrganizationView;
    public const string OrganizationManage = "perm:" + PermissionCodes.OrganizationManage;
    public const string UsersView = "perm:" + PermissionCodes.UsersView;
    public const string RolesManage = "perm:" + PermissionCodes.RolesManage;
    public const string ScopesManage = "perm:" + PermissionCodes.ScopesManage;
    public const string AuditView = "perm:" + PermissionCodes.AuditView;
    public const string AttachmentsUpload = "perm:" + PermissionCodes.AttachmentsUpload;
    public const string AttachmentsDownload = "perm:" + PermissionCodes.AttachmentsDownload;
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
        });
        return services;
    }
}
