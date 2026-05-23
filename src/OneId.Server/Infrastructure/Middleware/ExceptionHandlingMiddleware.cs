using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace OneId.Server.Infrastructure.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (context.Response.HasStarted) return;
            await WriteProblemAsync(context, StatusCodes.Status409Conflict,
                "https://httpstatuses.io/409", "Conflict", 409,
                "The resource was modified by another request. Reload and retry.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
            if (context.Response.HasStarted) return;
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "https://httpstatuses.io/500", "Internal Server Error", 500,
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context, int statusCode, string type, string title, int status, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var problem = new { type, title, status, detail };
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
