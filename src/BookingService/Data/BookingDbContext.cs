using BookingService.Models;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(e =>
        {
            e.HasKey(b => b.Id);

            e.Property(b => b.VenueName)
                .IsRequired()
                .HasMaxLength(200);

            e.Property(b => b.VenueAddress)
                .IsRequired()
                .HasMaxLength(300);

            e.Property(b => b.PricePerHour)
                .HasPrecision(10, 2);

            e.Property(b => b.TotalPrice)
                .HasPrecision(10, 2);

            e.Property(b => b.Status)
                .HasConversion<string>()
                .HasMaxLength(30);

            e.Property(b => b.IdempotencyKey)
                .HasMaxLength(100);

            // Індекси для швидкого пошуку своїх операцій, фільтрації за статусом та перевірки перетинів часу
            e.HasIndex(b => b.UserId);
            e.HasIndex(b => b.VenueId);
            e.HasIndex(b => b.Status);
            e.HasIndex(b => new { b.VenueId, b.StartTime, b.Status });

            // Унікальний індекс на ключ захисту від дублів
            e.HasIndex(b => b.IdempotencyKey)
                .IsUnique();

            // Перенесені зі старої таблиці моноліту історичні записи бронювань
            e.HasData(
                new Booking
                {
                    Id = 1,
                    UserId = 1,
                    VenueId = 1,
                    VenueName = "Central Football Field",
                    VenueAddress = "Main St 1",
                    PricePerHour = 500.00m,
                    StartTime = new DateTime(2026, 9, 12, 18, 0, 0, DateTimeKind.Utc),
                    Hours = 2,
                    TotalPrice = 1000.00m,
                    Status = BookingStatus.Confirmed,
                    CreatedAt = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc),
                    IdempotencyKey = null
                },
                new Booking
                {
                    Id = 3,
                    UserId = 1,
                    VenueId = 2,
                    VenueName = "Tennis Club Kyiv",
                    VenueAddress = "Park Ave 5",
                    PricePerHour = 300.00m,
                    StartTime = new DateTime(2026, 10, 9, 7, 15, 44, DateTimeKind.Utc),
                    Hours = 2,
                    TotalPrice = 600.00m,
                    Status = BookingStatus.Confirmed,
                    CreatedAt = new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc),
                    IdempotencyKey = null
                },
                new Booking
                {
                    Id = 4,
                    UserId = 1,
                    VenueId = 1,
                    VenueName = "Central Football Field",
                    VenueAddress = "Main St 1",
                    PricePerHour = 500.00m,
                    StartTime = new DateTime(2026, 10, 20, 19, 0, 0, DateTimeKind.Utc),
                    Hours = 2,
                    TotalPrice = 1000.00m,
                    Status = BookingStatus.Confirmed,
                    CreatedAt = new DateTime(2026, 10, 15, 15, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 10, 15, 15, 0, 0, DateTimeKind.Utc),
                    IdempotencyKey = null
                }
            );
        });
    }
}
