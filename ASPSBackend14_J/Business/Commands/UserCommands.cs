using Common.Entities;
using Common.Enums;
using Common.Messaging;
using Common.Models;

namespace Business.Commands;

// Commands
public class CreateUserCommand : Command
{
    public string KeycloakUserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}

public class CreateUserCommandResult : CommandResult
{
    public Key? UserKey { get; set; }
}

public class UpdateUserCommand : Command
{
    public Key UserKey { get; set; } = new Key();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public class UpdateUserCommandResult : CommandResult { }

public class DeleteUserCommand : Command
{
    public DeleteUserCommand()
    {
        CommandType = nameof(DeleteUserCommand);
    }
    
    public string CommandType { get; set; } = string.Empty;
    public Key UserKey { get; set; } = new Key();
}

public class DeleteUserCommandResult : CommandResult { }
