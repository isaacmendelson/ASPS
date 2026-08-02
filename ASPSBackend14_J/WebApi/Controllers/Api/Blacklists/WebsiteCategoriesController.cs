using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Business.Commands;
using Business.Queries;
using Common.Models;
using WebApi.Services;

namespace WebApi.Controllers.Api.Blacklists;

/// <summary>
/// Blacklists API — Website Categories.
/// JIRA: ASPS-648
/// </summary>
[ApiController]
[Route("api/blacklists/website-categories")]
[Authorize(Roles = "Admin")]
public class WebsiteCategoriesController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<WebsiteCategoriesController> _logger;

    public WebsiteCategoriesController(ICQRSClient cqrsClient, ILogger<WebsiteCategoriesController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/blacklists/website-categories
    /// Returns a paged list of all website categories.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request)
    {
        request.Normalize();
        try
        {
            var query = new GetAllWebsiteCategoriesQuery
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
            };

            var result = await _cqrsClient.SendQueryAsync<GetAllWebsiteCategoriesQueryResult>(query);

            if (!result.Success)
            {
                _logger.LogWarning("GetAllWebsiteCategoriesQuery failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            var dtos = result.Categories.Select(c => new WebsiteCategoryDto
            {
                Key = c.KeyField,
                Name = c.Name,
                ParentId = string.IsNullOrEmpty(c.ParentId) ? null : c.ParentId,
                ParentName = c.Parent?.Name,
                Source = c.Source,
                DateCreated = c.DateCreated,
                IsDeleted = c.IsDeleted,
            }).ToList();

            var pagedResult = new PagedResult<WebsiteCategoryDto>
            {
                Items = dtos,
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize,
            };

            return Ok(pagedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting website categories");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/blacklists/website-categories/parents
    /// Returns the list of parent (top-level) categories only.
    /// </summary>
    [HttpGet("parents")]
    public async Task<IActionResult> GetParents()
    {
        try
        {
            var query = new GetParentCategoriesQuery();
            var result = await _cqrsClient.SendQueryAsync<GetParentCategoriesQueryResult>(query);

            if (!result.Success)
            {
                _logger.LogWarning("GetParentCategoriesQuery failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            var dtos = result.Parents.Select(c => new WebsiteCategoryDto
            {
                Key = c.KeyField,
                Name = c.Name,
                ParentId = string.IsNullOrEmpty(c.ParentId) ? null : c.ParentId,
                ParentName = c.Parent?.Name,
                Source = c.Source,
                DateCreated = c.DateCreated,
                IsDeleted = c.IsDeleted,
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting parent website categories");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/blacklists/website-categories/by-name/{name}
    /// Returns a single website category looked up by name.
    /// </summary>
    [HttpGet("by-name/{name}")]
    public async Task<IActionResult> GetByName(string name)
    {
        try
        {
            var query = new GetWebsiteCategoryByNameQuery { Name = name };
            var result = await _cqrsClient.SendQueryAsync<GetWebsiteCategoryByNameQueryResult>(query);

            if (!result.Success || result.Category == null)
            {
                return NotFound(new { message = $"Website category '{name}' not found." });
            }

            var dto = new WebsiteCategoryDto
            {
                Key = result.Category.KeyField,
                Name = result.Category.Name,
                ParentId = string.IsNullOrEmpty(result.Category.ParentId) ? null : result.Category.ParentId,
                ParentName = result.Category.Parent?.Name,
                Source = result.Category.Source,
                DateCreated = result.Category.DateCreated,
                IsDeleted = result.Category.IsDeleted,
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting website category by name: {Name}", name);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// POST /api/blacklists/website-categories
    /// Creates a new website category.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWebsiteCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        try
        {
            var command = new CreateWebsiteCategoryCommand
            {
                Name = request.Name.Trim(),
                ParentId = request.ParentId,
                Source = request.Source,
            };

            var result = await _cqrsClient.SendCommandAsync<CreateWebsiteCategoryCommandResult>(command);

            if (!result.Success)
            {
                _logger.LogWarning("CreateWebsiteCategoryCommand failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            return Ok(new { message = result.Message, categoryId = result.CategoryId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating website category: {Name}", request.Name);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// PUT /api/blacklists/website-categories/{key}
    /// Updates an existing website category (identified by its name / key).
    /// The {key} path segment is the category Name (used as the lookup key).
    /// </summary>
    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, [FromBody] UpdateWebsiteCategoryRequest request)
    {
        try
        {
            var command = new UpdateWebsiteCategoryCommand
            {
                Name = key,
                ParentId = request.ParentId,
                Source = request.Source,
            };

            var result = await _cqrsClient.SendCommandAsync<UpdateWebsiteCategoryCommandResult>(command);

            if (!result.Success)
            {
                _logger.LogWarning("UpdateWebsiteCategoryCommand failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            return Ok(new { message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating website category: {Key}", key);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
