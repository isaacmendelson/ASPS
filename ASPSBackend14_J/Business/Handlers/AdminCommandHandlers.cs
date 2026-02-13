using Business.Commands;
using Business.DomainEvents;
using Business.Views;
using Common.Entities;
using Interface.Repositories;

namespace Business.Handlers;

public class AdminCommandHandlers
{
    private readonly IUserRepository _userRepository;
    private readonly ASView _asView;

    public AdminCommandHandlers(IUserRepository userRepository, ASView asView)
    {
        _userRepository = userRepository;
        _asView = asView;
    }

    public async Task<CreateUserAdminCommandResult> HandleAsync(CreateUserAdminCommand command)
    {
        try
        {
            var user = new User
            {
                KeyField = Guid.NewGuid().ToString(),
                KeycloakUserId = command.KeycloakUserId ?? Guid.NewGuid().ToString(),
                FirstName = command.FirstName,
                LastName = command.LastName,
                Address = command.Address ?? string.Empty,
                City = command.City ?? string.Empty,
                State = command.State ?? string.Empty,
                Zip = command.Zip ?? string.Empty,
                Country = command.Country ?? string.Empty,
                PhoneNumber = command.PhoneNumber ?? string.Empty,
                Email = command.Email ?? string.Empty,
                Role = command.Role,
                Locale = command.Locale,
                Timezone = command.Timezone,
                GuardianKey = command.GuardianKey
            };

            var created = await _userRepository.AddAsync(user);

            await _asView.Handle(new UserAdded(created));

            return new CreateUserAdminCommandResult
            {
                Success = true,
                Message = "User created successfully",
                UserKey = created.Key
            };
        }
        catch (Exception ex)
        {
            return new CreateUserAdminCommandResult
            {
                Success = false,
                Message = $"Error creating user: {ex.Message}"
            };
        }
    }
}
