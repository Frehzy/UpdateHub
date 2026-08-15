using Microsoft.AspNetCore.Mvc;
using UpdateHub.Server.Application.Abstractions.Services;

namespace UpdateHub.Server.Api.V1.Controllers;

[ApiController]
[Route("api/v1/download")]
public class DownloadController(IManifestService manifestService, ILogger<DownloadController> logger) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Download(string id)
    {
        try
        {
            var entry = await manifestService.GetEntryByIdAsync(id);
            if (entry == null)
            {
                return NotFound(new { error = "File not found" });
            }

            var filesPath = manifestService.GetFilesPath();
            var filePath = Path.Combine(filesPath, entry.RelativePath);

            if (!System.IO.File.Exists(filePath))
            {
                logger.LogWarning("File exists in manifest but not on disk: {Path}", entry.RelativePath);
                return NotFound(new { error = "File not found on disk" });
            }

            var fileStream = System.IO.File.OpenRead(filePath);
            return File(fileStream, "application/octet-stream", Path.GetFileName(entry.RelativePath));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading file {Id}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}