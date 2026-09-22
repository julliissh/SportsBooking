namespace CatalogService.Dtos;

public record VenueDto(int Id, string Name, string Address, decimal PricePerHour, string? Description, string Category);
public record VenueDetailsDto(int Id, string Name, string Address, decimal PricePerHour, string? Description, string Category, int CategoryId);
public record VenueListDto(List<VenueDto> Items, int Page, int PageSize, int TotalCount);

public record CreateVenueRequest(string Name, string Address, decimal PricePerHour, string? Description, int CategoryId);
public record UpdateVenueRequest(string Name, string Address, decimal PricePerHour, string? Description, int CategoryId);

public record CategoryDto(int Id, string Name);
