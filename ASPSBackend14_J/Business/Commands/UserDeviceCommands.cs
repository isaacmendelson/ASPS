using Common.Enums;
using Common.Messaging;
using Common.Models;

namespace Business.Commands;

public class CreateUserDeviceCommand : Command
{
    public CreateUserDeviceCommand()
    {
        CommandType = nameof(CreateUserDeviceCommand);
    }

    public string CommandType { get; set; }
    public Key UserKey { get; set; } = new Key();
    public DeviceType DeviceType { get; set; }
    public string DeviceUid { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public OperatingSystemType OperatingSystem { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
}

public class CreateUserDeviceCommandResult : CommandResult
{
    public Key? DeviceKey { get; set; }
}

public class UpdateUserDeviceCommand : Command
{
    public Key DeviceKey { get; set; } = new Key();
    public DeviceMonitoringStatus MonitoringStatus { get; set; }
}

public class UpdateUserDeviceCommandResult : CommandResult { }

public class DeleteUserDeviceCommand : Command
{
    public Key DeviceKey { get; set; } = new Key();
}

public class DeleteUserDeviceCommandResult : CommandResult { }
