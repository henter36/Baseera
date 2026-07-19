using Baseera.Api.Auth;
using Baseera.Api.Endpoints;
using Baseera.Api.Middleware;
using Baseera.Application.DependencyInjection;
using Baseera.BackgroundJobs;
using Baseera.Infrastructure.DependencyInjection;
using Baseera.Infrastructure.Persistence;
using Baseera.Reporting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBaseeraApplication();
builder.Services.AddBaseeraInfrastructure(builder.Configuration);
builder.Services.AddBaseeraBackgroundJobs();
builder.Services.AddBaseeraReporting();

var useTestAuth = builder.Configuration.GetValue("Auth:UseTestAuth", builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"));

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

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseMiddleware<CurrentUserMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTimeOffset.UtcNow }));
app.MapBaseeraApi();

var seedDemo = app.Configuration.GetValue("Seed:DemoOrganization", app.Environment.IsDevelopment());
await DatabaseInitializer.InitializeAsync(app.Services, seedDemo);

app.Run();

public partial class Program;
