using Microsoft.AspNetCore.Mvc;
using Monolith.Data;
using Monolith.Models;

namespace Monolith.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _database;

    public AuthController(AppDbContext database)
    {
        _database = database;
    }

    [HttpPost("register")]
    public IActionResult Register(RegisterRequest request)
    {
        if (_database.Users.Any(user => user.Username == request.Username))
        {
            return Conflict("Користувач з таким ім'ям вже є");
        }
        var user = new User { Username = request.Username, Password = request.Password, Role = "User" };
        _database.Users.Add(user);
        _database.SaveChanges();
        return Created($"/api/auth/me?username={user.Username}", new { user.Id, user.Username });
    }

    [HttpPost("login")]
    public IActionResult Login(LoginRequest request)
    {
        var user = _database.Users.FirstOrDefault(user =>
            user.Username == request.Username && user.Password == request.Password);

        if (user is null)
        {
            return Unauthorized();
        }
        return Ok(new { token = $"fake-jwt-{user.Id}", user.Username, user.Role });
    }

    [HttpGet("me")]
    public IActionResult Me([FromQuery] string username)
    {
        var user = _database.Users.FirstOrDefault(user => user.Username == username);
        return user is null ? NotFound() : Ok(new { user.Id, user.Username, user.Role });
    }
}
public record RegisterRequest(string Username, string Password);
public record LoginRequest(string Username, string Password);
