using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
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
            //GitCommitId = ThisAssembly.GitCommitId,
            //GitCommitId = ThisAssembly.AssemblyInformationalVersion
            GitCommitId = typeof(SystemController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? "unknown",
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
