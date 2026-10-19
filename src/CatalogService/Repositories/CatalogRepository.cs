using CatalogService.Data;
using CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Repositories;

public class CatalogRepository : ICatalogRepository
{
    private readonly CatalogDbContext _db;

    public CatalogRepository(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<(List<Venue> Items, int TotalCount)> GetPageAsync(
        int page, int pageSize, string? search, int? categoryId, string? sortBy, bool desc)
    {
        var query = _db.Venues
            .Include(v => v.Category)
            .Where(v => v.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(v => v.Name.Contains(search) || v.Address.Contains(search));

        if (categoryId.HasValue)
            query = query.Where(v => v.CategoryId == categoryId.Value);

        query = (sortBy?.ToLower(), desc) switch
        {
            ("price", false)  => query.OrderBy(v => v.PricePerHour),
            ("price", true)   => query.OrderByDescending(v => v.PricePerHour),
            ("name", true)    => query.OrderByDescending(v => v.Name),
            _                 => query.OrderBy(v => v.Name)
        };

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Venue?> GetByIdAsync(int id)
        => await _db.Venues
            .Include(v => v.Category)
            .FirstOrDefaultAsync(v => v.Id == id && v.IsActive);

    public async Task<bool> CategoryExistsAsync(int categoryId)
        => await _db.Categories.AnyAsync(c => c.Id == categoryId);

    public async Task AddAsync(Venue venue)
        => await _db.Venues.AddAsync(venue);

    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();

    public async Task<List<Category>> GetCategoriesAsync()
        => await _db.Categories.OrderBy(c => c.Name).ToListAsync();
}
