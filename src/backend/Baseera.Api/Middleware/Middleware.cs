namespace Baseera.Api.Middleware;

using Baseera.Application.Workforce;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var values) && !string.IsNullOrWhiteSpace(values)
            ? values.ToString()
            : Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        await next(context);
    }
}

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "ممنوع", ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "غير موجود", ex.Message);
        }
        catch (ArgumentException ex)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "طلب غير صالح",
                ex.Message);
        }
        catch (WorkforceValidationException ex)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "أخطاء تحقق",
                ex.Message);
        }
        catch (OperationCanceledException ex)
            when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Request processing was canceled by the client.");
        }
        catch (Baseera.Application.Forms.Responses.FormResponseConflictException ex)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(ex.Payload, context.RequestAborted);
        }
        catch (Baseera.Application.Forms.Responses.FormResponseValidationException ex)
        {
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    type = "about:blank",
                    title = "أخطاء تحقق",
                    status = StatusCodes.Status422UnprocessableEntity,
                    issues = ex.Issues
                },
                context.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "تعارض", ex.Message);
        }
        catch (FluentValidation.ValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    title = "خطأ في التحقق",
                    status = StatusCodes.Status400BadRequest,
                    errors
                },
                context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "خطأ داخلي", "حدث خطأ غير متوقع.");
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(
            new
            {
                type = "about:blank",
                title,
                status,
                detail
            },
            context.RequestAborted);
    }
}

public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, Baseera.Infrastructure.Identity.UserProvisioningService provisioning, Baseera.Infrastructure.Identity.CurrentUser currentUser)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var state = await provisioning.ResolveAsync(context.User, context.RequestAborted);
            currentUser.SetState(state);
        }

        await next(context);
    }
}

public sealed class ProvisionedUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, Baseera.Infrastructure.Identity.CurrentUser currentUser)
    {
        if (context.User.Identity?.IsAuthenticated == true && !currentUser.IsAuthenticated)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    title = "غير مصرح",
                    detail = "الحساب غير مُفعّل أو غير مُهيّأ في المنصة.",
                    status = StatusCodes.Status403Forbidden
                },
                context.RequestAborted);
            return;
        }

        await next(context);
    }
}
