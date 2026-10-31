namespace ApiGateway.Clients;

public interface IAuthGwClient
{
    Task<bool> CheckHealthAsync(CancellationToken ct = default);
}
