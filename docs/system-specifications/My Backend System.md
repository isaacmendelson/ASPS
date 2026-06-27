My Backend System

Build backend solution as a Visual Studio 2022 solution, C# using .NET10, with Entity Framework and MySql database. Send me the solution in a zipped file.

The solution will have the following projects:

- Backend (This is the main startup project. It loads Common, Interface and Business projects.)

- Common

- Interface

- Business

- WebApi

The main Backend project will be called “ASPSBackend”.

The solution has multi startup projects. When it starts, ASPSBackend and WebApi are started.

Actions received in the WebApi methods will be sent to the Business via NetMQ

When a WebApi method is invoked, a Command or Query will be created, and will be sent to the Business section via NetMQ.

The Business section will have a QueryHandler/CommandHandler for each Query/Command from the WebApi.

The handlers return a QueryResult/CommandResult.

Business . Data:

Business.Data.EF:

Will Include Repositories for:

- Users

- UserDevices

- UserAccounts

- DeviceAlerts

- AnalysisResults

Business . Views:

Will include a class ASView, which is a IDomainEventHandler, IBackgroundTask,

On system start, ASView initializes and will load into memory all Users, UserDevices and UserAccounts.

The Business section will also have a NetMQ RealTime listener that will receive messages via a dedicated port specified in appsettings.json file (e.g. 50001).

These messages are alerts from remote devices.

There are different alert types, all inheriting from class DeviceAlert.

Each alert type will be saved to a different table.

When the listener receives a new DeviceAlert message fires a Domain Event of type “DeviceAlertReceived”

In app.Business shall be folder Realtime Analysis:

In Realtime Analysis shall be folder UserDomain, containing the following objects:

UDAnalysisManager analysis manager and many more analyzer objects, each performing analysis of a specific DeviceAlert. For example: UDRemoteAccessAnalyzer, UDPhishingAnalyzer, etc.

When the Business app starts, it reads a list of Users from the database, and creates an instance of UDAnalysisManager for each active User.

UDAnalysisManager Object:

When a UDAnalysisManager instance starts, it loads a list of specific Analyzers.

The UDAnalysisManager is a Background Service. An instance will run for each user. When it starts,  it loads a list of specific Analyzers, and registers to DeviceAlert events (added, closed etc.). It handles events (such as DeviceAlertAdded, DeviceAlertUpdated and DeviceAlertClosed).

With the event DeviceAlertAdded, The UDAnalysisManager creates and starts a new UDAnalysis object (unless it exists).

UDAnalysis Object:

When created it will get the list of specific-analyzer objects from the PDAnalysisManager.

It has a current list of active UDAlerts and DeviceAlers.

When called to Analyze, it will invoke the “Analyze” function for each specific-analyzer.

After receiving analysis results from all analyzers, UDAnalysis Sets its internal property “result”, of type UDAnalysisResult. It can also create and publish alerts of type UDAlert, update or close active alerts.

Specific-analyzer object:

Each Specific-analyzer object handles a specific type of alert.

Its “Analyze” function creates and publishes alerts of type UDAlert, if issues are detected.

In the backend projects, install nugget packages supporting add–migration, etc.

AS Solution Folder Structure

|  |  |  |  |  |  |
| --- | --- | --- | --- | --- | --- |
| Clients |  |  |  |  |  |
|  | AS.Clients.WebApi |  |  |  |  |
|  | AS.Clients.WebApi.Tests |  |  |  |  |
|  |  |  |  |  |  |
| Common |  |  |  |  |  |
|  | AS.Common |  |  |  |  |
|  |  | Alerts |  |  |  |
|  |  | Configurations |  |  |  |
|  |  | Devices |  |  |  |
|  |  |  | Configuration |  |  |
|  |  |  | Control |  |  |
|  |  |  | SoftwareUpdate |  |  |
|  |  | ErrorMessages |  |  |  |
|  |  | Logging |  |  |  |
|  |  | Serialization |  |  |  |
|  |  | Users |  |  |  |
|  |  | Validation |  |  |  |
|  | AS.Common.Tests |  |  |  |  |
|  |  |  |  |  |  |
| Server |  |  |  |  |  |
|  | AS.Business |  |  |  |  |
|  |  | Admin |  |  |  |
|  |  | Alerts |  |  |  |
|  |  | Configurations |  |  |  |
|  |  | Data |  |  | Contains EntityFramework folder and misc. files: IASContext and all repository interfaces |
|  |  |  | EF |  |  |
|  |  |  |  | Migrations |  |
|  |  | Framework |  |  | Contains Command & Query dispatchers and validators |
|  |  | Logging |  |  |  |
|  |  | NotificationsManager |  |  |  |
|  |  |  |  |  |  |
|  |  | Realtimelysis |  |  | Contains a NetMQ BackgoundService RealTimeReportListener, that handles DeviceAlerts. |
|  |  |  |  |  |  |
|  |  | UserDevices |  |  |  |
|  |  | UserNotificationsService |  |  |  |
|  |  | Users |  |  |  |
|  |  | Views |  |  |  |
|  |  |  | ActiveAlertsView |  |  |
|  |  |  | Alerts |  |  |
|  |  |  | Configurations |  |  |
|  |  |  | Data |  |  |
|  |  |  | Devices |  |  |
|  |  |  | UserViews |  | Contains in-memory data objects (e.g. UserView, UserDeviceView, IAlertViewRepository, etc. All inheriting from ASView.cs) |
|  |  |  |  |  |  |
|  |  |  |  |  |  |
|  | AS.Business.Interface |  |  |  | Contains Commands, Queries, CommandResults, QueryResults for Users, Devices, Alerts etc. in separate folders. |
|  |  |  | Admin |  |  |
|  |  |  | Alerts |  |  |
|  |  |  | Dashboards |  |  |
|  |  |  | Logging |  |  |
|  |  |  | Reports |  |  |
|  |  |  | Security |  |  |
|  |  |  | Serialization |  |  |
|  |  |  | Users |  |  |
|  |  |  | UserDevices |  |  |
|  |  |  | UserAccounts |  |  |
|  |  |  | Validation |  |  |
|  |  |  | Views |  |  |
|  |  |  |  |  |  |
|  | AS.Business.Tests |  |  |  | Contains Unit tets |
|  |  |  |  |  |  |
|  | AS.NetMQ |  |  |  | Contains: INetMQJsonSeerializer, NetMQMessageSink, NetMQMessageSource |
|  |  |  |  |  |  |
|  | ASBackend |  |  |  | Contains BackendHub, Bootstrapper, BusinessService,BusinessServiceActor, ASServie, ASServiceControl, NotificationsWebServer, etc. |

Interfaces:

| ITag | ITag | ITag |
| --- | --- | --- |
| Key | Key |  |
| string | Name |  |
| string | Type |  |

Entity Objects:

| Key : IEquatable<Key>, IXmlSerializable | Key : IEquatable<Key>, IXmlSerializable | Key : IEquatable<Key>, IXmlSerializable |
| --- | --- | --- |
| string | Type |  |
| string | Value |  |
| string? | InstanceName |  |

| Tag : ITag, IEquatable<Tag> | Tag : ITag, IEquatable<Tag> | Tag : ITag, IEquatable<Tag> |
| --- | --- | --- |
| Key | Key |  |
| string | Name |  |
| string | Type |  |
| string? | BaseType |  |

| UserItem | UserItem | UserItem |
| --- | --- | --- |
| int | Key |  |
| string | KeycloakUserId |  |
| string | FirstName |  |
| String | LastName |  |
| DateTime | DateCreated |  |
| DateTime | DateModified |  |
| DateTime | DateDeleted |  |
| bool | IsDeleted |  |
| bool | IsDisabled |  |
| String | Address |  |
| String | City |  |
| String | State |  |
| String | Zip |  |
| String | Country |  |
| UserRole | Role |  |
| int? | GuardianKey |  |

| Key | Key | Key |
| --- | --- | --- |
| string | Type |  |
| string | Value |  |
| string | TypeName |  |
| string | InstanceName |  |

| ITag | ITag | ITag |
| --- | --- | --- |
| Key | Key |  |
| string | Name |  |
| string | Type |  |

| Tag : ITag | Tag : ITag | Tag : ITag |
| --- | --- | --- |
| Key | Key |  |
| string | Name |  |
| string | Type |  |
| string | BaseType |  |

| Entity | Entity | Entity |
| --- | --- | --- |
| Key | Key |  |
| Tag | Tag |  |
| string | TypeName |  |
| DateTime | DateCreated |  |
| DateTime? | DateModified |  |
| DateTime? | DateDeleted |  |
| bool | IsDeleted |  |
| bool | IsDisabled |  |

| User : Entity | User : Entity | User : Entity |
| --- | --- | --- |
| Key | Key |  |
| string | KeycloakUserId |  |
| string | FirstName |  |
| String | LastName |  |
| String | Address |  |
| String | City |  |
| String | State |  |
| String | Zip |  |
| String | Country |  |
| string | PhoneNumber | Main phone number |
| UserRole | Role |  |
| int? | GuardianKey |  |
| string? | Locale |  |
| int? | Timezone |  |

| UserAccount | UserAccount | UserAccount |
| --- | --- | --- |
| Key | Key |  |
| Key | UserKey |  |
| int | AccountType |  |
| String | LoginUrl |  |
| String | UserName |  |
| String | PasswordHash |  |
| bool | Is2FactorAuth |  |
| String | LoginPhoneNumber |  |
| DateTime | DateCreated |  |
| DateTime | DateModified |  |
| DateTime | DateDeleted |  |
| bool | IsDeleted |  |
| bool | IsDisabled |  |

| UserDevice (abstract) : Entity | UserDevice (abstract) : Entity | UserDevice (abstract) : Entity |
| --- | --- | --- |
| int | AggregateVersionField |  |
| Key? | UserKey |  |
| int | DeviceType |  |
| string | DeviceUid |  |
| string? | PhoneNumber |  |
| int | OperatingSystem |  |
| string? | MAC |  |
| string? | IMEI |  |
| string? | BiosSerial |  |
| string? | Make |  |
| string? | Model |  |
| string? | Serial |  |
| DateTime | DateCreated |  |
| DateTime? | DateModified |  |
| DateTime? | DateDeleted |  |
| bool | IsDeleted |  |
| bool | IsDisabled |  |
| DeviceMonitoringStatus | MonitoringStatus |  |

| PersonalComputer : UserDevice | PersonalComputer : UserDevice | PersonalComputer : UserDevice |
| --- | --- | --- |
| PersonalComputerType | Type |  |
| string | MotherboardSerial |  |
| string | BiosSerial |  |
| string | UserAgent |  |
| int | Timezone |  |
| OperatingSystemType | OperatingSystem |  |

| SmartPhone: UserDevice | SmartPhone: UserDevice | SmartPhone: UserDevice |
| --- | --- | --- |
| string | PhoneNumber |  |

| DeviceInfo | DeviceInfo | DeviceInfo |
| --- | --- | --- |
| Key | Key |  |
| string | DeviceUid |  |
| string | AggregateVersion |  |
| string | IP |  |
| string | UserAgent |  |
| int | Timezone |  |
| OperatingSystemType | OperatingSystem |  |
|  |  |  |

| DeviceMessage | DeviceMessage | DeviceMessage |
| --- | --- | --- |
| Priority | Priority |  |
| DeviceInfo | DeviceInfo |  |
| DateTime | Timestamp |  |
| string | Token |  |

| DeviceAlert: DeviceMessage, IDeviceAlert | DeviceAlert: DeviceMessage, IDeviceAlert | DeviceAlert: DeviceMessage, IDeviceAlert |
| --- | --- | --- |
|  |  |  |

| RemoteAccessAlert :DeviceAlert | RemoteAccessAlert :DeviceAlert | RemoteAccessAlert :DeviceAlert |
| --- | --- | --- |
| RemoteAccessApp | RemoteAccessApp |  |
| int | RunningProcesses |  |
| string | ConnectionUrl |  |
| int | ConnectionStatus |  |
| int | ConnectionsCount |  |
| int | SessionStatus |  |

| UrlAlert : DeviceAlert | UrlAlert : DeviceAlert | UrlAlert : DeviceAlert |
| --- | --- | --- |
| string | Url |  |
| Key[] | Trackers |  |
| string[] | IFrameDomains |  |
| string | UserAgent |  |

| DeviceAlertReceived : DomainEvent | DeviceAlertReceived : DomainEvent | DeviceAlertReceived : DomainEvent |
| --- | --- | --- |
| DeviceAlert | Alert |  |
| Priority | Priority |  |
| string | DeviceUid |  |
| DateTime | ReceiveTimestamp |  |
| DateTime | MessageTimestamp |  |

| RemoteAccessAlertVM | RemoteAccessAlertVM | RemoteAccessAlertVM |
| --- | --- | --- |
| string | DeviceUid |  |
| int | RemoteAccessApp |  |
| int | RunningProcesses |  |
| string | ConnectionUrl |  |
| int | ConnectionStatus |  |
| int | ConnectionsCount |  |
| int | SessionStatus |  |
| Severity | Severity |  |
| DateTime | Timestamp |  |

| PhishingAlertVM | PhishingAlertVM | PhishingAlertVM |
| --- | --- | --- |
| string | DeviceUid |  |
| string | Url |  |
| Severity | Severity |  |
| DateTime | Timestamp |  |

| VoiceCallAlertVM | VoiceCallAlertVM | VoiceCallAlertVM |
| --- | --- | --- |
| string | DeviceUid |  |
| string | PhoneNumber |  |
| Binary | VoiceSample |  |
| Severity | Severity |  |
| DateTime | Timestamp |  |

| DeviceRegistrationRequest | DeviceRegistrationRequest | DeviceRegistrationRequest |
| --- | --- | --- |
| DateTime | Timestamp |  |
| string | UserToken |  |
| DeviceInfo | DeviceInfo |  |
| string | RequestId |  |

| DeviceRegistrationResponse | DeviceRegistrationResponse | DeviceRegistrationResponse |
| --- | --- | --- |
| string | RequestId |  |
| string? | DeviceUid |  |
| bool? | HasError |  |
| string? | ErrorMessage |  |

| AnalysisResultContainer: Entity | AnalysisResultContainer: Entity | AnalysisResultContainer: Entity |
| --- | --- | --- |
| Key | UserKey |  |
| string | Discriminator |  |
| string? | JsonValue |  |
| bool? | HasError |  |
| string? | ErrorMessage |  |
| DateTime | Timestamp |  |
| bool | IsFromCache |  |

| UrlAnalysisResultContainer: AnalysisResultContainer | UrlAnalysisResultContainer: AnalysisResultContainer | UrlAnalysisResultContainer: AnalysisResultContainer |
| --- | --- | --- |
| string | Domain |  |
| string | Url |  |

| AlertFlag | AlertFlag | AlertFlag |
| --- | --- | --- |
| int | Key |  |
| int | UserKey |  |
| int | SensorType |  |
| DateTime | Created |  |
| AlertFlagType | AlertFlagType |  |
| AlertFlagStatus | Status |  |
| bool | IsDeleted |  |
| DateTime | Deleted |  |
| DateTime | Modified |  |

User Domain:

| UDUser | UDUser | UDUser |
| --- | --- | --- |
| Key | Key |  |
| string | FirstName |  |
| String | LastName |  |
| String | Address |  |
| String | City |  |
| String | State |  |
| String | Zip |  |
| String | Country |  |
| string | PhoneNumber | Main phone number |
| UserRole | Role |  |
| int? | GuardianKey |  |
| string? | Locale |  |
| int? | Timezone |  |
| IEnumerable<DeviceAlert> | ActiveAlerts |  |
|  |  |  |

Interfaces:

internal interface IDomainEventHandler

{

void Handle(IDomainEvent evt);

Type[] GetHandleableEvents();

}

+++++++++++++++++++++++++++

public interface IBackgroundTask

{

void Start();

void Stop();

}

Enumerations:

AccountType

| Email: 1 |
| --- |
| Communication: 2 |
| Social: 3 |
| Financial: 4 |
| Other : 5 |

DeviceType

| Unknown: 0 |
| --- |
| PersonalComputer: 1 |
| SmartPhone: 2 |
| Other: 3 |

DeviceMonitoringStatus

| Disabled: 0 |
| --- |
| Enabled: 1 |

OperatingSystem

| Windows: 1 |
| --- |
| Linux: 2 |
| MAC: 3 |
| Android: 4 |
| IOS : 5 |

RemoteAccessApp

| AnyDesk: 1 |
| --- |
| TeamViewer: 2, |
| ChromeRemoteDesktop: 3 |
| RemotePC: 4 |
| LogMeIn: 5 |
| Splashtop: 6 |
| VNC: 7 |

UserRole

| Unknown: 0 |
| --- |
| Self: 1 |
| Guardian: 2 |
| Other: 3 |

CautionLevel

| Low 0 |
| --- |
| Medium: 1 |
| High: 2 |
|  |

AlertFlagType

| None 0 |
| --- |
| RemoteAccess_AppRunning: 1 |
| RemoteAccess_ConnectionOpen: 2 |
| RemoteAccess_SessionActive: 3 |

AlertFlagStatus

| Unknown: 0 |
| --- |
| Open: 1 |
| Closed: 2 |

ConnectionStatus

| Unknown: 0 |
| --- |
| Open: 1 |
| Closed: 2 |

OperatingSystemType

| Unknown: 0 |
| --- |
| Windows: 1 |
| Linux : 2 |
| Mac = 3 |
| Android: 4 |
| IOS: 5 |

PersonalComputerType

| Unknown: 0 |
| --- |
| Desktop: 1 |
| Laptop : 2 |
| Tablet : 3 |

Frontend Application

Frontend will be built with Angular

Sections:

Users

Login

Dashboard - After user logs in, he is directed to the Dashboard page.

Profile - User Display / Edit his profile

Devices - List user devices. Enable create new, update and delete

Accounts - List user accounts. Enable create new, update and delete

Alerts - List alerts received from any of that user\s devices

Events

Admin

Login

Dashboard

Users

All Users - List all users

Profile - Specific user’s profile

Devices- Specific user’s devices

Accounts- Specific user’s accounts

Alerts - List alerts received from any of that user\s devices

Events Log

Output:

Send the output backend system and frontend in separate zipped files.
