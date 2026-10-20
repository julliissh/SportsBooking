using System.ComponentModel.DataAnnotations;

namespace BookingService.Dtos;

/// <summary>
/// Запит на створення нового бронювання спортивного майданчика.
/// UserId не передається у тілі — визначається виключно з JWT-токена.
/// </summary>
public class CreateBookingRequest
{
    /// <summary>Ідентифікатор майданчика з CatalogService (наприклад, 1).</summary>
    /// <example>1</example>
    [Required]
    public int VenueId { get; set; }

    /// <summary>Дата та час початку бронювання в UTC (має бути в майбутньому).</summary>
    /// <example>2027-05-10T14:00:00Z</example>
    [Required]
    public DateTime StartTime { get; set; }

    /// <summary>Тривалість оренди в годинах (від 1 до 24).</summary>
    /// <example>2</example>
    [Required]
    public int Hours { get; set; }
}

/// <summary>
/// Запит на зміну часу або тривалості існуючого підтвердженого бронювання.
/// </summary>
public class UpdateBookingRequest
{
    /// <summary>Нова дата та час початку бронювання в UTC (має бути в майбутньому).</summary>
    /// <example>2027-05-11T16:00:00Z</example>
    [Required]
    public DateTime StartTime { get; set; }

    /// <summary>Нова тривалість оренди в годинах (від 1 до 24).</summary>
    /// <example>3</example>
    [Required]
    public int Hours { get; set; }
}

/// <summary>
/// Детальна інформація про операцію бронювання разом зі знімком майданчика (ItemSnapshot).
/// </summary>
public record BookingResponseDto(
    int Id,
    int UserId,
    int VenueId,
    string VenueName,
    string VenueAddress,
    decimal PricePerHour,
    DateTime StartTime,
    int Hours,
    decimal TotalPrice,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Сторінка списку бронювань із метаданими пагінації.
/// </summary>
public record BookingListDto(
    List<BookingResponseDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);
