using Microsoft.AspNetCore.Mvc;
using UpdateHub.Server.Api.V1.DTOs.Request;
using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Application.Abstractions.Services;

namespace UpdateHub.Server.Api.V1.Controllers;

[ApiController]
[Route("api/v1/update")]
public class UpdateController(IUpdateService updateService, ILogger<UpdateController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Update([FromForm] CheckRequestDto request)
    {
        try
        {
            var clientId = HttpContext.Items["ClientId"]?.ToString();
            if (!string.IsNullOrEmpty(clientId) && request.ClientInfo != null)
            {
                request.ClientInfo.ClientId = clientId;
            }

            var response = await updateService.UpdateAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new ErrorResponseDto { Error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Update endpoint");
            return StatusCode(500, new ErrorResponseDto { Error = "Internal server error" });
        }
    }
}