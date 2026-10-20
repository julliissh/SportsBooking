using BookingService.Models;

namespace BookingService.Repositories;

public interface IBookingRepository
{
    Task<(List<Booking> Items, int TotalCount)> GetPageAsync(
        int page,
        int pageSize,
        int? userId = null,
        BookingStatus? status = null,
        int? venueId = null,
        CancellationToken ct = default);

    Task<Booking?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<Booking?> GetByIdempotencyKeyAsync(int userId, string idempotencyKey, CancellationToken ct = default);

    Task<bool> HasOverlappingBookingAsync(
        int venueId,
        DateTime startUtc,
        DateTime endUtc,
        int? excludeBookingId = null,
        CancellationToken ct = default);

    Task AddAsync(Booking booking, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
