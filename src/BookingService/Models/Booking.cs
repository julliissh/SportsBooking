namespace BookingService.Models;

/// <summary>
/// Статус операції бронювання майданчика.
/// </summary>
public enum BookingStatus
{
    Confirmed,
    Cancelled
}

/// <summary>
/// Головна сутність операційного сервісу бронювань.
/// Містить денормалізований знімок об'єкта каталогу (VenueName, VenueAddress, PricePerHour)
/// на момент оформлення операції, без зовнішніх ключів до бази каталогу.
/// </summary>
public class Booking
{
    public int Id { get; set; }

    /// <summary>Ідентифікатор користувача з JWT-токена.</summary>
    public int UserId { get; set; }

    /// <summary>Ідентифікатор спортивного майданчика в CatalogService (без FK).</summary>
    public int VenueId { get; set; }

    /// <summary>Знімок назви майданчика на момент бронювання (ItemSnapshot).</summary>
    public string VenueName { get; set; } = string.Empty;

    /// <summary>Знімок адреси майданчика на момент бронювання (ItemSnapshot).</summary>
    public string VenueAddress { get; set; } = string.Empty;

    /// <summary>Знімок ціни за годину на момент бронювання (ItemSnapshot).</summary>
    public decimal PricePerHour { get; set; }

    /// <summary>Дата та час початку бронювання (UTC).</summary>
    public DateTime StartTime { get; set; }

    /// <summary>Тривалість оренди в годинах (1..24).</summary>
    public int Hours { get; set; }

    /// <summary>Загальна вартість бронювання (PricePerHour * Hours).</summary>
    public decimal TotalPrice { get; set; }

    /// <summary>Поточний стан операції (Confirmed / Cancelled).</summary>
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;

    /// <summary>Час створення запису (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Час останнього оновлення запису (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Унікальний ключ ідемпотентності для захисту від повторного створення.</summary>
    public string? IdempotencyKey { get; set; }
}
