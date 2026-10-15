using CatalogService.Dtos;
using CatalogService.Models;
using CatalogService.Repositories;

namespace CatalogService.Services;

public class CatalogServiceImpl : ICatalogService
{
    private readonly ICatalogRepository _repo;
    private readonly ILogger<CatalogServiceImpl> _logger;

    public CatalogServiceImpl(ICatalogRepository repo, ILogger<CatalogServiceImpl> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<VenueListDto> GetVenuesAsync(
        int page, int pageSize, string? search, int? categoryId, string? sortBy, bool desc)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _repo.GetPageAsync(page, pageSize, search, categoryId, sortBy, desc);
        var dtos = items.Select(ToDto).ToList();
        return new VenueListDto(dtos, page, pageSize, total);
    }

    public async Task<VenueDetailsDto?> GetByIdAsync(int id)
    {
        var v = await _repo.GetByIdAsync(id);
        return v is null ? null : ToDetailsDto(v);
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

        await _repo.AddAsync(venue);
        await _repo.SaveChangesAsync();

        _logger.LogInformation("Venue created: {Name} (id={Id})", venue.Name, venue.Id);

        var saved = await _repo.GetByIdAsync(venue.Id);
        return ToDetailsDto(saved!);
    }

    public async Task<bool> UpdateAsync(int id, UpdateVenueRequest request)
    {
        var venue = await _repo.GetByIdAsync(id);
        if (venue is null) return false;

        venue.Name = request.Name;
        venue.Address = request.Address;
        venue.PricePerHour = request.PricePerHour;
        venue.Description = request.Description;
        venue.CategoryId = request.CategoryId;

        await _repo.SaveChangesAsync();
        _logger.LogInformation("Venue updated: id={Id}", id);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var venue = await _repo.GetByIdAsync(id);
        if (venue is null) return false;

        venue.IsActive = false;
        await _repo.SaveChangesAsync();
        _logger.LogInformation("Venue soft-deleted: id={Id}", id);
        return true;
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        var categories = await _repo.GetCategoriesAsync();
        return categories.Select(c => new CategoryDto(c.Id, c.Name)).ToList();
    }

    private static VenueDto ToDto(Venue v) =>
        new(v.Id, v.Name, v.Address, v.PricePerHour, v.Description, v.Category.Name);

    private static VenueDetailsDto ToDetailsDto(Venue v) =>
        new(v.Id, v.Name, v.Address, v.PricePerHour, v.Description, v.Category.Name, v.CategoryId);
}
