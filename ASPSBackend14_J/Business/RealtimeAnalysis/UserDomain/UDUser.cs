using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using System.Collections.Generic;

namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Runtime representation of a User with active alerts and analysis state.
/// Each user has their own UDUser instance running in the background.
/// </summary>
public class UDUser 
{
    //protected UDUser() { }
    //public UDUser(Key key)
    //{
    //    Key = key;
    //    this.RiskAssessment = new RiskAssessment(0, "", false, 1);
    //}
    private IEnumerable<UserDeviceView> _userDevices;

    private int maxRemoteAccessAnalysisResults = 1000;
    public UDUser(Key key, UserInfo userInfo, RiskAssessment riskAssessment, IEnumerable<UserDeviceView>? userDevices, 
        IEnumerable<DeviceAlertView>? activeAlerts, Dictionary<string, IEnumerable<BrowserTab>>? browserTabs, bool? isTaregted)
    {
        RiskAssessment = riskAssessment;
        Key = key;
        UserInfo = userInfo;
        ActiveAlerts = activeAlerts ?? new List<DeviceAlertView>();
        UserDevices = userDevices ?? new List<UserDeviceView>();
        BrowserTabs = browserTabs ?? new();
        this.IsTargeted = isTaregted?? false;
    }

    // Core identity
    public Key Key { get; private set; }

    public RiskAssessment RiskAssessment { get; private set; }
    
    
    // User properties from User entity (excluding IsDeleted and KeyField)
   public UserInfo UserInfo { get; private set; }

    // Runtime analysis parameters
    public bool IsTargeted { get; private set; }    //True if user contact information is found in darknet lead lists.
    public Dictionary<string, IEnumerable<BrowserTab>>? BrowserTabs { get; private set; }
    public IEnumerable<DeviceAlertView> ActiveAlerts { get; set; }

    public Dictionary<string, List<RemoteAccessAnalysisResultVm>> RemoteAccessAnalysisResults { get; private set; }

    public Dictionary<string, RemoteAccessAnalysisResultVm> RemoteAccessStatus 
    { 
        get
        {
            var res = new Dictionary<string, RemoteAccessAnalysisResultVm>();
            foreach (var d in this._userDevices)
            {
                var r = this.RemoteAccessAnalysisResults[d.DeviceUid]
                    .Where(i => i.Success)
                    .OrderByDescending(i => i.analyzed_at).FirstOrDefault();
                if (r is not null)
                {
                    res[d.DeviceUid] = r;
                }
            }
            return res;
        }
    }

    public IEnumerable<UserDeviceView> UserDevices {
        get
        {
            return this._userDevices ?? new List<UserDeviceView>();
        } 
        set
        {
            this._userDevices = value;
        } 
    
    }
    
    //List of phishing & other suspicious urls received in messages (email, sms, WhatsApp):
    public Dictionary<string, IEnumerable<UserDeviceUrlSurfData>> UserUrlSurfDataByDevice { get; set; }

    
    public void SetUserIsTargeted(bool value)
    {
        this.IsTargeted = value;
    }

    // Add an alert to the active alerts list
    public void AddAlert(DeviceAlertView alert)
    {
        var alerts = ActiveAlerts.ToList();
        alerts.Add(alert);
        ActiveAlerts = alerts;
    }
    
    // Clear all active alerts
    public void ClearAlerts()
    {
        ActiveAlerts = new List<DeviceAlertView>();
    }

    public void AddRemoteAccessAnalysisResult(string deviceUid, RemoteAccessAnalysisResultVm vm)
    {
        this.RemoteAccessAnalysisResults[deviceUid].Add(vm);
        if (this.RemoteAccessAnalysisResults[deviceUid].Count > this.maxRemoteAccessAnalysisResults)
        {
            this.RemoteAccessAnalysisResults[deviceUid].Remove(this.RemoteAccessAnalysisResults[deviceUid].Last());
        }
    }



}
