using AuthService.Models;

namespace AuthService.Repositories;

public interface IUserRepository : IBaseRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<bool> ExistsByEmailAsync(string email);
    Task<User?> GetWithRolesAsync(int id);
    Task<Role?> GetRoleByNameAsync(string roleName);
    Task<List<Role>> GetRolesByNamesAsync(List<string> roleNames);
    Task<(List<User> Items, int TotalCount)> GetPagedUsersAsync(int page, int pageSize);
    Task AddUserWithRoleAsync(User user, string roleName);
}
