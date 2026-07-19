namespace Baseera.Application.DependencyInjection;

using Baseera.Application.Abstractions;
using Baseera.Application.Attachments;
using Baseera.Application.Audit;
using Baseera.Application.Identity;
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
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IAttachmentAppService, AttachmentAppService>();
        return services;
    }
}
