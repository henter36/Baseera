namespace Baseera.Application.Security;

/// <summary>
/// Fail-fast environment policy: TestAuth and Demo Seed require BOTH an allowlisted environment
/// AND an explicit configuration flag. A boolean alone is never sufficient.
/// </summary>
public static class EnvironmentSecurityGuard
{
    public static bool IsAllowlistedForTestFeatures(string environmentName) =>
        string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);

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

    public static bool CanEnableTestAuth(string environmentName, bool useTestAuthFlag) =>
        useTestAuthFlag && IsAllowlistedForTestFeatures(environmentName);

    public static bool CanEnableDemoSeed(string environmentName, bool seedFlag) =>
        seedFlag && IsAllowlistedForTestFeatures(environmentName);
}
