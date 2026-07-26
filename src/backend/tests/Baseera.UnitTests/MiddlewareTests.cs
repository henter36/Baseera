namespace Baseera.UnitTests;

using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Baseera.Api.Middleware;
using Baseera.Application.Forms.Responses;
using Baseera.Application.Workforce;
using Baseera.Infrastructure.Identity;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class MiddlewareTests
{
    [Theory]
    [MemberData(nameof(ExceptionMappings))]
    public async Task Exception_middleware_maps_known_exceptions(Exception exception, HttpStatusCode status, string title)
    {
        var context = CreateContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.FromException(exception),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal((int)status, context.Response.StatusCode);
        using var document = await ReadJsonAsync(context);
        Assert.Equal(title, document.RootElement.GetProperty("title").GetString());
        Assert.Equal((int)status, document.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Workforce_validation_exception_maps_to_bad_request()
    {
        var context = CreateContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.FromException(new WorkforceValidationException("بيانات القوى البشرية غير صالحة.")),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var document = await ReadJsonAsync(context);
        Assert.Equal("أخطاء تحقق", document.RootElement.GetProperty("title").GetString());
        Assert.Equal("بيانات القوى البشرية غير صالحة.", document.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Request_aborted_cancellation_is_not_converted_to_application_error()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = CreateContext();
        context.RequestAborted = cancellation.Token;
        var middleware = new ExceptionHandlingMiddleware(
            httpContext => Task.FromCanceled(httpContext.RequestAborted),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.NotEqual(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task Form_response_validation_maps_to_unprocessable_entity_with_issues()
    {
        var issue = new FormResponseValidationIssueDto("Required", "$.field", "field", "الحقل مطلوب.", "Error");
        var context = CreateContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.FromException(new FormResponseValidationException([issue])),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, context.Response.StatusCode);
        using var document = await ReadJsonAsync(context);
        Assert.Equal("أخطاء تحقق", document.RootElement.GetProperty("title").GetString());
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("issues").GetArrayLength());
    }

    [Fact]
    public async Task Fluent_validation_exception_maps_to_bad_request_errors()
    {
        var context = CreateContext();
        var failures = new[]
        {
            new ValidationFailure("name", "الاسم مطلوب.")
        };
        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.FromException(new ValidationException(failures)),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var document = await ReadJsonAsync(context);
        Assert.Equal("خطأ في التحقق", document.RootElement.GetProperty("title").GetString());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Provisioned_user_middleware_rejects_authenticated_unprovisioned_principal()
    {
        var context = CreateContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "subject")], "test"));
        var currentUser = new CurrentUser(new HttpContextAccessor { HttpContext = context });
        var middleware = new ProvisionedUserMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, currentUser);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        using var document = await ReadJsonAsync(context);
        Assert.Equal("غير مصرح", document.RootElement.GetProperty("title").GetString());
        Assert.Equal(StatusCodes.Status403Forbidden, document.RootElement.GetProperty("status").GetInt32());
    }

    public static TheoryData<Exception, HttpStatusCode, string> ExceptionMappings()
    {
        return new TheoryData<Exception, HttpStatusCode, string>
        {
            { new UnauthorizedAccessException("denied"), HttpStatusCode.Forbidden, "ممنوع" },
            { new KeyNotFoundException("missing"), HttpStatusCode.NotFound, "غير موجود" },
            { new ArgumentException("bad"), HttpStatusCode.BadRequest, "طلب غير صالح" },
            { new InvalidOperationException("conflict"), HttpStatusCode.Conflict, "تعارض" },
            { new Exception("boom"), HttpStatusCode.InternalServerError, "خطأ داخلي" }
        };
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }
}
