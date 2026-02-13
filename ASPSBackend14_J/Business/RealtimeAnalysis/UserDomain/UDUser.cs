using Common.Enums;
using Common.Models;
using Common.Models.Alerts;

namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Runtime representation of a User with active alerts and analysis state.
/// Each user has their own UDUser instance running in the background.
/// </summary>
public class UDUser
{
    public UDUser(Key key)
    {
        Key = key;
        DateCreated = DateTime.UtcNow;
        this.RiskAssessment = new RiskAssessmentVm(0, "", false, 1);
    }

    // Core identity
    
    public RiskAssessmentVm RiskAssessment { get; private set; }
    public Key Key { get; private set; }
    
    // User properties from User entity (excluding IsDeleted and KeyField)
    public string KeycloakUserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Zip { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public int? GuardianKey { get; set; }
    public string? Locale { get; set; }
    public int? Timezone { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
    public DateTime? DateDeleted { get; set; }
    public bool IsDisabled { get; set; }
    
    // Runtime analysis state
    public IEnumerable<DeviceAlert> ActiveAlerts { get; private set; } = new List<DeviceAlert>();
    
    // Constructor

    
    // Add an alert to the active alerts list
    public void AddAlert(DeviceAlert alert)
    {
        var alerts = ActiveAlerts.ToList();
        alerts.Add(alert);
        ActiveAlerts = alerts;
    }
    
    // Clear all active alerts
    public void ClearAlerts()
    {
        ActiveAlerts = new List<DeviceAlert>();
    }
    
    // Get full name
    public string FullName => $"{FirstName} {LastName}".Trim();
}
