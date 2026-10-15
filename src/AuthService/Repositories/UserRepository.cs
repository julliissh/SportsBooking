using AuthService.Data;
using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AuthDbContext _db;

    public UserRepository(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetByIdAsync(int id)
        => await _db.Users.FindAsync(id);

    public async Task<User?> GetByEmailAsync(string email)
        => await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

    public async Task<bool> ExistsByEmailAsync(string email)
        => await _db.Users.AnyAsync(u => u.Email == email);

    public async Task<User?> GetWithRolesAsync(int id)
        => await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);

    public async Task<Role?> GetRoleByNameAsync(string roleName)
        => await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName);

    public async Task<List<Role>> GetRolesByNamesAsync(List<string> roleNames)
        => await _db.Roles.Where(r => roleNames.Contains(r.Name)).ToListAsync();

    public async Task<(List<User> Items, int TotalCount)> GetPagedUsersAsync(int page, int pageSize)
    {
        var query = _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.IsActive);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task AddUserWithRoleAsync(User user, string roleName)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName)
            ?? throw new InvalidOperationException($"Role {roleName} not found.");

        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();

        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await _db.SaveChangesAsync();

        // reload user roles
        await _db.Entry(user).Collection(u => u.UserRoles).Query().Include(ur => ur.Role).LoadAsync();
    }

    public async Task AddAsync(User entity)
        => await _db.Users.AddAsync(entity);

    public void Remove(User entity)
        => _db.Users.Remove(entity);

    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();
}
