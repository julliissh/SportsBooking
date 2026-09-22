using Microsoft.EntityFrameworkCore;
using Monolith.Models;

namespace Monolith.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Booking> Bookings => Set<Booking>();
}
