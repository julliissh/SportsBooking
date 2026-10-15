using CatalogService.Dtos;
using CatalogService.Models;

namespace CatalogService.Repositories;

public interface ICatalogRepository
{
    Task<(List<Venue> Items, int TotalCount)> GetPageAsync(
        int page, int pageSize, string? search, int? categoryId, string? sortBy, bool desc);
    Task<Venue?> GetByIdAsync(int id);
    Task<bool> CategoryExistsAsync(int categoryId);
    Task AddAsync(Venue venue);
    Task SaveChangesAsync();
    Task<List<Category>> GetCategoriesAsync();
}
