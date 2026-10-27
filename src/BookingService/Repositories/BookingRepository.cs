using BookingService.Data;
using BookingService.Models;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly BookingDbContext _db;

    public BookingRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<(List<Booking> Items, int TotalCount)> GetPageAsync(
        int page,
        int pageSize,
        int? userId = null,
        BookingStatus? status = null,
        int? venueId = null,
        CancellationToken ct = default)
    {
        var query = _db.Bookings.AsNoTracking().AsQueryable();

        if (userId.HasValue)
            query = query.Where(b => b.UserId == userId.Value);

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        if (venueId.HasValue)
            query = query.Where(b => b.VenueId == venueId.Value);

        query = query.OrderByDescending(b => b.Id);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Booking?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<Booking?> GetByIdempotencyKeyAsync(int userId, string idempotencyKey, CancellationToken ct = default)
        => await _db.Bookings.FirstOrDefaultAsync(
            b => b.UserId == userId && b.IdempotencyKey == idempotencyKey, ct);

    public async Task<bool> HasOverlappingBookingAsync(
        int venueId,
        DateTime startUtc,
        DateTime endUtc,
        int? excludeBookingId = null,
        CancellationToken ct = default)
    {
        var query = _db.Bookings.Where(b =>
            b.VenueId == venueId &&
            b.Status == BookingStatus.Confirmed &&
            b.StartTime < endUtc &&
            b.StartTime.AddHours(b.Hours) > startUtc);

        if (excludeBookingId.HasValue)
            query = query.Where(b => b.Id != excludeBookingId.Value);

        return await query.AnyAsync(ct);
    }

    public async Task AddAsync(Booking booking, CancellationToken ct = default)
        => await _db.Bookings.AddAsync(booking, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
