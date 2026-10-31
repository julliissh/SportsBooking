using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.Clients;
using ApiGateway.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ApiGateway.IntegrationTests;

public class GatewayTests : IClassFixture<GatewayApiFactory>, IAsyncLifetime
{
    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _client;

    public GatewayTests(GatewayApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        _factory.ResetStubs();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private void Authenticate(int userId = 1, string role = "User")
    {
        var token = GatewayApiFactory.GenerateJwtToken(userId, $"user{userId}@sportsbooking.com", role);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task Scenario11_CompositeRequest_WhenAllAvailable_Returns200WithAggregatedDetails()
    {
        Authenticate(userId: 1);

        var response = await _client.GetAsync("/api/gateway/venues/1/details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<VenueDetailsAggDto>();
        Assert.NotNull(body);
        Assert.Equal(1, body!.Venue.Id);
        Assert.Equal("Olympic Arena", body.Venue.Name);
        Assert.Equal("active", body.BookingStatus);
        Assert.NotNull(body.MyActiveBooking);
        Assert.Equal(101, body.MyActiveBooking!.Id);
        Assert.Equal(900.00m, body.MyActiveBooking.TotalPrice);
    }

    [Fact]
    public async Task Scenario12_CompositeRequest_WhenCatalogUnavailable_Returns503ProblemDetails()
    {
        Authenticate(userId: 1);
        _factory.CatalogStub.SimulatedStatus = GwStatus.Unavailable;

        var response = await _client.GetAsync("/api/gateway/venues/1/details");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem!.Status);
        Assert.Contains("unavailable", problem.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario12b_CompositeRequest_WhenCatalogDownstreamDelaysOver2Seconds_TimesOutWith503ProblemDetails()
    {
        using var wireMock = WireMockServer.Start();
        wireMock
            .Given(Request.Create().WithPath("/api/v1/catalog/items/1").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithDelay(TimeSpan.FromSeconds(3.5))
                    .WithBody("{\"id\":1,\"name\":\"Slow Arena\"}"));

        var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Services:CatalogUrl", wireMock.Url);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICatalogGwClient>();
                services.AddHttpClient<ICatalogGwClient, CatalogGwClient>(c =>
                {
                    c.BaseAddress = new Uri(wireMock.Url!);
                    c.Timeout = TimeSpan.FromSeconds(2);
                });
            });
        });

        using var client = customFactory.CreateClient();
        var token = GatewayApiFactory.GenerateJwtToken(userId: 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await client.GetAsync("/api/gateway/venues/1/details");
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 3200, $"Expected timeout ~2000ms, but took {sw.ElapsedMilliseconds}ms");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem!.Status);
        Assert.Equal("Catalog service unavailable", problem.Title);
    }

    [Fact]
    public async Task Scenario13_CompositeRequest_WhenBookingUnavailable_Returns200WithUnavailableStatus()
    {
        Authenticate(userId: 1);
        _factory.BookingStub.SimulatedStatus = GwStatus.Unavailable;

        var response = await _client.GetAsync("/api/gateway/venues/1/details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<VenueDetailsAggDto>();
        Assert.NotNull(body);
        Assert.Equal(1, body!.Venue.Id);
        Assert.Equal("Olympic Arena", body.Venue.Name);
        Assert.Equal("unavailable", body.BookingStatus);
        Assert.Null(body.MyActiveBooking);
    }

    [Fact]
    public async Task Scenario14_CompositeRequest_WithoutToken_Returns401AndDownstreamNotInvoked()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/gateway/venues/1/details");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem!.Status);
        Assert.Equal("Unauthorized", problem.Title);

        Assert.Equal(0, _factory.BookingStub.CallCount);
        Assert.Equal(0, _factory.CatalogStub.CallCount);
    }

    [Fact]
    public async Task Scenario14b_OcelotRoute_WithoutToken_Returns401WithProblemDetailsBody()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem!.Status);
    }

    [Fact]
    public async Task Scenario14c_Request_WithInvalidOrExpiredToken_Returns401ProblemDetails()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-expired-or-malformed-token");

        var response = await _client.GetAsync("/api/gateway/venues/1/details");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem!.Status);
        Assert.Equal("Unauthorized", problem.Title);

        Assert.Equal(0, _factory.BookingStub.CallCount);
        Assert.Equal(0, _factory.CatalogStub.CallCount);
    }

    [Fact]
    public async Task Scenario16_CatalogCache_SecondRequestServedFromCache_DownstreamCalledOnce()
    {
        using var wireMock = WireMockServer.Start();
        wireMock
            .Given(Request.Create().WithPath("/api/v1/catalog/items").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("{\"items\":[{\"id\":1,\"name\":\"Olympic Arena\"}],\"total\":1}"));

        var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Gateway:CatalogHost", "localhost");
            builder.UseSetting("Gateway:CatalogPort", wireMock.Port.ToString());
            builder.UseSetting("Routes:2:DownstreamHostAndPorts:0:Host", "localhost");
            builder.UseSetting("Routes:2:DownstreamHostAndPorts:0:Port", wireMock.Port.ToString());
            builder.UseSetting("Routes:3:DownstreamHostAndPorts:0:Host", "localhost");
            builder.UseSetting("Routes:3:DownstreamHostAndPorts:0:Port", wireMock.Port.ToString());
            builder.UseSetting("Cache:CatalogTtlSeconds", "60");
        });

        using var client = customFactory.CreateClient();
        var token = GatewayApiFactory.GenerateJwtToken(userId: 1, "admin@sportsbooking.com", "Admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp1 = await client.GetAsync("/api/v1/catalog/items");
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        Assert.True(resp1.Headers.TryGetValues("X-Cache", out var values1));
        Assert.Contains("MISS", values1);

        var resp2 = await client.GetAsync("/api/v1/catalog/items");
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        Assert.True(resp2.Headers.TryGetValues("X-Cache", out var values2));
        Assert.Contains("HIT", values2);

        Assert.Single(wireMock.LogEntries);
    }

    [Fact]
    public async Task Scenario16b_CatalogCache_MutationInvalidatesCache_SubsequentGetHitsDownstream()
    {
        using var wireMock = WireMockServer.Start();

        wireMock
            .Given(Request.Create().WithPath("/api/v1/catalog/items/1").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("{\"id\":1,\"name\":\"Initial Arena\"}"));

        wireMock
            .Given(Request.Create().WithPath("/api/v1/catalog/items/1").UsingPut())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("{\"id\":1,\"name\":\"Updated Arena\"}"));

        var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Gateway:CatalogHost", "localhost");
            builder.UseSetting("Gateway:CatalogPort", wireMock.Port.ToString());
            builder.UseSetting("Routes:2:DownstreamHostAndPorts:0:Host", "localhost");
            builder.UseSetting("Routes:2:DownstreamHostAndPorts:0:Port", wireMock.Port.ToString());
            builder.UseSetting("Routes:3:DownstreamHostAndPorts:0:Host", "localhost");
            builder.UseSetting("Routes:3:DownstreamHostAndPorts:0:Port", wireMock.Port.ToString());
            builder.UseSetting("Cache:CatalogTtlSeconds", "60");
        });

        using var client = customFactory.CreateClient();
        var token = GatewayApiFactory.GenerateJwtToken(userId: 1, "admin@sportsbooking.com", "Admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Перший GET -> MISS (звертається до downstream)
        var resp1 = await client.GetAsync("/api/v1/catalog/items/1");
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        Assert.True(resp1.Headers.TryGetValues("X-Cache", out var values1));
        Assert.Contains("MISS", values1);
        var body1 = await resp1.Content.ReadAsStringAsync();
        Assert.Contains("Initial Arena", body1);

        // 2. Повторний GET -> HIT (відповідь береться з кешу пам'яті шлюзу)
        var resp2 = await client.GetAsync("/api/v1/catalog/items/1");
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        Assert.True(resp2.Headers.TryGetValues("X-Cache", out var values2));
        Assert.Contains("HIT", values2);

        // Оновлюємо відповідь на WireMock для наступного виклику GET
        wireMock.ResetMappings();
        wireMock
            .Given(Request.Create().WithPath("/api/v1/catalog/items/1").UsingPut())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("{\"id\":1,\"name\":\"Updated Arena\"}"));

        wireMock
            .Given(Request.Create().WithPath("/api/v1/catalog/items/1").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("{\"id\":1,\"name\":\"Updated Arena\"}"));

        // 3. Зміна даних через PUT -> 200 OK -> шлюз інвалідує кеш каталогу
        var putContent = new StringContent("{\"name\":\"Updated Arena\"}", System.Text.Encoding.UTF8, "application/json");
        var putResp = await client.PutAsync("/api/v1/catalog/items/1", putContent);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        // 4. Наступний GET -> MISS (кеш скинуто, шлюз повторно звертається до сервісу й отримує оновлені дані)
        var resp3 = await client.GetAsync("/api/v1/catalog/items/1");
        Assert.Equal(HttpStatusCode.OK, resp3.StatusCode);
        Assert.True(resp3.Headers.TryGetValues("X-Cache", out var values3));
        Assert.Contains("MISS", values3);
        var body3 = await resp3.Content.ReadAsStringAsync();
        Assert.Contains("Updated Arena", body3);

        // Перевіряємо, що WireMock отримав рівно 2 GET-запити (до і після PUT)
        var getCalls = wireMock.LogEntries.Where(l => l.RequestMessage.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(2, getCalls.Count);
    }

    [Fact]
    public async Task Scenario17_CompositeRequest_TwoDifferentUsers_EachSeesOwnBooking()
    {
        Authenticate(userId: 1);
        var respUser1 = await _client.GetAsync("/api/gateway/venues/1/details");
        Assert.Equal(HttpStatusCode.OK, respUser1.StatusCode);
        var body1 = await respUser1.Content.ReadFromJsonAsync<VenueDetailsAggDto>();
        Assert.NotNull(body1);
        Assert.NotNull(body1!.MyActiveBooking);
        Assert.Equal(101, body1.MyActiveBooking!.Id);
        Assert.Equal(900.00m, body1.MyActiveBooking.TotalPrice);

        Authenticate(userId: 2);
        var respUser2 = await _client.GetAsync("/api/gateway/venues/1/details");
        Assert.Equal(HttpStatusCode.OK, respUser2.StatusCode);
        var body2 = await respUser2.Content.ReadFromJsonAsync<VenueDetailsAggDto>();
        Assert.NotNull(body2);
        Assert.NotNull(body2!.MyActiveBooking);
        Assert.Equal(102, body2.MyActiveBooking!.Id);
        Assert.Equal(1350.00m, body2.MyActiveBooking.TotalPrice);

        Assert.NotEqual(body1.MyActiveBooking.Id, body2.MyActiveBooking.Id);
        Assert.Equal(2, _factory.BookingStub.CallCount);
        Assert.Equal(2, _factory.CatalogStub.CallCount);
    }

    [Fact]
    public async Task Scenario18_CompositeRequest_WhenVenueNotFound_Returns404ProblemDetails()
    {
        Authenticate(userId: 1);
        _factory.CatalogStub.SimulatedStatus = GwStatus.NotFound;

        var response = await _client.GetAsync("/api/gateway/venues/999/details");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status404NotFound, problem!.Status);
        Assert.Equal("Venue not found", problem.Title);
    }

    [Fact]
    public async Task HealthCheck_WhenAllServicesAvailable_Returns200WithOkStatus()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = doc.RootElement;
        Assert.Equal("OK", root.GetProperty("status").GetString());
        Assert.Equal("Available", root.GetProperty("catalog").GetString());
        Assert.Equal("Available", root.GetProperty("booking").GetString());
        Assert.Equal("Available", root.GetProperty("auth").GetString());
    }

    [Fact]
    public async Task HealthCheck_WhenServiceUnavailable_Returns503WithDegradedStatus()
    {
        _factory.CatalogStub.SimulatedStatus = GwStatus.Unavailable;

        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = doc.RootElement;
        Assert.Equal("Degraded", root.GetProperty("status").GetString());
        Assert.Equal("Unavailable", root.GetProperty("catalog").GetString());
    }

    [Fact]
    public async Task Swagger_EndpointsAndExamples_GeneratedSuccessfully()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var swaggerJson = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/gateway/venues/{venueId}/details", swaggerJson);
        Assert.Contains("active-booking", swaggerJson);
        Assert.Contains("unavailable-booking", swaggerJson);
        Assert.Contains("none-booking", swaggerJson);
        Assert.Contains("catalog-unavailable", swaggerJson);
    }

    [Fact]
    public async Task Scenario19_OcelotRoute_WhenDownstreamExceedsTimeout_TimesOut()
    {
        using var wireMock = WireMockServer.Start();
        wireMock
            .Given(Request.Create().WithPath("/api/v1/catalog/slow").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithDelay(TimeSpan.FromSeconds(3.5))
                    .WithBody("{\"slow\":true}"));

        var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Gateway:CatalogHost", "localhost");
            builder.UseSetting("Gateway:CatalogPort", wireMock.Port.ToString());
            builder.UseSetting("Routes:2:DownstreamHostAndPorts:0:Host", "localhost");
            builder.UseSetting("Routes:2:DownstreamHostAndPorts:0:Port", wireMock.Port.ToString());
        });

        using var client = customFactory.CreateClient();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await client.GetAsync("/api/v1/catalog/slow");
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 3200, $"Expected timeout around 2000ms but took {sw.ElapsedMilliseconds}ms");
        Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable || response.StatusCode == HttpStatusCode.GatewayTimeout);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.True(problem!.Status == StatusCodes.Status503ServiceUnavailable || problem.Status == StatusCodes.Status504GatewayTimeout);
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
    }

    [Fact]
    public async Task Scenario20_ClientSupplied_UserHeaders_AreStrippedAndOverwritten()
    {
        Authenticate(userId: 1, role: "User");

        // Клієнт намагається підробити чужий userId та роль Admin у HTTP-заголовках
        _client.DefaultRequestHeaders.Remove("X-User-Id");
        _client.DefaultRequestHeaders.Remove("X-User-Roles");
        _client.DefaultRequestHeaders.Add("X-User-Id", "999");
        _client.DefaultRequestHeaders.Add("X-User-Roles", "Admin");

        var response = await _client.GetAsync("/api/gateway/venues/1/details");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<VenueDetailsAggDto>();
        Assert.NotNull(body);
        Assert.NotNull(body!.MyActiveBooking);
        // Шлюз відкинув 999 і використав валідний sub з токена (userId = 1 -> bookingId = 101)
        Assert.Equal(101, body.MyActiveBooking!.Id);
    }
}
