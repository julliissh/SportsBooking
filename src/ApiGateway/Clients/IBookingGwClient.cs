using ApiGateway.Dtos;

namespace ApiGateway.Clients;

public interface IBookingGwClient
{
    Task<GwResult<ActiveBookingDto>> GetMyActiveAsync(
        int venueId,
        string userId,
        string bearerToken,
        string correlationId,
        CancellationToken ct = default);

    Task<bool> CheckHealthAsync(CancellationToken ct = default);
}
