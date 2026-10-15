using AuthService.Dtos;
using AuthService.Models;
using AuthService.Repositories;

namespace AuthService.Services;

public class UserServiceImpl : IUserService
{
    private readonly IUserRepository _userRepo;

    public UserServiceImpl(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task<UserDto?> GetMeAsync(int userId)
    {
        var user = await _userRepo.GetWithRolesAsync(userId);
        return user is null ? null : ToDto(user);
    }

    public async Task<UserListDto> GetAllAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _userRepo.GetPagedUsersAsync(page, pageSize);
        return new UserListDto(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<bool> UpdateRolesAsync(int userId, List<string> roleNames)
    {
        var user = await _userRepo.GetWithRolesAsync(userId);
        if (user is null) return false;

        var roles = await _userRepo.GetRolesByNamesAsync(roleNames);
        if (roles.Count != roleNames.Count) return false;

        user.UserRoles.Clear();
        foreach (var role in roles)
            user.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });

        await _userRepo.SaveChangesAsync();
        return true;
    }

    private static UserDto ToDto(User u) =>
        new(u.Id, u.Email, u.DisplayName, u.UserRoles.Select(ur => ur.Role.Name).ToList());
}
