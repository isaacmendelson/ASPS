using Common.Models.Alerts;
using DeviceAlert = Common.Models.DeviceAlert;

namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Represents a device alert with its analysis state and metadata.
/// Tracks alerts from user's devices along with their analysis results.
/// </summary>
public class ActiveDeviceAlert
{
    /// <summary>
    /// The device that sent this alert
    /// </summary>
    public string DeviceUid { get; set; } = string.Empty;
    
    /// <summary>
    /// The actual device alert data
    /// </summary>
    public DeviceAlert Alert { get; set; } = null!;
    
    /// <summary>
    /// The Key of the DeviceAlert entity in the database
    /// </summary>
    public string DeviceAlertEntityKey { get; set; } = string.Empty;
    
    /// <summary>
    /// When this alert was received
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// The analysis result - NULL initially, filled when analysis completes
    /// </summary>
    public UDAnalysisResult? AnalysisResult { get; set; }
    
    public ActiveDeviceAlert()
    {
        Timestamp = DateTime.UtcNow;
    }
    
    public ActiveDeviceAlert(string deviceUid, DeviceAlert alert, string deviceAlertEntityKey)
    {
        DeviceUid = deviceUid;
        Alert = alert;
        DeviceAlertEntityKey = deviceAlertEntityKey;
        Timestamp = DateTime.UtcNow;
        AnalysisResult = null;
    }
}
