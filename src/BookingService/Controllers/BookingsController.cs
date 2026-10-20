using System.Security.Claims;
using BookingService.Dtos;
using BookingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingService.Controllers;

/// <summary>
/// Контролер операційного сервісу керування бронюваннями спортивних майданчиків.
/// Маршрути повністю сумісні зі старим монолітом (/api/bookings).
/// </summary>
[ApiController]
[Route("api/bookings")]
[Authorize]
[Produces("application/json")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    /// <summary>
    /// Отримати список власних бронювань поточного користувача з пагінацією та фільтром за статусом.
    /// </summary>
    /// <param name="page">Номер сторінки (починаючи з 1).</param>
    /// <param name="pageSize">Кількість записів на сторінці (від 1 до 100).</param>
    /// <param name="status">Необов'язковий фільтр за статусом: Confirmed або Cancelled.</param>
    /// <param name="ct">Токен скасування асинхронної операції.</param>
    [HttpGet]
    [ProducesResponseType<BookingListDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyBookings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _bookingService.GetUserBookingsAsync(userId, page, pageSize, status, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Отримати список усіх бронювань у системі з фільтрами (лише для адміністратора).
    /// </summary>
    /// <param name="page">Номер сторінки (починаючи з 1).</param>
    /// <param name="pageSize">Кількість записів на сторінці (від 1 до 100).</param>
    /// <param name="status">Фільтр за статусом (Confirmed / Cancelled).</param>
    /// <param name="userId">Фільтр за ID користувача.</param>
    /// <param name="venueId">Фільтр за ID спортивного майданчика.</param>
    /// <param name="ct">Токен скасування асинхронної операції.</param>
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<BookingListDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] int? userId = null,
        [FromQuery] int? venueId = null,
        CancellationToken ct = default)
    {
        var result = await _bookingService.GetAllBookingsAsync(page, pageSize, status, userId, venueId, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Отримати деталі одного бронювання за ідентифікатором (доступно власнику або адміністратору).
    /// Повертає 404 Not Found, якщо бронювання не існує або належить іншому користувачу.
    /// </summary>
    /// <param name="id">Ідентифікатор бронювання.</param>
    /// <param name="ct">Токен скасування асинхронної операції.</param>
    [HttpGet("{id:int}")]
    [ProducesResponseType<BookingResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");
        var result = await _bookingService.GetByIdAsync(id, userId, isAdmin, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Створити нове бронювання спортивного майданчика.
    /// Перевіряє існування майданчика у CatalogService (з таймаутом 2 с) та підтримує заголовок Idempotency-Key.
    /// </summary>
    /// <param name="request">Параметри нового бронювання (VenueId, StartTime, Hours).</param>
    /// <param name="idempotencyKey">Необов'язковий унікальний ключ для захисту від повторного створення.</param>
    /// <param name="ct">Токен скасування асинхронної операції.</param>
    [HttpPost]
    [ProducesResponseType<BookingResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBookingRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _bookingService.CreateAsync(userId, request, idempotencyKey, ct);
        if (!result.IsSuccess)
            return ToActionResult(result);

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    /// <summary>
    /// Змінити час початку або тривалість власного підтвердженого бронювання.
    /// </summary>
    /// <param name="id">Ідентифікатор бронювання.</param>
    /// <param name="request">Нові параметри часу та тривалості.</param>
    /// <param name="ct">Токен скасування асинхронної операції.</param>
    [HttpPut("{id:int}")]
    [ProducesResponseType<BookingResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateBookingRequest request,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _bookingService.UpdateAsync(id, userId, request, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Скасувати бронювання (переводить статус у Cancelled без фізичного видалення запису з БД).
    /// Доступно власнику бронювання або адміністратору.
    /// </summary>
    /// <param name="id">Ідентифікатор бронювання.</param>
    /// <param name="ct">Токен скасування асинхронної операції.</param>
    [HttpPost("{id:int}/cancel")]
    [HttpDelete("{id:int}")]
    [ProducesResponseType<BookingResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");
        var result = await _bookingService.CancelAsync(id, userId, isAdmin, ct);
        return ToActionResult(result);
    }

    private int GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(sub, out var parsedId) ? parsedId : 0;
    }

    private IActionResult ToActionResult<T>(ServiceResult<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Data);

        return result.ErrorType switch
        {
            ServiceErrorType.Validation => ValidationProblem(new ValidationProblemDetails(result.ValidationErrors!)),
            ServiceErrorType.BadRequest => Problem(
                title: result.Title,
                detail: result.Detail,
                statusCode: StatusCodes.Status400BadRequest),
            ServiceErrorType.NotFound => Problem(
                title: result.Title,
                detail: result.Detail,
                statusCode: StatusCodes.Status404NotFound),
            ServiceErrorType.ServiceUnavailable => Problem(
                title: result.Title,
                detail: result.Detail,
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
