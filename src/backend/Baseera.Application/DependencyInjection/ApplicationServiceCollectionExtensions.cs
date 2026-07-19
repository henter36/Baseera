namespace Baseera.Application.DependencyInjection;

using Baseera.Application.Abstractions;
using Baseera.Application.Attachments;
using Baseera.Application.Audit;
using Baseera.Application.Identity;
using Baseera.Application.Notes;
using Baseera.Application.Organization;
using Baseera.Application.Security;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddBaseeraApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<UpdateRegionRequestValidator>();
        services.AddScoped<IOrganizationalScopeService, OrganizationalScopeService>();
        services.AddScoped<IPrivilegeGuard, PrivilegeGuard>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IAttachmentAppService, AttachmentAppService>();
        services.AddScoped<INoteScopeService, NoteScopeService>();
        services.AddScoped<INoteQueryService, NoteQueryService>();
        services.AddScoped<INoteCommandService, NoteCommandService>();
        services.AddScoped<INoteAssignmentService, NoteAssignmentService>();
        services.AddScoped<INoteWorkflowService, NoteWorkflowService>();
        return services;
    }
}
