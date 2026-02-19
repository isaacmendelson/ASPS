using Common.Enums;
using Common.Models;

namespace Common.Models;

public class DeviceInfo
{

    public DeviceInfo()
    {
    }
    public DeviceInfo(Key key, string deviceUid, string aggregateVersion, string iP, string userAgent, int timezone, 
        OperatingSystemType operatingSystem, string? mACAddress, Key? userKey = null)
    {
        Key = key;
        DeviceUid = deviceUid;
        AggregateVersion = aggregateVersion;
        IP = iP;
        UserAgent = userAgent;
        Timezone = timezone;
        OperatingSystem = operatingSystem;
        UserKey = userKey;
        MACAddress = mACAddress;
    }

    public Key Key { get; set; } = new Key();
    public string DeviceUid { get; set; } = string.Empty;
    public string AggregateVersion { get; set; } = string.Empty;
    public string IP { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public int Timezone { get; set; }
    public OperatingSystemType OperatingSystem { get; set; }
    public Key? UserKey { get; set; }

    public string? MACAddress { get; set; }

    public DeviceType DeviceType
    {
        get
        {
            return OperatingSystem switch
            {
                OperatingSystemType.Windows => DeviceType.PersonalComputer,
                OperatingSystemType.MacOS => DeviceType.PersonalComputer,
                OperatingSystemType.Linux => DeviceType.PersonalComputer,
                OperatingSystemType.Android => DeviceType.MobilePhone,
                OperatingSystemType.IOS => DeviceType.MobilePhone,
                _ => DeviceType.Unknown
            };
        }
    }

}
