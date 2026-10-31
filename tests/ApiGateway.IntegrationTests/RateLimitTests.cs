using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ApiGateway.IntegrationTests;

public class RateLimitTestFactory : WebApplicationFactory<Program>
{
    private WireMockServer? _wireMock;

    public int BookingPort => _wireMock?.Port ?? 5003;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        _wireMock = WireMockServer.Start();
        _wireMock
            .Given(Request.Create().WithPath("/api/bookings").UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(201)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("{\"id\":10,\"status\":\"Confirmed\"}"));

        _wireMock
            .Given(Request.Create().WithPath("/api/v1/auth/login").UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("{\"accessToken\":\"fake-jwt-token\"}"));

        _wireMock
            .Given(Request.Create().WithPath("/api/v1/catalog/items").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("[]"));

        builder.UseSetting("Jwt:Key", GatewayApiFactory.TestJwtKey);
        builder.UseSetting("Jwt:Issuer", GatewayApiFactory.TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", GatewayApiFactory.TestJwtAudience);
        builder.UseSetting("RateLimits:Login", "2");
        builder.UseSetting("RateLimits:Booking", "3");
        builder.UseSetting("RateLimits:General", "10");

        builder.UseSetting("Gateway:AuthHost", "localhost");
        builder.UseSetting("Gateway:AuthPort", _wireMock.Port.ToString());
        builder.UseSetting("Gateway:CatalogHost", "localhost");
        builder.UseSetting("Gateway:CatalogPort", _wireMock.Port.ToString());
        builder.UseSetting("Gateway:BookingHost", "localhost");
        builder.UseSetting("Gateway:BookingPort", _wireMock.Port.ToString());

        builder.UseSetting("Routes:0:DownstreamHostAndPorts:0:Host", "localhost");
        builder.UseSetting("Routes:0:DownstreamHostAndPorts:0:Port", _wireMock.Port.ToString());
        builder.UseSetting("Routes:2:DownstreamHostAndPorts:0:Host", "localhost");
        builder.UseSetting("Routes:2:DownstreamHostAndPorts:0:Port", _wireMock.Port.ToString());
        builder.UseSetting("Routes:3:DownstreamHostAndPorts:0:Host", "localhost");
        builder.UseSetting("Routes:3:DownstreamHostAndPorts:0:Port", _wireMock.Port.ToString());
        builder.UseSetting("Routes:4:DownstreamHostAndPorts:0:Host", "localhost");
        builder.UseSetting("Routes:4:DownstreamHostAndPorts:0:Port", _wireMock.Port.ToString());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _wireMock?.Stop();
        _wireMock?.Dispose();
    }
}

public class RateLimitTests : IClassFixture<RateLimitTestFactory>
{
    private readonly RateLimitTestFactory _factory;
    private readonly HttpClient _client;

    public RateLimitTests(RateLimitTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Scenario15_RateLimit_ExceededBookingLimit_Returns429WithRetryAfterHeader()
    {
        var token = GatewayApiFactory.GenerateJwtToken(userId: 42, "limit-user@sportsbooking.com", "User");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        for (int i = 0; i < 3; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
            {
                Content = new StringContent("{\"venueId\":1,\"startTime\":\"2027-06-01T10:00:00Z\",\"hours\":2}", Encoding.UTF8, "application/json")
            };
            var resp = await _client.SendAsync(req);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, resp.StatusCode);
        }

        using var rejectedReq = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = new StringContent("{\"venueId\":1,\"startTime\":\"2027-06-01T10:00:00Z\",\"hours\":2}", Encoding.UTF8, "application/json")
        };
        var rejectedResponse = await _client.SendAsync(rejectedReq);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        Assert.NotNull(rejectedResponse.Headers.RetryAfter);

        var problem = await rejectedResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status429TooManyRequests, problem!.Status);
        Assert.Equal("Too Many Requests", problem.Title);
    }

    [Fact]
    public async Task Scenario15b_RateLimit_LoginLimitIsStricterThanGeneralRequests()
    {
        // Перші 2 спроби входу дозволені (RateLimits:Login = 2)
        for (int i = 0; i < 2; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = new StringContent("{\"email\":\"user@sportsbooking.com\",\"password\":\"Secret1!\"}", Encoding.UTF8, "application/json")
            };
            var resp = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }

        // 3-я спроба входу блокується з 429 Too Many Requests
        using var rejectedReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = new StringContent("{\"email\":\"user@sportsbooking.com\",\"password\":\"Secret1!\"}", Encoding.UTF8, "application/json")
        };
        var rejectedResp = await _client.SendAsync(rejectedReq);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResp.StatusCode);
        Assert.NotNull(rejectedResp.Headers.RetryAfter);

        // У той самий час загальні запити каталогу (ліміт = 10) з тієї ж IP-адреси продовжують успішно проходити
        var catalogResp = await _client.GetAsync("/api/v1/catalog/items");
        Assert.Equal(HttpStatusCode.OK, catalogResp.StatusCode);
    }
}
