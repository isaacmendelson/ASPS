using Common.Messaging;

namespace Business.Commands;

/// <summary>
/// Command to create a new website category.
/// JIRA: SCRUM-822
/// </summary>
public class CreateWebsiteCategoryCommand : Command
{
    public CreateWebsiteCategoryCommand()
    {
        CommandType = nameof(CreateWebsiteCategoryCommand);
    }

    public string CommandType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string? Source { get; set; }
}

public class CreateWebsiteCategoryCommandResult : CommandResult
{
    public int CategoryId { get; set; }
}

/// <summary>
/// Command to update an existing website category.
/// JIRA: SCRUM-822
/// </summary>
public class UpdateWebsiteCategoryCommand : Command
{
    public UpdateWebsiteCategoryCommand()
    {
        CommandType = nameof(UpdateWebsiteCategoryCommand);
    }

    public string CommandType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string? Source { get; set; }
}

public class UpdateWebsiteCategoryCommandResult : CommandResult
{
}
