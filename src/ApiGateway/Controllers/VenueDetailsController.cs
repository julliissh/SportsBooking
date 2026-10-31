using System.Security.Claims;
using ApiGateway.Clients;
using ApiGateway.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api/gateway/venues")]
[Authorize]
[Produces("application/json")]
public class VenueDetailsController : ControllerBase
{
    private readonly ICatalogGwClient _catalogClient;
    private readonly IBookingGwClient _bookingClient;
    private readonly ILogger<VenueDetailsController> _logger;

    public VenueDetailsController(
        ICatalogGwClient catalogClient,
        IBookingGwClient bookingClient,
        ILogger<VenueDetailsController> logger)
    {
        _catalogClient = catalogClient;
        _bookingClient = bookingClient;
        _logger = logger;
    }

    /// <summary>
    /// Отримати деталі майданчика та статус власного активного бронювання (паралельний складений запит).
    /// </summary>
    /// <remarks>
    /// Приклад успішної відповіді з активним бронюванням (200 OK):
    /// <code>
    /// {
    ///   "venue": {
    ///     "id": 1,
    ///     "name": "Olympic Arena",
    ///     "address": "вул. Спортивна, 1",
    ///     "pricePerHour": 450.00,
    ///     "description": "Сучасний спорткомплекс",
    ///     "category": "Multipurpose"
    ///   },
    ///   "myActiveBooking": {
    ///     "id": 101,
    ///     "venueId": 1,
    ///     "startTime": "2027-06-01T10:00:00Z",
    ///     "hours": 2,
    ///     "totalPrice": 900.00,
    ///     "status": "Confirmed",
    ///     "createdAt": "2026-10-25T14:30:00Z"
    ///   },
    ///   "bookingStatus": "active"
    /// }
    /// </code>
    ///
    /// Приклад часткової деградації при недоступності BookingService (200 OK):
    /// <code>
    /// {
    ///   "venue": { ... },
    ///   "myActiveBooking": null,
    ///   "bookingStatus": "unavailable"
    /// }
    /// </code>
    ///
    /// Приклад помилки недоступності CatalogService (503 Service Unavailable):
    /// <code>
    /// {
    ///   "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
    ///   "title": "Catalog service unavailable",
    ///   "status": 503,
    ///   "detail": "The catalog service is currently unreachable or timed out.",
    ///   "instance": "/api/gateway/venues/1/details"
    /// }
    /// </code>
    ///
    /// Приклад не знайденого майданчика (404 Not Found):
    /// <code>
    /// {
    ///   "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
    ///   "title": "Venue not found",
    ///   "status": 404,
    ///   "detail": "Venue with id 999 was not found in catalog.",
    ///   "instance": "/api/gateway/venues/999/details"
    /// }
    /// </code>
    ///
    /// Приклад перевищення ліміту (429 Too Many Requests):
    /// <code>
    /// {
    ///   "type": "https://tools.ietf.org/html/rfc6585#section-4",
    ///   "title": "Too Many Requests",
    ///   "status": 429,
    ///   "detail": "Rate limit exceeded. Please retry later.",
    ///   "instance": "/api/gateway/venues/1/details"
    /// }
    /// </code>
    /// </remarks>
    [HttpGet("{venueId:int}/details")]
    [ProducesResponseType<VenueDetailsAggDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDetails(int venueId, CancellationToken ct)
    {
        var correlationId = HttpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            HttpContext.Request.Headers["X-Correlation-Id"] = correlationId;
        }

        var userId = User.FindFirstValue("sub")
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? string.Empty;

        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        var bearerToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..].Trim()
            : string.Empty;

        var venueTask = _catalogClient.GetVenueAsync(venueId, correlationId, ct);
        var bookingTask = _bookingClient.GetMyActiveAsync(venueId, userId, bearerToken, correlationId, ct);

        _logger.LogInformation("Dispatched parallel requests for venue {VenueId}", venueId);

        await Task.WhenAll(venueTask, bookingTask);

        var venueResult = venueTask.Result;
        var bookingResult = bookingTask.Result;

        if (venueResult.Status == GwStatus.NotFound)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Venue not found",
                Detail = $"Venue with id {venueId} was not found in catalog.",
                Instance = HttpContext.Request.Path
            });
        }

        if (venueResult.Status == GwStatus.Unavailable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Catalog service unavailable",
                Detail = "The catalog service is currently unreachable or timed out.",
                Instance = HttpContext.Request.Path
            });
        }

        var bookingStatus = bookingResult.Status switch
        {
            GwStatus.Ok => "active",
            GwStatus.NotFound => "none",
            GwStatus.Unavailable => "unavailable",
            _ => "none"
        };

        var responseDto = new VenueDetailsAggDto(
            Venue: venueResult.Data!,
            MyActiveBooking: bookingResult.Data,
            BookingStatus: bookingStatus);

        return Ok(responseDto);
    }
}
