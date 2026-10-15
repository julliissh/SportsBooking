using AuthService.Dtos;

namespace AuthService.Services;

public interface IUserService
{
    Task<UserDto?> GetMeAsync(int userId);
    Task<UserListDto> GetAllAsync(int page, int pageSize);
    Task<bool> UpdateRolesAsync(int userId, List<string> roles);
}
