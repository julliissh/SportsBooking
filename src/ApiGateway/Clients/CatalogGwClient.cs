using System.Net;
using System.Net.Http.Json;
using ApiGateway.Dtos;
using ApiGateway.Services;
using Microsoft.Extensions.Caching.Memory;

namespace ApiGateway.Clients;

public class CatalogGwClient : ICatalogGwClient
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ICatalogCacheService _cacheService;
    private readonly IConfiguration _config;
    private readonly ILogger<CatalogGwClient> _logger;

    public CatalogGwClient(
        HttpClient http,
        IMemoryCache cache,
        ICatalogCacheService cacheService,
        IConfiguration config,
        ILogger<CatalogGwClient> logger)
    {
        _http = http;
        _cache = cache;
        _cacheService = cacheService;
        _config = config;
        _logger = logger;
    }

    public async Task<GwResult<VenueDto>> GetVenueAsync(
        int venueId,
        string correlationId,
        CancellationToken ct = default)
    {
        var cacheKey = $"venue:{venueId}";
        if (_cache.TryGetValue(cacheKey, out VenueDto? cachedVenue) && cachedVenue is not null)
        {
            _cacheService.RecordHit();
            return new GwResult<VenueDto>(GwStatus.Ok, cachedVenue);
        }

        _cacheService.RecordMiss();

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"/api/v1/catalog/items/{venueId}");

            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

            using var response = await _http.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new GwResult<VenueDto>(GwStatus.NotFound);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new GwResult<VenueDto>(GwStatus.Unavailable);
            }

            var dto = await response.Content.ReadFromJsonAsync<VenueDto>(cancellationToken: ct);
            if (dto is null)
            {
                return new GwResult<VenueDto>(GwStatus.NotFound);
            }

            var ttlSeconds = _config.GetValue<int>("Cache:CatalogTtlSeconds", 30);
            var entryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(ttlSeconds))
                .AddExpirationToken(_cacheService.GetExpirationToken());

            _cache.Set(cacheKey, dto, entryOptions);

            return new GwResult<VenueDto>(GwStatus.Ok, dto);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or TimeoutException)
        {
            _logger.LogError(ex, "CatalogService unavailable for venue {VenueId}", venueId);
            return new GwResult<VenueDto>(GwStatus.Unavailable);
        }
    }

    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            using var resp = await _http.GetAsync("/health", cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
