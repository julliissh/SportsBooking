using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookingService.Clients;
using BookingService.Data;
using BookingService.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookingService.IntegrationTests;

public class BookingIntegrationTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public BookingIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private void Authenticate(int userId = 1, string role = "User")
    {
        var token = ApiFactory.GenerateJwtToken(userId, $"user{userId}@sportsbooking.com", role);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // Сценарій 1: Створення з коректними даними -> 201, тіло з Id, заголовок Location, запис у БД
    [Fact]
    public async Task Scenario01_Create_WithValidData_Returns201AndLocationAndSavesToDb()
    {
        Authenticate(userId: 1);
        var startTime = new DateTime(2027, 5, 10, 14, 0, 0, DateTimeKind.Utc);
        var request = new CreateBookingRequest
        {
            VenueId = 1,
            StartTime = startTime,
            Hours = 2
        };

        var response = await _client.PostAsJsonAsync("/api/bookings", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/bookings/", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadFromJsonAsync<BookingResponseDto>();
        Assert.NotNull(body);
        Assert.True(body!.Id > 0);
        Assert.Equal(1, body.UserId);
        Assert.Equal(1, body.VenueId);
        Assert.Equal("Central Football Field", body.VenueName);
        Assert.Equal("Main St 1", body.VenueAddress);
        Assert.Equal(500.00m, body.PricePerHour);
        Assert.Equal(2, body.Hours);
        Assert.Equal(1000.00m, body.TotalPrice);
        Assert.Equal("Confirmed", body.Status);

        // Перевіряємо фізичний запис у тестовій БД
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var savedInDb = await db.Bookings.FirstOrDefaultAsync(b => b.Id == body.Id);
        Assert.NotNull(savedInDb);
        Assert.Equal("Central Football Field", savedInDb!.VenueName);
        Assert.Equal(1000.00m, savedInDb.TotalPrice);
    }

    // Сценарій 2: Створення без токена -> 401 Unauthorized
    [Fact]
    public async Task Scenario02_Create_WithoutToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var request = new CreateBookingRequest
        {
            VenueId = 1,
            StartTime = new DateTime(2027, 5, 10, 14, 0, 0, DateTimeKind.Utc),
            Hours = 2
        };

        var response = await _client.PostAsJsonAsync("/api/bookings", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Сценарій 3: Створення з неіснуючим ItemId (VenueId) -> 400 Bad Request та ProblemDetails
    [Fact]
    public async Task Scenario03_Create_WithNonExistentVenueId_Returns400ProblemDetails()
    {
        Authenticate(userId: 1);
        var request = new CreateBookingRequest
        {
            VenueId = 999,
            StartTime = new DateTime(2027, 5, 11, 14, 0, 0, DateTimeKind.Utc),
            Hours = 2
        };

        var response = await _client.PostAsJsonAsync("/api/bookings", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.Equal("Майданчик не знайдено", problem.Title);
        Assert.Contains("999", problem.Detail);
    }

    // Сценарій 4: Створення з порушенням бізнес-правила (дата в минулому) -> 400 ValidationProblemDetails
    [Fact]
    public async Task Scenario04_Create_WithPastStartTime_Returns400ValidationProblem()
    {
        Authenticate(userId: 1);
        var request = new CreateBookingRequest
        {
            VenueId = 1,
            StartTime = new DateTime(2020, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            Hours = 2
        };

        var response = await _client.PostAsJsonAsync("/api/bookings", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = doc.RootElement;
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("StartTime", out var startTimeErrors));
        Assert.True(startTimeErrors.GetArrayLength() > 0);
    }

    // Сценарій 5: Повтор запиту з тим самим Idempotency-Key -> Друга операція не створюється, відповідь та сама
    [Fact]
    public async Task Scenario05_Create_WithSameIdempotencyKey_DoesNotCreateDuplicateAndReturnsSameResult()
    {
        Authenticate(userId: 1);
        var payload = new CreateBookingRequest
        {
            VenueId = 2,
            StartTime = new DateTime(2027, 5, 12, 16, 0, 0, DateTimeKind.Utc),
            Hours = 3
        };

        using var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(payload)
        };
        req1.Headers.Add("Idempotency-Key", "idem-test-key-123");

        using var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(payload)
        };
        req2.Headers.Add("Idempotency-Key", "idem-test-key-123");

        var resp1 = await _client.SendAsync(req1);
        var resp2 = await _client.SendAsync(req2);

        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, resp2.StatusCode);

        var body1 = await resp1.Content.ReadFromJsonAsync<BookingResponseDto>();
        var body2 = await resp2.Content.ReadFromJsonAsync<BookingResponseDto>();

        Assert.NotNull(body1);
        Assert.NotNull(body2);
        Assert.Equal(body1!.Id, body2!.Id);
        Assert.Equal(body1.TotalPrice, body2.TotalPrice);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var count = await db.Bookings.CountAsync();
        Assert.Equal(1, count);
    }

    // Сценарій 6: Отримання своєї операції -> 200 OK і той самий набір полів, що й у baseline
    [Fact]
    public async Task Scenario06_GetById_OwnBooking_Returns200AndMatchesExpectedFields()
    {
        Authenticate(userId: 1);
        var createResp = await _client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            VenueId = 1,
            StartTime = new DateTime(2027, 5, 10, 14, 0, 0, DateTimeKind.Utc),
            Hours = 2
        });
        var created = await createResp.Content.ReadFromJsonAsync<BookingResponseDto>();

        var getResp = await _client.GetAsync($"/api/bookings/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var dto = await getResp.Content.ReadFromJsonAsync<BookingResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal(created.Id, dto!.Id);
        Assert.Equal(1, dto.UserId);
        Assert.Equal(1, dto.VenueId);
        Assert.Equal("Central Football Field", dto.VenueName);
        Assert.Equal("Main St 1", dto.VenueAddress);
        Assert.Equal(500.00m, dto.PricePerHour);
        Assert.Equal(2, dto.Hours);
        Assert.Equal(1000.00m, dto.TotalPrice);
        Assert.Equal("Confirmed", dto.Status);
    }

    // Сценарій 7: Спроба отримати чужу операцію -> 404 Not Found, чужі дані не розкриваються
    [Fact]
    public async Task Scenario07_GetById_OtherUsersBooking_Returns404WithoutLeakingData()
    {
        // Користувач 1 створює бронювання
        Authenticate(userId: 1);
        var createResp = await _client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            VenueId = 1,
            StartTime = new DateTime(2027, 5, 10, 14, 0, 0, DateTimeKind.Utc),
            Hours = 2
        });
        var created = await createResp.Content.ReadFromJsonAsync<BookingResponseDto>();

        // Користувач 2 намагається прочитати чуже бронювання
        Authenticate(userId: 2);
        var getResp = await _client.GetAsync($"/api/bookings/{created!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);

        var problem = await getResp.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem!.Status);
        Assert.Equal("Бронювання не знайдено", problem.Title);
    }

    // Сценарій 8: Скасування операції -> Статус змінився на Cancelled, повторне скасування оброблено коректно (400)
    [Fact]
    public async Task Scenario08_Cancel_OwnBooking_ChangesStatusAndHandlesRepeatCancel()
    {
        Authenticate(userId: 1);
        var createResp = await _client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            VenueId = 1,
            StartTime = new DateTime(2027, 5, 10, 14, 0, 0, DateTimeKind.Utc),
            Hours = 2
        });
        var created = await createResp.Content.ReadFromJsonAsync<BookingResponseDto>();

        // Перше скасування -> 200 OK зі статусом Cancelled
        var cancelResp1 = await _client.PostAsync($"/api/bookings/{created!.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResp1.StatusCode);

        var cancelledDto = await cancelResp1.Content.ReadFromJsonAsync<BookingResponseDto>();
        Assert.NotNull(cancelledDto);
        Assert.Equal("Cancelled", cancelledDto!.Status);

        // Повторне скасування -> 400 Bad Request ProblemDetails
        var cancelResp2 = await _client.PostAsync($"/api/bookings/{created.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, cancelResp2.StatusCode);

        var problem = await cancelResp2.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.Equal("Бронювання вже скасовано", problem.Title);
    }

    // Сценарій 9: Каталог недоступний (стаб повертає Unavailable) -> Сервіс не падає, повертає 503 ProblemDetails
    [Fact]
    public async Task Scenario09_Create_WhenCatalogUnavailable_Returns503ProblemDetails()
    {
        Authenticate(userId: 1);
        _factory.CatalogStub.SimulatedStatus = CatalogLookupStatus.Unavailable;

        var request = new CreateBookingRequest
        {
            VenueId = 1,
            StartTime = new DateTime(2027, 5, 15, 14, 0, 0, DateTimeKind.Utc),
            Hours = 2
        };

        var response = await _client.PostAsJsonAsync("/api/bookings", request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(503, problem!.Status);
        Assert.Contains("недоступний", problem.Title, StringComparison.OrdinalIgnoreCase);
    }

    // Сценарій 10: Список з пагінацією й фільтром -> Ті самі поля, сортування за спаданням Id і коректна кількість
    [Fact]
    public async Task Scenario10_GetList_WithPaginationAndStatusFilter_ReturnsFilteredSortedPage()
    {
        Authenticate(userId: 1);

        var r1 = await _client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            VenueId = 1,
            StartTime = new DateTime(2027, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            Hours = 2
        });
        var b1 = await r1.Content.ReadFromJsonAsync<BookingResponseDto>();

        var r2 = await _client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            VenueId = 2,
            StartTime = new DateTime(2027, 6, 2, 12, 0, 0, DateTimeKind.Utc),
            Hours = 3
        });
        var b2 = await r2.Content.ReadFromJsonAsync<BookingResponseDto>();

        // Скасовуємо перше бронювання, щоб перевірити фільтрацію за status=Confirmed
        await _client.PostAsync($"/api/bookings/{b1!.Id}/cancel", null);

        var listResp = await _client.GetAsync("/api/bookings?page=1&pageSize=10&status=Confirmed");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var listDto = await listResp.Content.ReadFromJsonAsync<BookingListDto>();
        Assert.NotNull(listDto);
        Assert.Equal(1, listDto!.Page);
        Assert.Equal(10, listDto.PageSize);
        Assert.Equal(1, listDto.TotalCount);
        Assert.Single(listDto.Items);
        Assert.Equal(b2!.Id, listDto.Items[0].Id);
        Assert.Equal("Confirmed", listDto.Items[0].Status);
        Assert.Equal("Tennis Club Kyiv", listDto.Items[0].VenueName);
    }
}
