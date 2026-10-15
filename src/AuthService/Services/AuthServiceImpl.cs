using AuthService.Dtos;
using AuthService.Models;
using AuthService.Repositories;

namespace AuthService.Services;

public class AuthServiceImpl : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _tokenRepo;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthServiceImpl> _logger;

    public AuthServiceImpl(
        IUserRepository userRepo,
        IRefreshTokenRepository tokenRepo,
        ITokenService tokenService,
        ILogger<AuthServiceImpl> logger)
    {
        _userRepo = userRepo;
        _tokenRepo = tokenRepo;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<(TokenResponse? Tokens, string? Error)> RegisterAsync(RegisterRequest request)
    {
        if (await _userRepo.ExistsByEmailAsync(request.Email))
            return (null, "Email already taken");

        var user = new User
        {
            Email = request.Email,
            DisplayName = request.DisplayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        await _userRepo.AddUserWithRoleAsync(user, "User");

        var tokens = await IssueTokensAsync(user);
        _logger.LogInformation("User registered: {Email}", request.Email);
        return (tokens, null);
    }

    public async Task<(TokenResponse? Tokens, string? Error)> LoginAsync(LoginRequest request)
    {
        var user = await _userRepo.GetByEmailAsync(request.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for {Email}", request.Email);
            return (null, "Invalid credentials");
        }

        var tokens = await IssueTokensAsync(user);
        _logger.LogInformation("User logged in: {Email}", request.Email);
        return (tokens, null);
    }

    public async Task<(TokenResponse? Tokens, string? Error)> RefreshAsync(string refreshToken)
    {
        var token = await _tokenRepo.GetActiveTokenAsync(refreshToken);

        if (token is null || !token.IsActive)
            return (null, "Invalid or expired refresh token");

        var newRefresh = _tokenService.GenerateRefreshToken();

        token.RevokedAt = DateTime.UtcNow;
        token.ReplacedByToken = newRefresh;

        await _tokenRepo.AddAsync(new RefreshToken
        {
            UserId = token.UserId,
            Token = newRefresh,
            ExpiresAt = _tokenService.GetRefreshTokenExpiry()
        });

        await _tokenRepo.SaveChangesAsync();

        var accessToken = _tokenService.GenerateAccessToken(token.User);
        return (new TokenResponse(accessToken, newRefresh), null);
    }

    public async Task<bool> LogoutAsync(string refreshToken)
    {
        var token = await _tokenRepo.GetActiveTokenAsync(refreshToken);
        if (token is null) return false;

        token.RevokedAt = DateTime.UtcNow;
        await _tokenRepo.SaveChangesAsync();
        return true;
    }

    private async Task<TokenResponse> IssueTokensAsync(User user)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        await _tokenRepo.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = _tokenService.GetRefreshTokenExpiry()
        });
        await _tokenRepo.SaveChangesAsync();

        return new TokenResponse(accessToken, refreshToken);
    }
}
