using CatalogService.Data;
using CatalogService.Dtos;
using CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Services;

public class CatalogServiceImpl : ICatalogService
{
    private readonly CatalogDbContext _db;
    private readonly ILogger<CatalogServiceImpl> _logger;

    public CatalogServiceImpl(CatalogDbContext db, ILogger<CatalogServiceImpl> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<VenueListDto> GetVenuesAsync(int page, int pageSize, string? search, int? categoryId, string? sortBy, bool desc)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Venues
            .Include(v => v.Category)
            .Where(v => v.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(v => v.Name.Contains(search) || v.Address.Contains(search));

        if (categoryId.HasValue)
            query = query.Where(v => v.CategoryId == categoryId.Value);

        query = (sortBy?.ToLower(), desc) switch
        {
            ("price", false) => query.OrderBy(v => v.PricePerHour),
            ("price", true) => query.OrderByDescending(v => v.PricePerHour),
            ("name", true) => query.OrderByDescending(v => v.Name),
            _ => query.OrderBy(v => v.Name)
        };

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VenueDto(v.Id, v.Name, v.Address, v.PricePerHour, v.Description, v.Category.Name))
            .ToListAsync();

        return new VenueListDto(items, page, pageSize, total);
    }

    public async Task<VenueDetailsDto?> GetByIdAsync(int id)
    {
        var v = await _db.Venues
            .Include(v => v.Category)
            .FirstOrDefaultAsync(v => v.Id == id && v.IsActive);

        return v is null ? null
            : new VenueDetailsDto(v.Id, v.Name, v.Address, v.PricePerHour, v.Description, v.Category.Name, v.CategoryId);
    }

    public async Task<VenueDetailsDto> CreateAsync(CreateVenueRequest request)
    {
        var venue = new Venue
        {
            Name = request.Name,
            Address = request.Address,
            PricePerHour = request.PricePerHour,
            Description = request.Description,
            CategoryId = request.CategoryId
        };
        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created venue: {Name}", venue.Name);

        var saved = await _db.Venues.Include(v => v.Category).FirstAsync(v => v.Id == venue.Id);
        return new VenueDetailsDto(saved.Id, saved.Name, saved.Address, saved.PricePerHour, saved.Description, saved.Category.Name, saved.CategoryId);
    }

    public async Task<bool> UpdateAsync(int id, UpdateVenueRequest request)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.Id == id && v.IsActive);
        if (venue is null) return false;

        venue.Name = request.Name;
        venue.Address = request.Address;
        venue.PricePerHour = request.PricePerHour;
        venue.Description = request.Description;
        venue.CategoryId = request.CategoryId;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated venue: {Id}", id);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.Id == id && v.IsActive);
        if (venue is null) return false;

        venue.IsActive = false; // soft delete
        await _db.SaveChangesAsync();
        _logger.LogInformation("Soft-deleted venue: {Id}", id);
        return true;
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        return await _db.Categories
            .Select(c => new CategoryDto(c.Id, c.Name))
            .ToListAsync();
    }
}
