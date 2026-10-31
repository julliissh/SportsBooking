namespace ApiGateway.Clients;

public class AuthGwClient : IAuthGwClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AuthGwClient> _logger;

    public AuthGwClient(HttpClient http, ILogger<AuthGwClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            using var resp = await _http.GetAsync("/health", cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AuthService health check failed");
            return false;
        }
    }
}
