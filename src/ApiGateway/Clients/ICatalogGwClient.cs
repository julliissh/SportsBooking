using ApiGateway.Dtos;

namespace ApiGateway.Clients;

public interface ICatalogGwClient
{
    Task<GwResult<VenueDto>> GetVenueAsync(
        int venueId,
        string correlationId,
        CancellationToken ct = default);

    Task<bool> CheckHealthAsync(CancellationToken ct = default);
}
