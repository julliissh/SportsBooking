using CatalogService.Dtos;

namespace CatalogService.Services;

public interface ICatalogService
{
    Task<VenueListDto> GetVenuesAsync(int page, int pageSize, string? search, int? categoryId, string? sortBy, bool desc);
    Task<VenueDetailsDto?> GetByIdAsync(int id);
    Task<VenueDetailsDto> CreateAsync(CreateVenueRequest request);
    Task<bool> UpdateAsync(int id, UpdateVenueRequest request);
    Task<bool> DeleteAsync(int id);
    Task<List<CategoryDto>> GetCategoriesAsync();
}
