using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(6)]
    [MaxLength(100)]
    public string Password { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = null!;
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = null!;
}

public class LogoutRequest
{
    [Required]
    public string RefreshToken { get; set; } = null!;
}

public class UpdateRolesRequest
{
    [Required]
    [MinLength(1)]
    public List<string> Roles { get; set; } = [];
}

public record TokenResponse(string AccessToken, string RefreshToken);
public record UserDto(int Id, string Email, string DisplayName, List<string> Roles);
public record UserListDto(List<UserDto> Items, int Page, int PageSize, int TotalCount);
