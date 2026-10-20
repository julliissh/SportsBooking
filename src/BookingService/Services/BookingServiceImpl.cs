using BookingService.Clients;
using BookingService.Dtos;
using BookingService.Models;
using BookingService.Repositories;

namespace BookingService.Services;

public class BookingServiceImpl : IBookingService
{
    private readonly IBookingRepository _repo;
    private readonly ICatalogClient _catalogClient;
    private readonly ILogger<BookingServiceImpl> _logger;

    public BookingServiceImpl(
        IBookingRepository repo,
        ICatalogClient catalogClient,
        ILogger<BookingServiceImpl> logger)
    {
        _repo = repo;
        _catalogClient = catalogClient;
        _logger = logger;
    }

    public async Task<ServiceResult<BookingListDto>> GetUserBookingsAsync(
        int userId,
        int page,
        int pageSize,
        string? status,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return ServiceResult<BookingListDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["Pagination"] = ["Параметри пагінації некоректні (page >= 1, 1 <= pageSize <= 100)."]
            });
        }

        BookingStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var st))
            {
                return ServiceResult<BookingListDto>.ValidationFailure(new Dictionary<string, string[]>
                {
                    ["status"] = ["Невідомий статус бронювання. Допустимі значення: Confirmed, Cancelled."]
                });
            }
            parsedStatus = st;
        }

        var (items, total) = await _repo.GetPageAsync(page, pageSize, userId: userId, status: parsedStatus, ct: ct);
        var dtos = items.Select(ToDto).ToList();
        return ServiceResult<BookingListDto>.Success(new BookingListDto(dtos, page, pageSize, total));
    }

    public async Task<ServiceResult<BookingListDto>> GetAllBookingsAsync(
        int page,
        int pageSize,
        string? status,
        int? userId,
        int? venueId,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return ServiceResult<BookingListDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["Pagination"] = ["Параметри пагінації некоректні (page >= 1, 1 <= pageSize <= 100)."]
            });
        }

        BookingStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var st))
            {
                return ServiceResult<BookingListDto>.ValidationFailure(new Dictionary<string, string[]>
                {
                    ["status"] = ["Невідомий статус бронювання. Допустимі значення: Confirmed, Cancelled."]
                });
            }
            parsedStatus = st;
        }

        var (items, total) = await _repo.GetPageAsync(page, pageSize, userId: userId, status: parsedStatus, venueId: venueId, ct: ct);
        var dtos = items.Select(ToDto).ToList();
        return ServiceResult<BookingListDto>.Success(new BookingListDto(dtos, page, pageSize, total));
    }

    public async Task<ServiceResult<BookingResponseDto>> GetByIdAsync(
        int id,
        int currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var booking = await _repo.GetByIdAsync(id, ct);
        if (booking is null || (booking.UserId != currentUserId && !isAdmin))
        {
            return ServiceResult<BookingResponseDto>.NotFound(
                "Бронювання не знайдено",
                $"Бронювання з ID {id} не знайдено.");
        }

        return ServiceResult<BookingResponseDto>.Success(ToDto(booking));
    }

    public async Task<ServiceResult<BookingResponseDto>> CreateAsync(
        int currentUserId,
        CreateBookingRequest request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (normalizedKey is not null)
        {
            var existing = await _repo.GetByIdempotencyKeyAsync(currentUserId, normalizedKey, ct);
            if (existing is not null)
            {
                _logger.LogInformation("Idempotent booking hit: bookingId={BookingId}, key={Key}", existing.Id, normalizedKey);
                return ServiceResult<BookingResponseDto>.Success(ToDto(existing));
            }
        }

        var errors = ValidateBusinessRules(request.VenueId, request.StartTime, request.Hours);
        if (errors.Count > 0)
        {
            return ServiceResult<BookingResponseDto>.ValidationFailure(errors);
        }

        var startUtc = NormalizeUtc(request.StartTime);
        var endUtc = startUtc.AddHours(request.Hours);

        var hasOverlap = await _repo.HasOverlappingBookingAsync(request.VenueId, startUtc, endUtc, ct: ct);
        if (hasOverlap)
        {
            return ServiceResult<BookingResponseDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["StartTime"] = ["Обраний час для цього майданчика вже зайнятий."]
            });
        }

        var catalogLookup = await _catalogClient.GetVenueAsync(request.VenueId, ct);
        switch (catalogLookup.Status)
        {
            case CatalogLookupStatus.NotFound:
                return ServiceResult<BookingResponseDto>.BadRequest(
                    "Майданчик не знайдено",
                    $"Майданчик з ID {request.VenueId} не знайдено в CatalogService.");

            case CatalogLookupStatus.TimedOut:
                return ServiceResult<BookingResponseDto>.Unavailable(
                    "Перевищено час очікування CatalogService",
                    "Сервіс каталогу не відповів у межах встановленого таймауту (2 с).");

            case CatalogLookupStatus.Unavailable:
                return ServiceResult<BookingResponseDto>.Unavailable(
                    "Сервіс каталогу недоступний",
                    "Не вдалося зв'язатися з CatalogService.");
        }

        var venue = catalogLookup.Venue!;
        var now = DateTime.UtcNow;

        var booking = new Booking
        {
            UserId = currentUserId,
            VenueId = request.VenueId,
            VenueName = venue.Name,
            VenueAddress = venue.Address,
            PricePerHour = venue.PricePerHour,
            StartTime = startUtc,
            Hours = request.Hours,
            TotalPrice = venue.PricePerHour * request.Hours,
            Status = BookingStatus.Confirmed,
            CreatedAt = now,
            UpdatedAt = now,
            IdempotencyKey = normalizedKey
        };

        await _repo.AddAsync(booking, ct);
        await _repo.SaveChangesAsync(ct);

        _logger.LogInformation("Booking created: id={BookingId}, venueId={VenueId}, userId={UserId}",
            booking.Id, booking.VenueId, booking.UserId);

        return ServiceResult<BookingResponseDto>.Success(ToDto(booking));
    }

    public async Task<ServiceResult<BookingResponseDto>> UpdateAsync(
        int id,
        int currentUserId,
        UpdateBookingRequest request,
        CancellationToken ct = default)
    {
        var booking = await _repo.GetByIdAsync(id, ct);
        if (booking is null || booking.UserId != currentUserId)
        {
            return ServiceResult<BookingResponseDto>.NotFound(
                "Бронювання не знайдено",
                $"Бронювання з ID {id} не знайдено.");
        }

        if (booking.Status != BookingStatus.Confirmed)
        {
            return ServiceResult<BookingResponseDto>.BadRequest(
                "Редагування неможливе",
                "Скасоване бронювання не можна змінити.");
        }

        var errors = ValidateBusinessRules(booking.VenueId, request.StartTime, request.Hours);
        if (errors.Count > 0)
        {
            return ServiceResult<BookingResponseDto>.ValidationFailure(errors);
        }

        var startUtc = NormalizeUtc(request.StartTime);
        var endUtc = startUtc.AddHours(request.Hours);

        var hasOverlap = await _repo.HasOverlappingBookingAsync(
            booking.VenueId, startUtc, endUtc, excludeBookingId: id, ct: ct);

        if (hasOverlap)
        {
            return ServiceResult<BookingResponseDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["StartTime"] = ["Обраний час для цього майданчика вже зайнятий."]
            });
        }

        booking.StartTime = startUtc;
        booking.Hours = request.Hours;
        booking.TotalPrice = booking.PricePerHour * request.Hours;
        booking.UpdatedAt = DateTime.UtcNow;

        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Booking updated: id={BookingId}, userId={UserId}", booking.Id, currentUserId);

        return ServiceResult<BookingResponseDto>.Success(ToDto(booking));
    }

    public async Task<ServiceResult<BookingResponseDto>> CancelAsync(
        int id,
        int currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var booking = await _repo.GetByIdAsync(id, ct);
        if (booking is null || (booking.UserId != currentUserId && !isAdmin))
        {
            return ServiceResult<BookingResponseDto>.NotFound(
                "Бронювання не знайдено",
                $"Бронювання з ID {id} не знайдено.");
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return ServiceResult<BookingResponseDto>.BadRequest(
                "Бронювання вже скасовано",
                $"Бронювання з ID {id} вже має статус Cancelled.");
        }

        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        await _repo.SaveChangesAsync(ct);

        _logger.LogInformation("Booking cancelled: id={BookingId}, byUserId={UserId}", booking.Id, currentUserId);

        return ServiceResult<BookingResponseDto>.Success(ToDto(booking));
    }

    private static DateTime NormalizeUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    private static Dictionary<string, string[]> ValidateBusinessRules(int venueId, DateTime startTime, int hours)
    {
        var errors = new Dictionary<string, string[]>();

        if (venueId <= 0)
            errors["VenueId"] = ["Ідентифікатор майданчика має бути більшим за 0."];

        if (hours < 1 || hours > 24)
            errors["Hours"] = ["Тривалість бронювання має бути від 1 до 24 годин."];

        var startUtc = NormalizeUtc(startTime);
        if (startUtc <= DateTime.UtcNow)
            errors["StartTime"] = ["Дата та час початку бронювання не можуть бути в минулому."];

        return errors;
    }

    private static BookingResponseDto ToDto(Booking b) =>
        new(
            b.Id,
            b.UserId,
            b.VenueId,
            b.VenueName,
            b.VenueAddress,
            b.PricePerHour,
            b.StartTime,
            b.Hours,
            b.TotalPrice,
            b.Status.ToString(),
            b.CreatedAt,
            b.UpdatedAt
        );
}
