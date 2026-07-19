namespace Baseera.Reporting;

using Microsoft.Extensions.DependencyInjection;

public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddBaseeraReporting(this IServiceCollection services)
    {
        // Reporting engines are introduced in later phases.
        return services;
    }
}
