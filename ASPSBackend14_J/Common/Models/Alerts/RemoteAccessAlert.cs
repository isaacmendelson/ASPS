using Common.Enums;

namespace Common.Models.Alerts;

public class RemoteAccessAlert : DeviceAlert
{
    protected RemoteAccessAlert() { }
    public RemoteAccessAlert(RemoteAccessApp remoteAccessApp, int runningProcesses, string connectionUrl, ConnectionStatus connectionStatus, int connectionsCount, int sessionStatus)
    {
        RemoteAccessApp = remoteAccessApp;
        RunningProcesses = runningProcesses;
        ConnectionUrl = connectionUrl;
        ConnectionStatus = connectionStatus;
        ConnectionsCount = connectionsCount;
        SessionStatus = sessionStatus;
    }

    public RemoteAccessApp RemoteAccessApp { get; set; }
    public int RunningProcesses { get; set; }
    public string ConnectionUrl { get; set; } = string.Empty;
    public ConnectionStatus ConnectionStatus { get; set; }
    public int ConnectionsCount { get; set; }
    public int SessionStatus { get; set; }
}
