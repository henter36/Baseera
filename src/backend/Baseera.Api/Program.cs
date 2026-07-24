using Baseera.Api.Auth;
using Baseera.Api.Authorization;
using Baseera.Api.Endpoints;
using Baseera.Api.Health;
using Baseera.Api.Middleware;
using Baseera.Application.DependencyInjection;
using Baseera.Application.Security;
using Baseera.Application.Workspaces;
using Baseera.BackgroundJobs;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.DependencyInjection;
using Baseera.Infrastructure.Persistence;
using Baseera.Reporting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

var useTestAuthFlag = builder.Configuration.GetValue("Auth:UseTestAuth", false);
var seedDemoFlag = builder.Configuration.GetValue("Seed:DemoOrganization", false);
EnvironmentSecurityGuard.EnsureSafeConfiguration(builder.Environment.EnvironmentName, useTestAuthFlag, seedDemoFlag);

var useTestAuth = EnvironmentSecurityGuard.CanEnableTestAuth(builder.Environment.EnvironmentName, useTestAuthFlag);
var seedDemo = EnvironmentSecurityGuard.CanEnableDemoSeed(builder.Environment.EnvironmentName, seedDemoFlag);

EnvironmentSecurityGuard.EnsureEntraConfiguredForRestrictedEnvironments(
    builder.Environment.EnvironmentName,
    useTestAuth,
    builder.Configuration["AzureAd:TenantId"],
    builder.Configuration["AzureAd:ClientId"],
    builder.Configuration["AzureAd:Audience"]);

builder.Services.AddBaseeraApplication();
builder.Services.Configure<WorkspaceFrameworkOptions>(builder.Configuration.GetSection("Workspaces"));
builder.Services.AddBaseeraInfrastructure(builder.Configuration);
builder.Services.AddBaseeraBackgroundJobs();
builder.Services.AddBaseeraReporting();
builder.Services.AddBaseeraHealthChecks(builder.Configuration, builder.Environment, useTestAuth, seedDemo);

if (useTestAuth)
{
    builder.Services.AddAuthentication(TestAuthConstants.Scheme)
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
            TestAuthConstants.Scheme, _ => { });
}
else
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
}

builder.Services.AddBaseeraAuthorizationPolicies();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseMiddleware<CurrentUserMiddleware>();
app.UseMiddleware<ProvisionedUserMiddleware>();
app.UseAuthorization();

app.MapBaseeraHealthEndpoints();
app.MapBaseeraApi();

var applyMigrations = app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"));
await DatabaseInitializer.InitializeAsync(app.Services, seedDemo, applyMigrations);

app.Run();

public partial class Program;
