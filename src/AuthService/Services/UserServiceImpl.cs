using AuthService.Data;
using AuthService.Dtos;
using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public class UserServiceImpl : IUserService
{
    private readonly AuthDbContext _db;

    public UserServiceImpl(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<UserDto?> GetMeAsync(int userId)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

        return user is null ? null : ToDto(user);
    }

    public async Task<UserListDto> GetAllAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.IsActive);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new UserListDto(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<bool> UpdateRolesAsync(int userId, List<string> roleNames)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) return false;

        var roles = await _db.Roles.Where(r => roleNames.Contains(r.Name)).ToListAsync();
        if (roles.Count != roleNames.Count) return false;

        _db.UserRoles.RemoveRange(user.UserRoles);
        foreach (var role in roles)
            _db.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });

        await _db.SaveChangesAsync();
        return true;
    }

    private static UserDto ToDto(User u) =>
        new(u.Id, u.Email, u.DisplayName, u.UserRoles.Select(ur => ur.Role.Name).ToList());
}
