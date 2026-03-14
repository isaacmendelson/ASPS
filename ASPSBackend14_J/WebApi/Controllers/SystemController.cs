using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    /// <summary>
    /// Get system version information
    /// </summary>
    [HttpGet("version")]
    public IActionResult GetVersion()
    {
        return Ok(new
        {
            Version = ThisAssembly.AssemblyInformationalVersion,
            BuildDate = DateTime.UtcNow,
            GitCommitId = ThisAssembly.GitCommitId,
            IsPrerelease = ThisAssembly.IsPrerelease,
            IsPublicRelease = ThisAssembly.IsPublicRelease
        });
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow
        });
    }
}
