using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monolith.Data;
using Monolith.Models;

namespace Monolith.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _database;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public BookingsController(
        AppDbContext database,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _database = database;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var bookings = await _database.Bookings.OrderByDescending(b => b.Id).ToListAsync();
        var result = new List<BookingResponseDto>();

        foreach (var b in bookings)
        {
            result.Add(await EnrichBookingAsync(b));
        }

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var booking = await _database.Bookings.FirstOrDefaultAsync(b => b.Id == id);
        if (booking == null) return NotFound(new { error = "Бронювання не знайдено" });

        var enriched = await EnrichBookingAsync(booking);
        return Ok(enriched);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest request)
    {
        var userId = request.UserId;
        if (userId <= 0 && User.Identity?.IsAuthenticated == true)
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (int.TryParse(sub, out var parsedId))
                userId = parsedId;
        }

        if (userId <= 0)
            return BadRequest(new { error = "Потрібно вказати дійсний UserId або передати токен авторизації" });

        var client = _httpClientFactory.CreateClient();
        var catalogUrl = _configuration["Services:CatalogUrl"] ?? "http://localhost:5002";
        
        try
        {
            var venueResp = await client.GetAsync($"{catalogUrl}/api/v1/catalog/items/{request.VenueId}");
            if (!venueResp.IsSuccessStatusCode)
            {
                return BadRequest(new { error = $"Майданчик з ID {request.VenueId} не знайдено в CatalogService" });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { error = $"Не вдалося зв'язатися з CatalogService: {ex.Message}" });
        }

        var booking = new Booking
        {
            UserId = userId,
            VenueId = request.VenueId,
            StartTime = request.StartTime,
            Hours = request.Hours
        };

        _database.Bookings.Add(booking);
        await _database.SaveChangesAsync();

        var enriched = await EnrichBookingAsync(booking);
        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, enriched);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var booking = await _database.Bookings.FirstOrDefaultAsync(b => b.Id == id);
        if (booking == null) return NotFound(new { error = "Бронювання не знайдено" });

        _database.Bookings.Remove(booking);
        await _database.SaveChangesAsync();
        return NoContent();
    }

    private async Task<BookingResponseDto> EnrichBookingAsync(Booking b)
    {
        var client = _httpClientFactory.CreateClient();
        string? venueName = null;
        string? venueAddress = null;
        decimal? pricePerHour = null;
        string? category = null;
        string? userName = null;
        string? userEmail = null;

        try
        {
            var catalogUrl = _configuration["Services:CatalogUrl"] ?? "http://localhost:5002";
            var venueResp = await client.GetAsync($"{catalogUrl}/api/v1/catalog/items/{b.VenueId}");
            if (venueResp.IsSuccessStatusCode)
            {
                var venue = await venueResp.Content.ReadFromJsonAsync<ExternalVenueDto>();
                if (venue != null)
                {
                    venueName = venue.Name;
                    venueAddress = venue.Address;
                    pricePerHour = venue.PricePerHour;
                    category = venue.Category;
                }
            }
        }
        catch
        {
        }

        try
        {
            var authUrl = _configuration["Services:AuthUrl"] ?? "http://localhost:5001";
            var userResp = await client.GetAsync($"{authUrl}/api/v1/users/{b.UserId}");
            if (userResp.IsSuccessStatusCode)
            {
                var user = await userResp.Content.ReadFromJsonAsync<ExternalUserDto>();
                if (user != null)
                {
                    userName = user.DisplayName;
                    userEmail = user.Email;
                }
            }
        }
        catch
        {
            var localUser = _database.Users.FirstOrDefault(u => u.Id == b.UserId);
            if (localUser != null)
            {
                userName = localUser.Username;
            }
        }

        decimal? totalPrice = pricePerHour.HasValue ? pricePerHour.Value * b.Hours : null;

        return new BookingResponseDto(
            b.Id,
            b.UserId,
            userName,
            userEmail,
            b.VenueId,
            venueName,
            venueAddress,
            category,
            pricePerHour,
            b.StartTime,
            b.Hours,
            totalPrice
        );
    }
}

public record CreateBookingRequest(int UserId, int VenueId, DateTime StartTime, int Hours);

public record BookingResponseDto(
    int Id,
    int UserId,
    string? UserName,
    string? UserEmail,
    int VenueId,
    string? VenueName,
    string? VenueAddress,
    string? Category,
    decimal? PricePerHour,
    DateTime StartTime,
    int Hours,
    decimal? TotalPrice
);

internal record ExternalVenueDto(int Id, string Name, string Address, decimal PricePerHour, string Category);
internal record ExternalUserDto(int Id, string Email, string DisplayName);
