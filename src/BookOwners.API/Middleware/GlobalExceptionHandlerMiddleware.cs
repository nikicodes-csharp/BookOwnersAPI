using System.Net;
using System.Text.Json;

namespace BookOwners.API.Middleware;

public sealed class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Upstream API request failed");
            await WriteErrorResponse(context, HttpStatusCode.BadGateway, "Upstream API is unavailable.");
        }
        catch (TaskCanceledException ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Request timed out");
            await WriteErrorResponse(context, HttpStatusCode.GatewayTimeout, "The request timed out.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse upstream API response");
            await WriteErrorResponse(context, HttpStatusCode.BadGateway, "Upstream API returned an unexpected response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteErrorResponse(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorResponse(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }
}