using AuthService.Models;

namespace AuthService.Repositories;

public interface IRefreshTokenRepository : IBaseRepository<RefreshToken>
{
    Task<RefreshToken?> GetActiveTokenAsync(string token);
    Task RevokeAllUserTokensAsync(int userId);
}
