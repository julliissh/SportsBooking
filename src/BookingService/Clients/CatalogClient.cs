using System.Net;
using System.Net.Http.Json;

namespace BookingService.Clients;

public class CatalogClient : ICatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CatalogClient> _logger;

    public CatalogClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CatalogClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<CatalogLookupResult> GetVenueAsync(int venueId, CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/catalog/items/{venueId}");
            var correlationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
            }

            using var response = await _httpClient.SendAsync(request, timeoutCts.Token);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Catalog item not found: venueId={VenueId}", venueId);
                return new CatalogLookupResult(CatalogLookupStatus.NotFound);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("CatalogService returned error status {StatusCode} for venueId={VenueId}",
                    (int)response.StatusCode, venueId);
                return new CatalogLookupResult(CatalogLookupStatus.Unavailable);
            }

            var venue = await response.Content.ReadFromJsonAsync<CatalogVenueDto>(cancellationToken: timeoutCts.Token);
            return venue is null
                ? new CatalogLookupResult(CatalogLookupStatus.NotFound)
                : new CatalogLookupResult(CatalogLookupStatus.Found, venue);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogError("CatalogService request timed out (>2s) for venueId={VenueId}", venueId);
            return new CatalogLookupResult(CatalogLookupStatus.TimedOut);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "CatalogService is unreachable while checking venueId={VenueId}", venueId);
            return new CatalogLookupResult(CatalogLookupStatus.Unavailable);
        }
    }

    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            using var response = await _httpClient.GetAsync("/health", timeoutCts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
