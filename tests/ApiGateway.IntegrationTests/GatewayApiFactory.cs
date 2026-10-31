using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApiGateway.Clients;
using ApiGateway.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.IntegrationTests;

public class FakeCatalogGwClient : ICatalogGwClient
{
    public int CallCount { get; private set; }
    public GwStatus SimulatedStatus { get; set; } = GwStatus.Ok;
    public bool SimulatedHealth { get; set; } = true;
    public VenueDto? SimulatedVenue { get; set; } = new(
        1,
        "Olympic Arena",
        "Sportivna Sq 1",
        450.00m,
        "Modern sports complex",
        "Multipurpose");

    public Task<GwResult<VenueDto>> GetVenueAsync(int venueId, string correlationId, CancellationToken ct = default)
    {
        CallCount++;
        if (SimulatedStatus == GwStatus.Unavailable)
        {
            return Task.FromResult(new GwResult<VenueDto>(GwStatus.Unavailable));
        }
        if (SimulatedStatus == GwStatus.NotFound || venueId == 999)
        {
            return Task.FromResult(new GwResult<VenueDto>(GwStatus.NotFound));
        }

        var venue = SimulatedVenue ?? new VenueDto(venueId, "Olympic Arena", "Sportivna Sq 1", 450.00m, "Modern sports complex", "Multipurpose");
        return Task.FromResult(new GwResult<VenueDto>(GwStatus.Ok, venue with { Id = venueId }));
    }

    public Task<bool> CheckHealthAsync(CancellationToken ct = default) =>
        Task.FromResult(SimulatedHealth && SimulatedStatus != GwStatus.Unavailable);

    public void Reset()
    {
        CallCount = 0;
        SimulatedStatus = GwStatus.Ok;
        SimulatedHealth = true;
    }
}

public class FakeBookingGwClient : IBookingGwClient
{
    public int CallCount { get; private set; }
    public GwStatus SimulatedStatus { get; set; } = GwStatus.Ok;
    public bool SimulatedHealth { get; set; } = true;

    private readonly Dictionary<string, ActiveBookingDto> _userBookings = new()
    {
        ["1:1"] = new ActiveBookingDto(101, 1, DateTime.UtcNow.AddDays(2), 2, 900.00m, "Confirmed", DateTime.UtcNow.AddDays(-1)),
        ["2:1"] = new ActiveBookingDto(102, 1, DateTime.UtcNow.AddDays(3), 3, 1350.00m, "Confirmed", DateTime.UtcNow.AddDays(-1))
    };

    public Task<GwResult<ActiveBookingDto>> GetMyActiveAsync(
        int venueId,
        string userId,
        string bearerToken,
        string correlationId,
        CancellationToken ct = default)
    {
        CallCount++;
        if (SimulatedStatus == GwStatus.Unavailable)
        {
            return Task.FromResult(new GwResult<ActiveBookingDto>(GwStatus.Unavailable));
        }

        var key = $"{userId}:{venueId}";
        if (_userBookings.TryGetValue(key, out var booking))
        {
            return Task.FromResult(new GwResult<ActiveBookingDto>(GwStatus.Ok, booking));
        }

        return Task.FromResult(new GwResult<ActiveBookingDto>(GwStatus.NotFound));
    }

    public Task<bool> CheckHealthAsync(CancellationToken ct = default) =>
        Task.FromResult(SimulatedHealth && SimulatedStatus != GwStatus.Unavailable);

    public void Reset()
    {
        CallCount = 0;
        SimulatedStatus = GwStatus.Ok;
        SimulatedHealth = true;
    }
}

public class FakeAuthGwClient : IAuthGwClient
{
    public GwStatus SimulatedStatus { get; set; } = GwStatus.Ok;
    public bool SimulatedHealth { get; set; } = true;
    public int CallCount { get; private set; }

    public Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(SimulatedHealth && SimulatedStatus != GwStatus.Unavailable);
    }

    public void Reset()
    {
        CallCount = 0;
        SimulatedStatus = GwStatus.Ok;
        SimulatedHealth = true;
    }
}

public class GatewayApiFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "test-jwt-secret-key-that-is-at-least-32-bytes-long!";
    public const string TestJwtIssuer = "SportsBookingAuth";
    public const string TestJwtAudience = "SportsBookingClients";

    public FakeCatalogGwClient CatalogStub { get; } = new();
    public FakeBookingGwClient BookingStub { get; } = new();
    public FakeAuthGwClient AuthStub { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("Jwt:Key", TestJwtKey);
        builder.UseSetting("Jwt:Issuer", TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", TestJwtAudience);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICatalogGwClient>();
            services.RemoveAll<IBookingGwClient>();
            services.RemoveAll<IAuthGwClient>();

            services.AddSingleton<ICatalogGwClient>(CatalogStub);
            services.AddSingleton<IBookingGwClient>(BookingStub);
            services.AddSingleton<IAuthGwClient>(AuthStub);
        });
    }

    public void ResetStubs()
    {
        CatalogStub.Reset();
        BookingStub.Reset();
        AuthStub.Reset();
    }

    public static string GenerateJwtToken(int userId, string email = "user@sportsbooking.com", string role = "User")
    {
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestJwtIssuer,
            audience: TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
