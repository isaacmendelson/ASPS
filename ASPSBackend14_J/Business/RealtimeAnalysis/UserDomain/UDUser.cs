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
        IEnumerable<DeviceAlertView>? activeAlerts, Dictionary<string, IEnumerable<BrowserTab>>? browserTabs, bool? isTaregted, 
        bool? isCrossPlatformLocked = false, UserRiskProfile? riskProfile = null)
    {
        RiskAssessment = riskAssessment;
        Key = key;
        UserInfo = userInfo;
        ActiveAlerts = activeAlerts ?? new List<DeviceAlertView>();
        UserDevices = userDevices ?? new List<UserDeviceView>();
        BrowserTabs = browserTabs ?? new();
        this.IsTargeted = isTaregted ?? false;
        this.IsCrossPlatformLocked = isCrossPlatformLocked ?? false;
        //this.Devices = devices ?? new List<UserDevice>();
        this._userDevices = userDevices ?? new List<UserDeviceView>();  

        this.RiskProfile = riskProfile ?? new UserRiskProfile();
        RemoteAccessAnalysisResults = new Dictionary<string, List<RemoteAccessAnalysisResult>>();
        UserUrlSurfDataByDevice = new Dictionary<string, IEnumerable<UserDeviceUrlSurfData>>();
    }

    // Core identity
    public Key Key { get; private set; }

    public RiskAssessment RiskAssessment { get; private set; }
    
    
    // User properties from User entity (excluding IsDeleted and KeyField)
   public UserInfo UserInfo { get; private set; }

    // Runtime analysis parameters
    public bool IsTargeted { get; private set; }    //True if user contact information is found in darknet lead lists.
    public bool IsCrossPlatformLocked { get; private set; }  //True if all user devices are locked (cross-platform protection)
    public UserRiskProfile RiskProfile { get; private set; }  //User risk profile with vulnerability and exposure scores
    public Dictionary<string, IEnumerable<BrowserTab>>? BrowserTabs { get; private set; }
    public IEnumerable<DeviceAlertView> ActiveAlerts { get; set; }
    //public List<UserDevice> Devices { get; private set; }  //Direct access to user device entities

    public Dictionary<string, List<RemoteAccessAnalysisResult>> RemoteAccessAnalysisResults { get; private set; }

    public Dictionary<string, RemoteAccessAnalysisResult> RemoteAccessStatus 
    { 
        get
        {
            var res = new Dictionary<string, RemoteAccessAnalysisResult>();
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

    /// <summary>
    /// Set cross-platform lock status - locks all user devices when true
    /// </summary>
    public void SetCrossPlatformLock(bool value)
    {
        this.IsCrossPlatformLocked = value;
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

    public void SetBrowserTabs(string deviceUid, IEnumerable<BrowserTab> tabs)
    {
        if (!this.BrowserTabs.ContainsKey(deviceUid))
        {
            this.BrowserTabs[deviceUid] = new List<BrowserTab>();
        }
        this.BrowserTabs[deviceUid] = tabs.ToList();
    }
    public void AddRemoteAccessAnalysisResult(string deviceUid, RemoteAccessAnalysisResult vm)
    {
        if (!this.RemoteAccessAnalysisResults.ContainsKey(deviceUid))
        {
            this.RemoteAccessAnalysisResults[deviceUid] = new List<RemoteAccessAnalysisResult>();
        }
        
        this.RemoteAccessAnalysisResults[deviceUid].Add(vm);
        if (this.RemoteAccessAnalysisResults[deviceUid].Count > this.maxRemoteAccessAnalysisResults)
        {
            this.RemoteAccessAnalysisResults[deviceUid].Remove(this.RemoteAccessAnalysisResults[deviceUid].Last());
        }
    }



}
