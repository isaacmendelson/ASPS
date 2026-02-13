using Common.Enums;
using Common.Messaging;
using Common.Models;

namespace Business.Commands;

// Enhanced Create User Command (with all admin fields)
public class CreateUserAdminCommand : Command
{
    public CreateUserAdminCommand()
    {
        CommandType = nameof(CreateUserAdminCommand);
    }
    
    public string CommandType { get; set; }
    public string? KeycloakUserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? Country { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public UserRole Role { get; set; }
    public string? Locale { get; set; }
    public int? Timezone { get; set; }
    public int? GuardianKey { get; set; }
}

public class CreateUserAdminCommandResult : CommandResult
{
    public Key? UserKey { get; set; }
}
