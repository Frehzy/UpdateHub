using Microsoft.AspNetCore.Mvc;
using UpdateHub.Server.Api.V1.DTOs.Request;
using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Application.Abstractions.Services;

namespace UpdateHub.Server.Api.V1.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService authService, ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthRequestDto request)
    {
        try
        {
            var userAgent = Request.Headers.UserAgent.ToString();
            var response = await authService.LoginAsync(request, userAgent);

            if (response.MustChangePassword)
            {
                return Ok(new
                {
                    response.AccessToken,
                    response.RefreshToken,
                    response.TokenType,
                    response.ExpiresIn,
                    response.UserId,
                    response.Username,
                    response.Role,
                    response.ClientId,
                    MustChangePassword = true,
                    Message = "Password must be changed"
                });
            }

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto request)
    {
        try
        {
            var response = await authService.RefreshTokenAsync(request.RefreshToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequestDto request)
    {
        var userId = HttpContext.Items["UserId"]?.ToString();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ErrorResponseDto { Error = "User not authenticated" });
        }

        await authService.LogoutAsync(request.RefreshToken, userId);
        return NoContent();
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        var userId = HttpContext.Items["UserId"]?.ToString();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ErrorResponseDto { Error = "User not authenticated" });
        }

        try
        {
            await authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
            return Ok(new { Message = "Password changed successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ErrorResponseDto { Error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] CreateUserRequestDto request)
    {
        var userRole = HttpContext.Items["UserRole"]?.ToString();
        if (userRole != "Admin")
        {
            return Forbid();
        }

        try
        {
            var user = await authService.CreateUserAsync(
                request.Username,
                request.Password,
                request.Role,
                request.GroupIds,
                request.ClientIds);

            return CreatedAtAction(nameof(Register), new { id = user.Id }, new
            {
                user.Id,
                user.Username,
                user.Role,
                user.CreatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponseDto { Error = ex.Message });
        }
    }
}