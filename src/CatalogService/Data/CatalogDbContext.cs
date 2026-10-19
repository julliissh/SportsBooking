using CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Venue>(e =>
        {
            e.Property(v => v.Name).HasMaxLength(200);
            e.Property(v => v.Address).HasMaxLength(300);
            e.Property(v => v.PricePerHour).HasPrecision(10, 2);
            e.HasIndex(v => v.Name);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100);
        });

      
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Football" },
            new Category { Id = 2, Name = "Tennis" },
            new Category { Id = 3, Name = "Basketball" }
        );

   
        modelBuilder.Entity<Venue>().HasData(
            new Venue { Id = 1, Name = "Central Football Field", Address = "Main St 1", PricePerHour = 500, CategoryId = 1, IsActive = true },
            new Venue { Id = 2, Name = "Tennis Club Kyiv", Address = "Park Ave 5", PricePerHour = 300, CategoryId = 2, IsActive = true },
            new Venue { Id = 3, Name = "Basketball Arena", Address = "Sports Blvd 10", PricePerHour = 400, CategoryId = 3, IsActive = true }
        );
    }
}
