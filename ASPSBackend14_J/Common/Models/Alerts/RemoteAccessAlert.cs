using Common.Enums;

namespace Common.Models.Alerts;

public class RemoteAccessAlert : DeviceAlert
{

    protected RemoteAccessAlert() { }
    public RemoteAccessAlert(RemoteAccessApp remoteAccessApp, int runningProcesses, string connectionUrl, ConnectionStatus connectionStatus, RemoteAccessDirection remoteAccessDirection,
        int connectionsCount, int sessionStatus, BrowserTab[]? browserTabs)
    {
        RemoteAccessApp = remoteAccessApp;
        RunningProcesses = runningProcesses;
        ConnectionUrl = connectionUrl;
        ConnectionStatus = connectionStatus;
        ConnectionsCount = connectionsCount;
        SessionStatus = sessionStatus;
        BrowserTabs = browserTabs;
        RemoteAccessDirection = remoteAccessDirection;
    }

    //protected RemoteAccessAlert() { }
    //public RemoteAccessAlert(RemoteAccessApp remoteAccessApp, int runningProcesses, string connectionUrl, ConnectionStatus connectionStatus, int connectionsCount, int sessionStatus)
    //{
    //    RemoteAccessApp = remoteAccessApp;
    //    RunningProcesses = runningProcesses;
    //    ConnectionUrl = connectionUrl;
    //    ConnectionStatus = connectionStatus;
    //    ConnectionsCount = connectionsCount;
    //    SessionStatus = sessionStatus;
    //}
    public RemoteAccessDirection RemoteAccessDirection { get; set; }
    public RemoteAccessApp RemoteAccessApp { get; set; }
    public int RunningProcesses { get; set; }
    public string ConnectionUrl { get; set; } = string.Empty;
    public ConnectionStatus ConnectionStatus { get; set; }
    public int ConnectionsCount { get; set; }
    public int SessionStatus { get; set; }

    public BrowserTab[]? BrowserTabs { get; set; }

    // Deep detection fields
    public string? RemoteOS { get; set; }
    public string? RemoteVersion { get; set; }
    public string? ConnectionType { get; set; }
    public bool FileTransferActive { get; set; }
    public int FileTransfers { get; set; }
}
