using System.Text.Json;
using MoeezMobile.Api.Models.Common;

namespace MoeezMobile.Api.Middleware;

/// <summary>
/// Turns every unhandled exception into the standard response envelope, in the caller's language.
/// BusinessException -> 400 with its own message; everything else -> 500 with a generic message.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessException ex)
        {
            _logger.LogInformation("Business rule rejected: {Key}", ex.Key);
            await WriteAsync(context, StatusCodes.Status400BadRequest, ex.Key, ex.Args);
        }
        catch (NotFoundException ex)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, ex.Key, ex.Args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Path}", context.Request.Path);
            var detail = _env.IsDevelopment() ? new[] { ex.Message } : Array.Empty<string>();
            await WriteAsync(context, StatusCodes.Status500InternalServerError,
                MessageKeys.ServerError, Array.Empty<object>(), detail);
        }
    }

    private static async Task WriteAsync(
        HttpContext context, int statusCode, string messageKey, object[] args, string[]? errors = null)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new ApiResponse<object?>
        {
            Success = false,
            Message = Messages.Resolve(messageKey, RequestLanguage.Of(context), args),
            Data = null,
            Errors = (errors ?? Array.Empty<string>()).ToList()
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
