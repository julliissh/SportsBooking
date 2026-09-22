using Microsoft.AspNetCore.Mvc;
using Monolith.Data;
using Monolith.Models;

namespace Monolith.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _database;

    public BookingsController(AppDbContext database)
    {
        _database = database;
    }

    [HttpGet]
    public IEnumerable<Booking> GetAll() => _database.Bookings.ToList();

    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        var booking = _database.Bookings.FirstOrDefault(booking => booking.Id == id);
        return booking is null ? NotFound() : Ok(booking);
    }

    [HttpPost]
    public ActionResult<Booking> Create([FromBody] CreateBookingRequest request)
    {
        var booking = new Booking
        {
            UserId = request.UserId,
            VenueId = request.VenueId,
            StartTime = request.StartTime,
            Hours = request.Hours
        };
        _database.Bookings.Add(booking);
        _database.SaveChanges();
        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, booking);
    }

    [HttpDelete("{id}")]
    public IActionResult Cancel(int id)
    {
        var booking = _database.Bookings.FirstOrDefault(booking => booking.Id == id);
        if (booking is null)
        {
            return NotFound();
        }

        _database.Bookings.Remove(booking);
        _database.SaveChanges();
        return NoContent();
    }
}

public record CreateBookingRequest(int UserId, int VenueId, DateTime StartTime, int Hours);
