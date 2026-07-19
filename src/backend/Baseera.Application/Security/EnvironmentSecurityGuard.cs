namespace Baseera.Application.Security;

/// <summary>
/// Fail-fast guards for unsafe auth/seed and incomplete Entra production configuration.
/// </summary>
public static class EnvironmentSecurityGuard
{
    private static readonly HashSet<string> Allowlisted = new(StringComparer.OrdinalIgnoreCase)
    {
        "Development",
        "Testing"
    };

    public static bool IsAllowlistedForTestFeatures(string environmentName) =>
        Allowlisted.Contains(environmentName);

    public static bool CanEnableTestAuth(string environmentName, bool useTestAuthFlag) =>
        useTestAuthFlag && IsAllowlistedForTestFeatures(environmentName);

    public static bool CanEnableDemoSeed(string environmentName, bool seedDemoFlag) =>
        seedDemoFlag && IsAllowlistedForTestFeatures(environmentName);

    public static void EnsureSafeConfiguration(string environmentName, bool useTestAuth, bool seedDemoOrganization)
    {
        var allowlisted = IsAllowlistedForTestFeatures(environmentName);
        if (useTestAuth && !allowlisted)
        {
            throw new InvalidOperationException(
                $"Auth:UseTestAuth is enabled but environment '{environmentName}' is not Development or Testing. Startup aborted.");
        }

        if (seedDemoOrganization && !allowlisted)
        {
            throw new InvalidOperationException(
                $"Seed:DemoOrganization is enabled but environment '{environmentName}' is not Development or Testing. Startup aborted.");
        }
    }

    /// <summary>
    /// Production/Staging with Entra mode require real Tenant/Client (no placeholders).
    /// </summary>
    public static void EnsureEntraConfiguredForRestrictedEnvironments(
        string environmentName,
        bool useTestAuth,
        string? tenantId,
        string? clientId,
        string? audience)
    {
        if (useTestAuth || IsAllowlistedForTestFeatures(environmentName))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(tenantId) || tenantId.Contains("YOUR_", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(clientId) || clientId.Contains("YOUR_", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(audience) || audience.Contains("YOUR_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"AzureAd TenantId/ClientId/Audience must be configured for environment '{environmentName}'. Startup aborted.");
        }
    }
}
