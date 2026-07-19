namespace Baseera.Api.Auth;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Baseera.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

public static class TestAuthConstants
{
    public const string Scheme = "Test";
}

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHostEnvironment environment) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!EnvironmentSecurityGuard.IsAllowlistedForTestFeatures(environment.EnvironmentName))
        {
            return Task.FromResult(AuthenticateResult.Fail("TestAuth is disabled outside Development/Testing."));
        }

        if (!Request.Headers.TryGetValue("X-Test-User", out var userHeader) || string.IsNullOrWhiteSpace(userHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var subject = userHeader.ToString();
        var claims = new List<Claim>
        {
            new("oid", subject),
            new(ClaimTypes.NameIdentifier, subject),
            new("name", Request.Headers["X-Test-DisplayName"].FirstOrDefault() ?? subject),
            new(ClaimTypes.Email, Request.Headers["X-Test-Email"].FirstOrDefault() ?? $"{subject}@test.local")
        };

        var identity = new ClaimsIdentity(claims, TestAuthConstants.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, TestAuthConstants.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
