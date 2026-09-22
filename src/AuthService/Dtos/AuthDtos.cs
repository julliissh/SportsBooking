namespace AuthService.Dtos;

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);
public record UpdateRolesRequest(List<string> Roles);

public record TokenResponse(string AccessToken, string RefreshToken);
public record UserDto(int Id, string Email, string DisplayName, List<string> Roles);
public record UserListDto(List<UserDto> Items, int Page, int PageSize, int TotalCount);
