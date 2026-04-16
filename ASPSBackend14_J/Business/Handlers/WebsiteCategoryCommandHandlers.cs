using Business.Commands;
using Common.Entities;
using Interface.Repositories;
using Microsoft.Extensions.Logging;

namespace Business.Handlers;

/// <summary>
/// Command handlers for WebsiteCategory operations.
/// JIRA: SCRUM-822
/// </summary>
public class WebsiteCategoryCommandHandlers
{
    private readonly IWebsiteCategoryRepository _repository;
    private readonly ILogger<WebsiteCategoryCommandHandlers> _logger;

    public WebsiteCategoryCommandHandlers(
        IWebsiteCategoryRepository repository,
        ILogger<WebsiteCategoryCommandHandlers> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public virtual async Task<CreateWebsiteCategoryCommandResult> HandleAsync(CreateWebsiteCategoryCommand command)
    {
        try
        {
            // Validate: Check if category with this name already exists
            var exists = await _repository.ExistsAsync(command.Name);
            if (exists)
            {
                return new CreateWebsiteCategoryCommandResult
                {
                    Success = false,
                    Message = $"A category with the name '{command.Name}' already exists."
                };
            }

            // Create new category
            var category = new WebsiteCategory(command.Name, command.ParentId ?? string.Empty, command.Source);
            var categoryId = await _repository.AddAsync(category);

            _logger.LogInformation("Created website category: {Name} (ID: {Id})", command.Name, categoryId);

            return new CreateWebsiteCategoryCommandResult
            {
                Success = true,
                CategoryId = categoryId,
                Message = "Website category created successfully."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating website category: {Name}", command.Name);
            return new CreateWebsiteCategoryCommandResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<UpdateWebsiteCategoryCommandResult> HandleAsync(UpdateWebsiteCategoryCommand command)
    {
        try
        {
            // Get existing category by name
            var category = await _repository.GetByNameAsync(command.Name);
            if (category == null)
            {
                return new UpdateWebsiteCategoryCommandResult
                {
                    Success = false,
                    Message = $"Category '{command.Name}' not found."
                };
            }

            // Update fields (Name cannot be changed based on requirements)
            category.ParentId = command.ParentId ?? string.Empty;
            category.Source = command.Source;

            await _repository.UpdateAsync(category);

            _logger.LogInformation("Updated website category: {Name}", command.Name);

            return new UpdateWebsiteCategoryCommandResult
            {
                Success = true,
                Message = "Website category updated successfully."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating website category: {Name}", command.Name);
            return new UpdateWebsiteCategoryCommandResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
