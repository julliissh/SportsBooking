using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ApiGateway.Dtos;

namespace ApiGateway.Clients;

public class BookingGwClient : IBookingGwClient
{
    private readonly HttpClient _http;
    private readonly ILogger<BookingGwClient> _logger;

    public BookingGwClient(HttpClient http, ILogger<BookingGwClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<GwResult<ActiveBookingDto>> GetMyActiveAsync(
        int venueId,
        string userId,
        string bearerToken,
        string correlationId,
        CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/bookings/my-active?venueId={venueId}");

            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                request.Headers.TryAddWithoutValidation("X-User-Id", userId);
            }

            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
            }

            using var response = await _http.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.NotFound)
            {
                return new GwResult<ActiveBookingDto>(GwStatus.NotFound);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new GwResult<ActiveBookingDto>(GwStatus.Unavailable);
            }

            var dto = await response.Content.ReadFromJsonAsync<ActiveBookingDto>(cancellationToken: ct);
            return dto is null
                ? new GwResult<ActiveBookingDto>(GwStatus.NotFound)
                : new GwResult<ActiveBookingDto>(GwStatus.Ok, dto);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or TimeoutException)
        {
            _logger.LogError(ex, "BookingService unavailable for venue {VenueId}", venueId);
            return new GwResult<ActiveBookingDto>(GwStatus.Unavailable);
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
