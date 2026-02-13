using Business.Commands;
using Business.DomainEvents;
using Business.Queries;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Models;
using Interface.Repositories;

namespace Business.Handlers;

public class UserCommandHandlers
{
    private readonly IUserRepository _userRepository;
    private readonly ASView _asView;

    public UserCommandHandlers(IUserRepository userRepository, ASView asView)
    {
        _userRepository = userRepository;
        _asView = asView;
    }

    public async Task<CreateUserCommandResult> HandleAsync(CreateUserCommand command)
    {
        try
        {
            var user = new User
            {
                KeyField = Guid.NewGuid().ToString(),  // Set GUID as string
                KeycloakUserId = command.KeycloakUserId,
                FirstName = command.FirstName,
                LastName = command.LastName,
                Role = command.Role
            };

            var created = await _userRepository.AddAsync(user);

            await _asView.Handle(new UserAdded(created));

            return new CreateUserCommandResult
            {
                Success = true,
                Message = "User created successfully",
                UserKey = created.Key
            };
        }
        catch (Exception ex)
        {
            return new CreateUserCommandResult
            {
                Success = false,
                Message = $"Error creating user: {ex.Message}"
            };
        }
    }

    public async Task<UpdateUserCommandResult> HandleAsync(UpdateUserCommand command)
    {
        try
        {
            var user = await _userRepository.GetByKeyAsync(command.UserKey);
            if (user == null)
            {
                return new UpdateUserCommandResult { Success = false, Message = "User not found" };
            }

            user.FirstName = command.FirstName;
            user.LastName = command.LastName;
            user.Address = command.Address;
            user.City = command.City;
            user.PhoneNumber = command.PhoneNumber;

            await _userRepository.UpdateAsync(user);

            await _asView.Handle(new UserUpdated(user));

            return new UpdateUserCommandResult { Success = true, Message = "User updated successfully" };
        }
        catch (Exception ex)
        {
            return new UpdateUserCommandResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public async Task<DeleteUserCommandResult> HandleAsync(DeleteUserCommand command)
    {
        try
        {
            await _userRepository.DeleteAsync(command.UserKey);

            await _asView.Handle(new UserDeleted(Entity.GetDbKey(command.UserKey)));

            return new DeleteUserCommandResult { Success = true, Message = "User deleted successfully" };
        }
        catch (Exception ex)
        {
            return new DeleteUserCommandResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }
}

public class UserQueryHandlers
{
    private readonly IUserRepository _userRepository;

    public UserQueryHandlers(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<GetAllUsersQueryResult> HandleAsync(GetAllUsersQuery query)
    {
        try
        {
            Console.WriteLine("DEBUG: GetAllUsersQuery handler called");
            var users = await _userRepository.GetAllAsync();
            Console.WriteLine($"DEBUG: GetAllAsync returned {users.Count()} users");
            
            return new GetAllUsersQueryResult
            {
                Success = true,
                Message = "Users retrieved successfully",
                Users = users.ToList()
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in GetAllUsersQuery handler: {ex.Message}");
            return new GetAllUsersQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public async Task<GetUserByKeyQueryResult> HandleAsync(GetUserByKeyQuery query)
    {
        try
        {
            var user = await _userRepository.GetByKeyAsync(query.UserKey);
            return new GetUserByKeyQueryResult
            {
                Success = true,
                Message = user != null ? "User found" : "User not found",
                User = user
            };
        }
        catch (Exception ex)
        {
            return new GetUserByKeyQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public async Task<GetUserDetailsQueryResult> HandleAsync(GetUserDetailsQuery query)
    {
        try
        {
            var user = await _userRepository.GetUserWithDetailsAsync(query.UserKey);
            return new GetUserDetailsQueryResult
            {
                Success = true,
                Message = user != null ? "User details retrieved" : "User not found",
                User = user
            };
        }
        catch (Exception ex)
        {
            return new GetUserDetailsQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }
    
    public async Task<GetUserDevicesQueryResult> HandleAsync(GetUserDevicesQuery query)
    {
        try
        {
            // Need device repository - for now return empty
            return new GetUserDevicesQueryResult
            {
                Success = true,
                Message = "Devices query not yet implemented",
                Devices = new List<Common.Entities.UserDevice>()
            };
        }
        catch (Exception ex)
        {
            return new GetUserDevicesQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }
    
    public async Task<GetUserAccountsQueryResult> HandleAsync(GetUserAccountsQuery query)
    {
        try
        {
            // Need account repository - for now return empty
            return new GetUserAccountsQueryResult
            {
                Success = true,
                Message = "Accounts query not yet implemented",
                Accounts = new List<Common.Entities.UserAccount>()
            };
        }
        catch (Exception ex)
        {
            return new GetUserAccountsQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }
}

public class UserDeviceCommandHandlers
{
    private readonly IUserDeviceRepository _deviceRepository;

    public UserDeviceCommandHandlers(IUserDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<CreateUserDeviceCommandResult> HandleAsync(CreateUserDeviceCommand command)
    {
        try
        {
            UserDevice device;
            var deviceGuid = Guid.NewGuid().ToString();  // GUID as string

            if (command.DeviceType == Common.Enums.DeviceType.PersonalComputer)
            {
                device = new PersonalComputer
                {
                    KeyField = deviceGuid,  // Set GUID string
                    UserKey = command.UserKey,  // This uses the setter which converts to UserKeyField
                    DeviceUid = command.DeviceUid,
                    DeviceType = command.DeviceType,
                    OperatingSystem = command.OperatingSystem,
                    Make = command.Make,
                    Model = command.Model
                };
            }
            else
            {
                device = new SmartPhone
                {
                    KeyField = deviceGuid,  // Set GUID string
                    UserKey = command.UserKey,  // This uses the setter which converts to UserKeyField
                    DeviceUid = command.DeviceUid,
                    DeviceType = command.DeviceType,
                    PhoneNumber = command.PhoneNumber ?? "",
                    OperatingSystem = command.OperatingSystem,
                    Make = command.Make,
                    Model = command.Model
                };
            }

            var created = await _deviceRepository.AddAsync(device);

            return new CreateUserDeviceCommandResult
            {
                Success = true,
                Message = "Device created successfully",
                DeviceKey = created.Key  // This is fine - reading the computed Key property
            };
        }
        catch (Exception ex)
        {
            return new CreateUserDeviceCommandResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public async Task<UpdateUserDeviceCommandResult> HandleAsync(UpdateUserDeviceCommand command)
    {
        try
        {
            var device = await _deviceRepository.GetByKeyAsync(command.DeviceKey);
            if (device == null)
            {
                return new UpdateUserDeviceCommandResult { Success = false, Message = "Device not found" };
            }

            device.MonitoringStatus = command.MonitoringStatus;
            await _deviceRepository.UpdateAsync(device);

            return new UpdateUserDeviceCommandResult { Success = true, Message = "Device updated" };
        }
        catch (Exception ex)
        {
            return new UpdateUserDeviceCommandResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public async Task<DeleteUserDeviceCommandResult> HandleAsync(DeleteUserDeviceCommand command)
    {
        try
        {
            await _deviceRepository.DeleteAsync(command.DeviceKey);
            return new DeleteUserDeviceCommandResult { Success = true, Message = "Device deleted" };
        }
        catch (Exception ex)
        {
            return new DeleteUserDeviceCommandResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }
}
