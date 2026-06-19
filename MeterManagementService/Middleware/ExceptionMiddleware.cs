// ???????????????????????????????????????????????????????????????????????????
// MeterManagementService/Middleware/ExceptionMiddleware.cs
// ???????????????????????????????????????????????????????????????????????????
// Catches ALL unhandled exceptions and converts them to RFC 7807
// ProblemDetails JSON. Without this, ASP.NET returns HTML error pages
// which clients cannot parse.

using System.Text.Json;

namespace MeterManagementService.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate    _next;
    private readonly ILogger<ExceptionMiddleware> _log;
    private readonly IHostEnvironment   _env;

    public ExceptionMiddleware(RequestDelegate next,
        ILogger<ExceptionMiddleware> log, IHostEnvironment env)
    { _next = next; _log = log; _env = env; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception on {Method} {Path}",
                ctx.Request.Method, ctx.Request.Path);
            await HandleAsync(ctx, ex);
        }
    }

    private async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        ctx.Response.ContentType = "application/problem+json";
        var (code, title) = ex switch
        {
            InvalidOperationException   => (409, "Conflict"),
            ArgumentException           => (400, "Bad Request"),
            KeyNotFoundException        => (404, "Not Found"),
            UnauthorizedAccessException => (401, "Unauthorized"),
            _                           => (500, "Internal Server Error")
        };
        ctx.Response.StatusCode = code;
        var body = new
        {
            status   = code,
            title    = title,
            detail   = ex.Message,
            instance = ctx.Request.Path.Value,
            trace    = _env.IsDevelopment() ? ex.StackTrace : null
        };
        await ctx.Response.WriteAsync(
            JsonSerializer.Serialize(body, new JsonSerializerOptions
                { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
