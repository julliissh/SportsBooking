using AuthService.Data;
using AuthService.Dtos;
using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public class AuthServiceImpl : IAuthService
{
    private readonly AuthDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthServiceImpl> _logger;

    public AuthServiceImpl(AuthDbContext db, ITokenService tokenService, ILogger<AuthServiceImpl> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<(TokenResponse? Tokens, string? Error)> RegisterAsync(RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return (null, "Email already taken");

        var userRole = await _db.Roles.FirstAsync(r => r.Name == "User");

        var user = new User
        {
            Email = request.Email,
            DisplayName = request.DisplayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
        await _db.SaveChangesAsync();

        // reload with roles
        user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == user.Id);

        var tokens = await IssueTokensAsync(user);
        _logger.LogInformation("User registered: {Email}", request.Email);
        return (tokens, null);
    }

    public async Task<(TokenResponse? Tokens, string? Error)> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

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
        var token = await _db.RefreshTokens
            .Include(t => t.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.Token == refreshToken);

        if (token is null || !token.IsActive)
            return (null, "Invalid or expired refresh token");

        var newRefresh = _tokenService.GenerateRefreshToken();

        token.RevokedAt = DateTime.UtcNow;
        token.ReplacedByToken = newRefresh;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = token.UserId,
            Token = newRefresh,
            ExpiresAt = _tokenService.GetRefreshTokenExpiry()
        });

        await _db.SaveChangesAsync();

        var accessToken = _tokenService.GenerateAccessToken(token.User);
        return (new TokenResponse(accessToken, newRefresh), null);
    }

    public async Task<bool> LogoutAsync(string refreshToken)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
        if (token is null) return false;

        token.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<TokenResponse> IssueTokensAsync(User user)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = _tokenService.GetRefreshTokenExpiry()
        });
        await _db.SaveChangesAsync();

        return new TokenResponse(accessToken, refreshToken);
    }
}
