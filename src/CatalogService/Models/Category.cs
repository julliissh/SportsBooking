namespace CatalogService.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Venue> Venues { get; set; } = [];
}
