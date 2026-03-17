using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Version information endpoint
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VersionController : ControllerBase
{
    /// <summary>
    /// Get WebApi version
    /// </summary>
    [HttpGet]
    public IActionResult GetVersion()
    {
        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0.1";
        
        return Ok(new
        {
            version = version,
            component = "WebApi"
        });
    }
}
