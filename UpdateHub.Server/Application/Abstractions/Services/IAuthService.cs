using UpdateHub.Server.Api.V1.DTOs.Request;
using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Services;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(AuthRequestDto request, string? userAgent);
    Task<RefreshResponseDto> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(string refreshToken, string userId);
    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword);
    Task<UserEntity> CreateUserAsync(string username, string password, string role, List<string>? groupIds, List<string>? clientIds);
    Task<bool> HasAccessToClientAsync(string userId, string clientId);
    Task<bool> HasAccessToAnyClientAsync(string userId);
    Task<IEnumerable<string>> GetUserClientIdsAsync(string userId);
    Task<IEnumerable<string>> GetUserGroupIdsAsync(string userId);
}