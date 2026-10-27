using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using BookingService.Clients;
using BookingService.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace BookingService.IntegrationTests;

/// <summary>
/// Стаб (fake) клієнта CatalogService із заздалегідь відомими відповідями.
/// Дозволяє тестувати BookingService ізольовано без запуску процесу CatalogService
/// та моделювати помилки мережі/таймауту (Сценарій 9).
/// </summary>
public class FakeCatalogClient : ICatalogClient
{
    public CatalogLookupStatus SimulatedStatus { get; set; } = CatalogLookupStatus.Found;

    private static readonly Dictionary<int, CatalogVenueDto> KnownVenues = new()
    {
        [1] = new CatalogVenueDto(1, "Central Football Field", "Main St 1", 500.00m, "Football"),
        [2] = new CatalogVenueDto(2, "Tennis Club Kyiv", "Park Ave 5", 300.00m, "Tennis"),
        [3] = new CatalogVenueDto(3, "Basketball Arena", "Sports Blvd 10", 400.00m, "Basketball")
    };

    public Task<CatalogLookupResult> GetVenueAsync(int venueId, CancellationToken ct = default)
    {
        if (SimulatedStatus == CatalogLookupStatus.Unavailable)
            return Task.FromResult(new CatalogLookupResult(CatalogLookupStatus.Unavailable));

        if (SimulatedStatus == CatalogLookupStatus.TimedOut)
            return Task.FromResult(new CatalogLookupResult(CatalogLookupStatus.TimedOut));

        if (KnownVenues.TryGetValue(venueId, out var venue))
            return Task.FromResult(new CatalogLookupResult(CatalogLookupStatus.Found, venue));

        return Task.FromResult(new CatalogLookupResult(CatalogLookupStatus.NotFound));
    }

    public Task<bool> CheckHealthAsync(CancellationToken ct = default) =>
        Task.FromResult(SimulatedStatus == CatalogLookupStatus.Found);
}

public class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "super-secret-key-min-32-bytes-long!!";
    public const string TestJwtIssuer = "SportsBookingAuth";
    public const string TestJwtAudience = "SportsBookingClients";

    public FakeCatalogClient CatalogStub { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddUserSecrets<Program>(optional: true);
        });

        builder.ConfigureServices((context, services) =>
        {
            var baseConnStr = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
                ?? context.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection not configured in user-secrets.");

            // Завжди перемикаємо базу на окрему тестову miniproject_booking_test
            var testConnStr = Regex.Replace(
                baseConnStr,
                @"Database\s*=\s*[^;]+",
                "Database=miniproject_booking_test",
                RegexOptions.IgnoreCase);

            // 1. Прибираємо робочий DbContext і підключаємо окрему ТЕСТОВУ БД miniproject_booking_test
            services.RemoveAll<DbContextOptions<BookingDbContext>>();
            services.RemoveAll<BookingDbContext>();

            services.AddDbContext<BookingDbContext>(opt =>
                opt.UseMySql(testConnStr, new MySqlServerVersion(new Version(8, 0, 0))));

            // 2. Підміняємо клієнта каталогу стабом із відомими відповідями
            services.RemoveAll<ICatalogClient>();
            services.AddSingleton<ICatalogClient>(CatalogStub);
        });
    }

    public async Task ResetDatabaseAsync()
    {
        CatalogStub.SimulatedStatus = CatalogLookupStatus.Found;

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Bookings;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Bookings AUTO_INCREMENT = 1;");
    }

    public static string GenerateJwtToken(int userId, string email = "user@sportsbooking.com", string role = "User")
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
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
