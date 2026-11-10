namespace RP.Rag.Web.Middleware;

/// <summary>
/// Global exception handling middleware for non-interactive HTTP requests.
/// For Blazor interactive errors, use ErrorBoundary in Routes.razor.
/// </summary>
public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            logger.LogError(ex, "Unhandled exception while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            // Redirect to error page instead of showing raw JSON for user-friendly UI
            context.Response.Redirect($"/error?message={Uri.EscapeDataString(ex.Message)}");
        }
    }
}