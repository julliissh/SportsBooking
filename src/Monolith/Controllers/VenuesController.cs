using Microsoft.AspNetCore.Mvc;
using Monolith.Data;
using Monolith.Models;

namespace Monolith.Controllers;

[ApiController]
[Route("api/venues")]
public class VenuesController : ControllerBase
{
    private readonly AppDbContext _database;

    public VenuesController(AppDbContext database)
    {
        _database = database;
    }

    [HttpGet]
    public IEnumerable<Venue> GetAll() => _database.Venues.ToList();

    [HttpPost]
    public ActionResult<Venue> Create([FromBody] CreateVenueRequest request)
    {
        var venue = new Venue { Name = request.Name, Address = request.Address, PricePerHour = request.PricePerHour };
        _database.Venues.Add(venue);
        _database.SaveChanges();
        return CreatedAtAction(nameof(GetAll), venue);
    }
}

public record CreateVenueRequest(string Name, string Address, decimal PricePerHour);
