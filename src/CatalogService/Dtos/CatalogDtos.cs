using System.ComponentModel.DataAnnotations;

namespace CatalogService.Dtos;

public record VenueDto(int Id, string Name, string Address, decimal PricePerHour, string? Description, string Category);
public record VenueDetailsDto(int Id, string Name, string Address, decimal PricePerHour, string? Description, string Category, int CategoryId);
public record VenueListDto(List<VenueDto> Items, int Page, int PageSize, int TotalCount);

public class CreateVenueRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(300)]
    public string Address { get; set; } = null!;

    [Range(0, 1_000_000)]
    public decimal PricePerHour { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public int CategoryId { get; set; }
}

public class UpdateVenueRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(300)]
    public string Address { get; set; } = null!;

    [Range(0, 1_000_000)]
    public decimal PricePerHour { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public int CategoryId { get; set; }
}

public record CategoryDto(int Id, string Name);
