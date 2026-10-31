namespace ApiGateway.Dtos;

public enum GwStatus
{
    Ok,
    NotFound,
    Unavailable
}

public record GwResult<T>(GwStatus Status, T? Data = default);

public record VenueDto(
    int Id,
    string Name,
    string Address,
    decimal PricePerHour,
    string? Description,
    string Category
);

public record ActiveBookingDto(
    int Id,
    int VenueId,
    DateTime StartTime,
    int Hours,
    decimal TotalPrice,
    string Status,
    DateTime CreatedAt
);

public record VenueDetailsAggDto(
    VenueDto Venue,
    ActiveBookingDto? MyActiveBooking,
    string BookingStatus
);
