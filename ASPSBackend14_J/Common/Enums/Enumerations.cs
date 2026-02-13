namespace Common.Enums;

public enum AccountType
{
    Email = 1,
    Communication = 2,
    Social = 3,
    Financial = 4,
    Other = 5
}

public enum DeviceType
{
    Unknown = 0,
    PersonalComputer = 1,
    SmartPhone = 2,
    Other = 3
}

public enum DeviceMonitoringStatus
{
    Disabled = 0,
    Enabled = 1
}


public enum OperatingSystemType
{
    Unknown = 0,
    Windows = 1,
    Linux = 2,
    Mac = 3,
    Android = 4,
    IOS = 5
}

public enum RemoteAccessApp
{
    Unknown = 0,
    AnyDesk = 1,
    TeamViewer = 2,
    ChromeRemoteDesktop = 3,
    RemotePC = 4,
    LogMeIn = 5,
    Splashtop = 6,
    VNC = 7
}

public enum UserRole
{
    Unknown = 0,
    Self = 1,
    Guardian = 2,
    Other = 3
}

public enum CautionLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum AlertFlagType
{
    None = 0,
    RemoteAccess_AppRunning = 1,
    RemoteAccess_ConnectionOpen = 2,
    RemoteAccess_SessionActive = 3
}

public enum AlertFlagStatus
{
    Unknown = 0,
    Open = 1,
    Closed = 2
}

public enum ConnectionStatus
{
    Unknown = 0,
    Open = 1,
    Closed = 2
}



public enum PersonalComputerType
{
    Unknown = 0,
    Desktop = 1,
    Laptop = 2,
    Tablet = 3
}

public enum Priority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum Severity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum ResultStatusCode
{
    Success = 200,
    InvalidOperation = 400,
    ValidationError = 422,
    ServerError = 500,
    Unauthenticated = 401,
    Unauthorized = 403,
    NotFound = 404
}


public enum ProtectiveActionType
{
    None = 0,
    DisplayNotification = 1,
    EmailNotification = 2,
    SoundAlert = 3,
    BlockUrl = 4,
    UserDisplayNotification = 5,
    QuarantineDevice = 6,
    BlockRemoteAccess = 7,
}

public enum ProtectiveActionSubject
{
    none = 0,
    Device = 1,
    User = 2,
    Protector = 3
}

public enum  AnalysisLevel
{
    Unknown = 0,
    Device = 1,
    User = 2
}




