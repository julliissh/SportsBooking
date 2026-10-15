using AuthService.Data;
using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AuthDbContext _db;

    public RefreshTokenRepository(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<RefreshToken?> GetByIdAsync(int id)
        => await _db.RefreshTokens.FindAsync(id);

    public async Task<RefreshToken?> GetActiveTokenAsync(string token)
        => await _db.RefreshTokens
            .Include(t => t.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.Token == token);

    public async Task RevokeAllUserTokensAsync(int userId)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();
        foreach (var t in tokens)
            t.RevokedAt = DateTime.UtcNow;
    }

    public async Task AddAsync(RefreshToken entity)
        => await _db.RefreshTokens.AddAsync(entity);

    public void Remove(RefreshToken entity)
        => _db.RefreshTokens.Remove(entity);

    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();
}
