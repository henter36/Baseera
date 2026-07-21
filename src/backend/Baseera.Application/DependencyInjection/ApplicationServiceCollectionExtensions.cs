namespace Baseera.Application.DependencyInjection;

using Baseera.Application.Abstractions;
using Baseera.Application.Attachments;
using Baseera.Application.Audit;
using Baseera.Application.CorrectiveActions;
using Baseera.Application.Dashboard;
using Baseera.Application.Escalations;
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
        services.AddScoped<INoteTypeAccessService, NoteTypeAccessService>();
        services.AddScoped<INoteTypeManagementService, NoteTypeManagementService>();
        services.AddScoped<INoteEligibilityService, NoteEligibilityService>();
        services.AddScoped<INoteRoutingService, NoteRoutingService>();
        services.AddScoped<INoteQueryService, NoteQueryService>();
        services.AddScoped<INoteCommandService, NoteCommandService>();
        services.AddScoped<INoteAssignmentService, NoteAssignmentService>();
        services.AddScoped<INoteWorkflowService, NoteWorkflowService>();
        services.AddScoped<ICorrectiveActionScopeService, CorrectiveActionScopeService>();
        services.AddScoped<ICorrectiveActionQueryService, CorrectiveActionQueryService>();
        services.AddScoped<ICorrectiveActionCommandService, CorrectiveActionCommandService>();
        services.AddScoped<ICorrectiveActionAssignmentService, CorrectiveActionAssignmentService>();
        services.AddScoped<ICorrectiveActionWorkflowService, CorrectiveActionWorkflowService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IEscalationPolicyService, EscalationPolicyService>();
        services.AddScoped<IEscalationRuleService>(sp => (EscalationPolicyService)sp.GetRequiredService<IEscalationPolicyService>());
        services.AddScoped<IBackgroundJobLeaseService, BackgroundJobLeaseService>();
        services.AddScoped<IEscalationProcessor, EscalationProcessor>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IEscalationOccurrenceService, EscalationOccurrenceService>();
        services.AddScoped<OperationalDashboardFilterBuilder>();
        services.AddScoped<IOperationalDashboardQueryService, OperationalDashboardQueryService>();
        return services;
    }
}
