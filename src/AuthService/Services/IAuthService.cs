using AuthService.Dtos;

namespace AuthService.Services;

public interface IAuthService
{
    Task<(TokenResponse? Tokens, string? Error)> RegisterAsync(RegisterRequest request);
    Task<(TokenResponse? Tokens, string? Error)> LoginAsync(LoginRequest request);
    Task<(TokenResponse? Tokens, string? Error)> RefreshAsync(string refreshToken);
    Task<bool> LogoutAsync(string refreshToken);
}
