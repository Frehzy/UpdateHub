using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using UpdateHub.Server.Api.V1.DTOs.Request;
using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Application.Abstractions.Services;

namespace UpdateHub.Server.Api.V1.Controllers;

[ApiController]
[Route("api/v1/admin")]
public class AdminController(
    IClientService clientService,
    IManifestService manifestService,
    IStatisticsService statisticsService,
    IGroupService groupService,
    IUserRepository userRepository,
    IMapper mapper,
    ILogger<AdminController> logger) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? role = null)
    {
        var users = await userRepository.GetAllAsync();

        if (!string.IsNullOrEmpty(role))
        {
            users = users.Where(u => u.Role.ToString() == role);
        }

        var response = mapper.Map<IEnumerable<UserResponseDto>>(users);
        return Ok(new { users = response, total = response.Count() });
    }

    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUser(string userId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new ErrorResponseDto { Error = "User not found" });
        }

        var response = mapper.Map<UserResponseDto>(user);
        return Ok(response);
    }

    [HttpPut("users/{userId}/status")]
    public async Task<IActionResult> ToggleUserStatus(string userId, [FromBody] ToggleUserStatusRequestDto request)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new ErrorResponseDto { Error = "User not found" });
        }

        user.IsActive = request.IsActive;
        await userRepository.UpdateAsync(user);

        return Ok(new { user.Id, user.IsActive });
    }

    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new ErrorResponseDto { Error = "User not found" });
        }

        // Мягкое удаление
        user.IsActive = false;
        await userRepository.UpdateAsync(user);

        return NoContent();
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshManifest()
    {
        try
        {
            await manifestService.RefreshManifestAsync();
            return Ok(new { status = "ok", message = "Manifest refreshed successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing manifest");
            return StatusCode(500, new ErrorResponseDto { Error = "Failed to refresh manifest" });
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] int? days)
    {
        try
        {
            var stats = await statisticsService.GetStatisticsAsync(days);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting statistics");
            return StatusCode(500, new ErrorResponseDto { Error = "Failed to get statistics" });
        }
    }

    [HttpPost("clients")]
    public async Task<IActionResult> CreateClient([FromBody] CreateClientRequestDto request)
    {
        try
        {
            var client = await clientService.CreateClientAsync(request);
            return CreatedAtAction(nameof(GetClient), new { id = client.Id }, new
            {
                client.Id,
                client.GroupId,
                client.CreatedAt,
                client.IsActive
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponseDto { Error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpGet("clients")]
    public async Task<IActionResult> GetClients([FromQuery] string? groupId, [FromQuery] bool? isBlocked, [FromQuery] string? search)
    {
        var clients = await clientService.GetAllClientsAsync(groupId, isBlocked, search);
        var response = clients.Select(c => new
        {
            c.Id,
            c.GroupId,
            c.ComputerInfo,
            c.IsBlocked,
            c.IsActive,
            c.CreatedAt,
            c.UpdatedAt
        });

        return Ok(new { clients = response, total = response.Count() });
    }

    [HttpGet("clients/{id}")]
    public async Task<IActionResult> GetClient(string id)
    {
        try
        {
            var client = await clientService.GetClientDetailAsync(id);
            return Ok(client);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpPut("clients/{id}")]
    public async Task<IActionResult> UpdateClient(string id, [FromBody] UpdateClientRequestDto request)
    {
        try
        {
            var client = await clientService.UpdateClientAsync(id, request);
            return Ok(new { client.Id, client.GroupId, client.IsBlocked, client.IsActive });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpDelete("clients/{id}")]
    public async Task<IActionResult> DeleteClient(string id)
    {
        try
        {
            await clientService.DeleteClientAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpPost("clients/{id}/block")]
    public async Task<IActionResult> BlockClient(string id, [FromBody] BlockClientRequestDto request)
    {
        try
        {
            var blockedBy = HttpContext.Items["Username"]?.ToString() ?? "admin";
            await clientService.BlockClientAsync(id, request.Reason ?? "No reason provided", blockedBy);
            return Ok(new { status = "ok", message = "Client blocked" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpPost("clients/{id}/unblock")]
    public async Task<IActionResult> UnblockClient(string id)
    {
        try
        {
            await clientService.UnblockClientAsync(id);
            return Ok(new { status = "ok", message = "Client unblocked" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponseDto { Error = ex.Message });
        }
    }

    // Groups endpoints
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups()
    {
        var groups = await groupService.GetAllGroupsAsync();
        return Ok(new { groups });
    }

    [HttpGet("groups/{id}")]
    public async Task<IActionResult> GetGroup(string id)
    {
        try
        {
            var group = await groupService.GetGroupDetailAsync(id);
            return Ok(group);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequestDto request)
    {
        try
        {
            var group = await groupService.CreateGroupAsync(request.Name, request.Description);
            return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, group);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpPut("groups/{id}")]
    public async Task<IActionResult> UpdateGroup(string id, [FromBody] UpdateGroupRequestDto request)
    {
        try
        {
            var group = await groupService.UpdateGroupAsync(id, request.Name, request.Description);
            return Ok(group);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpDelete("groups/{id}")]
    public async Task<IActionResult> DeleteGroup(string id)
    {
        try
        {
            await groupService.DeleteGroupAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpPost("users/{userId}/clients")]
    public async Task<IActionResult> AddUserClientAccess(string userId, [FromBody] AddUserAccessRequestDto request)
    {
        try
        {
            await groupService.AddUserClientAccessAsync(userId, request.ClientId!);
            return Ok(new { status = "ok" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpDelete("users/{userId}/clients/{clientId}")]
    public async Task<IActionResult> RemoveUserClientAccess(string userId, string clientId)
    {
        try
        {
            await groupService.RemoveUserClientAccessAsync(userId, clientId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpPost("users/{userId}/groups")]
    public async Task<IActionResult> AddUserGroupAccess(string userId, [FromBody] AddUserAccessRequestDto request)
    {
        try
        {
            await groupService.AddUserGroupAccessAsync(userId, request.GroupId!);
            return Ok(new { status = "ok" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponseDto { Error = ex.Message });
        }
    }

    [HttpDelete("users/{userId}/groups/{groupId}")]
    public async Task<IActionResult> RemoveUserGroupAccess(string userId, string groupId)
    {
        try
        {
            await groupService.RemoveUserGroupAccessAsync(userId, groupId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponseDto { Error = ex.Message });
        }
    }
}