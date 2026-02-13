using Common.Enums;
using Common.Models;

namespace WebApi.DTOs;

// User DTOs
public class CreateUserRequest
{
    public string KeycloakUserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}

public class UpdateUserRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public class UserResponse
{
    public Key Key { get; set; } = new Key();
    public string KeycloakUserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime DateCreated { get; set; }
}

public class UserDetailsResponse : UserResponse
{
    public List<UserDeviceResponse> Devices { get; set; } = new();
    public List<UserAccountResponse> Accounts { get; set; } = new();
}

// UserDevice DTOs
public class CreateUserDeviceRequest
{
    public string UserKeyType { get; set; } = string.Empty;
    public string UserKeyValue { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public string DeviceUid { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public OperatingSystemType OperatingSystem { get; set; }
}

public class UpdateUserDeviceRequest
{
    public DeviceMonitoringStatus MonitoringStatus { get; set; }
}

public class UserDeviceResponse
{
    public Key Key { get; set; } = new Key();
    public DeviceType DeviceType { get; set; }
    public string DeviceUid { get; set; } = string.Empty;
    public DeviceMonitoringStatus MonitoringStatus { get; set; }
    public DateTime DateCreated { get; set; }
}

// UserAccount DTOs
public class UserAccountResponse
{
    public Key Key { get; set; } = new Key();
    public AccountType AccountType { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
}
