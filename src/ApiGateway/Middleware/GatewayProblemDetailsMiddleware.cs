using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Middleware;

/// <summary>
/// Забезпечує єдиний формат відповідей ProblemDetails (RFC 7807) для всіх помилок шлюзу (>= 400),
/// включно з таймаутами downstream, збоями Ocelot та помилками маршрутизації.
/// </summary>
public class GatewayProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayProblemDetailsMiddleware> _logger;

    public GatewayProblemDetailsMiddleware(
        RequestDelegate next,
        ILogger<GatewayProblemDetailsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        // Не перехоплюємо службові ендпоінти Swagger або Health Check
        if (path.StartsWithSegments("/swagger") || path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var originalBodyStream = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            memoryStream.Seek(0, SeekOrigin.Begin);
            var statusCode = context.Response.StatusCode;
            var contentType = context.Response.ContentType;

            if (statusCode >= StatusCodes.Status400BadRequest)
            {
                string rawBody;
                using (var reader = new StreamReader(memoryStream, Encoding.UTF8, leaveOpen: true))
                {
                    rawBody = await reader.ReadToEndAsync();
                }

                var isProblemJson = contentType != null &&
                    contentType.Contains("problem+json", StringComparison.OrdinalIgnoreCase);

                var isAlreadyProblemJson = isProblemJson || (
                    contentType != null &&
                    contentType.Contains("json", StringComparison.OrdinalIgnoreCase) &&
                    rawBody.Contains("\"status\"", StringComparison.OrdinalIgnoreCase) &&
                    (rawBody.Contains("\"title\"", StringComparison.OrdinalIgnoreCase) ||
                     rawBody.Contains("\"type\"", StringComparison.OrdinalIgnoreCase)));

                if (isAlreadyProblemJson)
                {
                    // Вже є ProblemDetails, гарантуємо заголовок Content-Type: application/problem+json
                    context.Response.Body = originalBodyStream;
                    context.Response.ContentType = "application/problem+json";
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await memoryStream.CopyToAsync(originalBodyStream);
                    return;
                }

                var title = statusCode switch
                {
                    StatusCodes.Status400BadRequest => "Bad Request",
                    StatusCodes.Status401Unauthorized => "Unauthorized",
                    StatusCodes.Status403Forbidden => "Forbidden",
                    StatusCodes.Status404NotFound => "Not Found",
                    StatusCodes.Status408RequestTimeout => "Request Timeout",
                    StatusCodes.Status429TooManyRequests => "Too Many Requests",
                    StatusCodes.Status500InternalServerError => "Internal Server Error",
                    StatusCodes.Status502BadGateway => "Bad Gateway",
                    StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
                    StatusCodes.Status504GatewayTimeout => "Gateway Timeout",
                    _ => "Gateway Error"
                };

                var detail = !string.IsNullOrWhiteSpace(rawBody) && rawBody.Length < 300 && !rawBody.Contains("<html", StringComparison.OrdinalIgnoreCase)
                    ? rawBody.Trim()
                    : $"An error occurred on the gateway with status code {statusCode}.";

                var type = statusCode switch
                {
                    StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                    StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                    StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                    StatusCodes.Status429TooManyRequests => "https://tools.ietf.org/html/rfc6585#section-4",
                    StatusCodes.Status503ServiceUnavailable => "https://tools.ietf.org/html/rfc9110#section-15.6.4",
                    _ => "https://tools.ietf.org/html/rfc9110"
                };

                var problem = new ProblemDetails
                {
                    Type = type,
                    Status = statusCode,
                    Title = title,
                    Detail = detail,
                    Instance = context.Request.Path
                };

                _logger.LogInformation("Unified gateway response status {Status} as ProblemDetails for {Path}", statusCode, context.Request.Path);

                context.Response.Body = originalBodyStream;
                context.Response.ContentType = "application/problem+json";
                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(problem);
                context.Response.ContentLength = jsonBytes.Length;
                await context.Response.Body.WriteAsync(jsonBytes);
            }
            else
            {
                context.Response.Body = originalBodyStream;
                memoryStream.Seek(0, SeekOrigin.Begin);
                await memoryStream.CopyToAsync(originalBodyStream);
            }
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}
