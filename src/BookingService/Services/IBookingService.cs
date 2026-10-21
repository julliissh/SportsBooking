using BookingService.Dtos;

namespace BookingService.Services;

public enum ServiceErrorType
{
    None,
    Validation,
    BadRequest,
    NotFound,
    ServiceUnavailable
}

public record ServiceResult<T>(
    T? Data,
    ServiceErrorType ErrorType = ServiceErrorType.None,
    string? Title = null,
    string? Detail = null,
    Dictionary<string, string[]>? ValidationErrors = null)
{
    public bool IsSuccess => ErrorType == ServiceErrorType.None;

    public static ServiceResult<T> Success(T data) => new(data);

    public static ServiceResult<T> ValidationFailure(Dictionary<string, string[]> errors) =>
        new(default, ServiceErrorType.Validation, ValidationErrors: errors);

    public static ServiceResult<T> BadRequest(string title, string detail) =>
        new(default, ServiceErrorType.BadRequest, Title: title, Detail: detail);

    public static ServiceResult<T> NotFound(string title, string detail) =>
        new(default, ServiceErrorType.NotFound, Title: title, Detail: detail);

    public static ServiceResult<T> Unavailable(string title, string detail) =>
        new(default, ServiceErrorType.ServiceUnavailable, Title: title, Detail: detail);
}

public interface IBookingService
{
    Task<ServiceResult<BookingListDto>> GetUserBookingsAsync(
        int userId,
        int page,
        int pageSize,
        string? status,
        CancellationToken ct = default);

    Task<ServiceResult<BookingListDto>> GetAllBookingsAsync(
        int page,
        int pageSize,
        string? status,
        int? userId,
        int? venueId,
        CancellationToken ct = default);

    Task<ServiceResult<BookingResponseDto>> GetByIdAsync(
        int id,
        int currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<ServiceResult<BookingResponseDto>> CreateAsync(
        int currentUserId,
        CreateBookingRequest request,
        string? idempotencyKey,
        CancellationToken ct = default);

    Task<ServiceResult<BookingResponseDto>> UpdateAsync(
        int id,
        int currentUserId,
        UpdateBookingRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<BookingResponseDto>> CancelAsync(
        int id,
        int currentUserId,
        bool isAdmin,
        CancellationToken ct = default);
}
