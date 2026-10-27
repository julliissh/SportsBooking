namespace BookingService.Clients;

public record CatalogVenueDto(
    int Id,
    string Name,
    string Address,
    decimal PricePerHour,
    string Category
);

public enum CatalogLookupStatus
{
    Found,
    NotFound,
    TimedOut,
    Unavailable
}

public record CatalogLookupResult(
    CatalogLookupStatus Status,
    CatalogVenueDto? Venue = null
);

public interface ICatalogClient
{
    Task<CatalogLookupResult> GetVenueAsync(int venueId, CancellationToken ct = default);
    Task<bool> CheckHealthAsync(CancellationToken ct = default);
}
