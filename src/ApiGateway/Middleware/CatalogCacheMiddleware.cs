using System.Text;
using ApiGateway.Services;
using Microsoft.Extensions.Caching.Memory;

namespace ApiGateway.Middleware;

public record CachedHttpResponse(int StatusCode, string? ContentType, byte[] Body);

public class CatalogCacheMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ICatalogCacheService _cacheService;
    private readonly IConfiguration _config;
    private readonly ILogger<CatalogCacheMiddleware> _logger;

    public CatalogCacheMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        ICatalogCacheService cacheService,
        IConfiguration config,
        ILogger<CatalogCacheMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _cacheService = cacheService;
        _config = config;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var isCatalog = path.StartsWithSegments("/api/v1/catalog");

        if (!isCatalog)
        {
            await _next(context);
            return;
        }

        var method = context.Request.Method;

        if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsDelete(method) || HttpMethods.IsPatch(method))
        {
            await _next(context);
            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                _cacheService.Invalidate();
            }
            return;
        }

        if (!HttpMethods.IsGet(method))
        {
            await _next(context);
            return;
        }

        var cacheKey = BuildCacheKey(context.Request);

        if (_cache.TryGetValue(cacheKey, out CachedHttpResponse? cached) && cached is not null)
        {
            _cacheService.RecordHit();
            _logger.LogInformation("Catalog cache HIT for key {CacheKey}", cacheKey);
            context.Response.StatusCode = cached.StatusCode;
            if (!string.IsNullOrEmpty(cached.ContentType))
            {
                context.Response.ContentType = cached.ContentType;
            }
            context.Response.Headers["X-Cache"] = "HIT";
            await context.Response.Body.WriteAsync(cached.Body);
            return;
        }

        _cacheService.RecordMiss();
        _logger.LogInformation("Catalog cache MISS for key {CacheKey}", cacheKey);
        var originalBodyStream = context.Response.Body;
        using var memStream = new MemoryStream();
        context.Response.Body = memStream;

        try
        {
            await _next(context);

            memStream.Seek(0, SeekOrigin.Begin);
            var responseBytes = memStream.ToArray();

            var hasSetCookie = context.Response.Headers.ContainsKey("Set-Cookie");
            if (context.Response.StatusCode == StatusCodes.Status200OK && !hasSetCookie)
            {
                var ttlSeconds = _config.GetValue<int>("Cache:CatalogTtlSeconds", 30);
                var entryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(Math.Max(5, ttlSeconds)))
                    .AddExpirationToken(_cacheService.GetExpirationToken());

                var toCache = new CachedHttpResponse(
                    context.Response.StatusCode,
                    context.Response.ContentType,
                    responseBytes);

                _cache.Set(cacheKey, toCache, entryOptions);
                context.Response.Headers["X-Cache"] = "MISS";
            }

            context.Response.Body = originalBodyStream;
            await context.Response.Body.WriteAsync(responseBytes);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private static string BuildCacheKey(HttpRequest request)
    {
        var sb = new StringBuilder();
        sb.Append("GET:").Append(request.Path.Value);

        if (request.Query.Count > 0)
        {
            sb.Append('?');
            var sortedParams = request.Query
                .OrderBy(q => q.Key, StringComparer.Ordinal)
                .Select(q => $"{q.Key}={q.Value}");
            sb.Append(string.Join('&', sortedParams));
        }

        return sb.ToString();
    }
}
