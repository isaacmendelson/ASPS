using Business.DomainEvents;

namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Handles detection and alerting when user details are found in scam/marketing lead lists
/// ASPS-370: UserIsTargetedAlert
/// Alert fires only once per user (when IsTargeted transitions from false to true)
/// </summary>
public class UserIsTargetedAlertHandler
{
    /// <summary>
    /// Check if user should receive targeted alert and create event if needed
    /// </summary>
    /// <param name="user">UDUser instance</param>
    /// <param name="userEmail">User email found in lists</param>
    /// <param name="userPhoneNumber">User phone number found in lists</param>
    /// <param name="foundInLists">List names where user details were found</param>
    /// <param name="source">Source of the discovery</param>
    /// <param name="confidenceScore">Confidence score (0.0-1.0)</param>
    /// <returns>UserIsTargetedAlertReceived event if should alert, null if already targeted</returns>
    public UserIsTargetedAlertReceived? CheckAndCreateAlert(
        UDUser user,
        string userEmail,
        string userPhoneNumber,
        List<string> foundInLists,
        string source,
        double confidenceScore)
    {
        // Only fire alert if user is not already marked as targeted
        if (user.IsTargeted)
            return null;

        // Create the alert event
        var alertEvent = new UserIsTargetedAlertReceived(
            userKeyField: user.Key.Value,
            userEmail: userEmail,
            userPhoneNumber: userPhoneNumber,
            foundInLists: foundInLists,
            source: source,
            confidenceScore: Math.Clamp(confidenceScore, 0, 1)
        );

        // Mark user as targeted
        user.SetUserIsTargeted(true);

        return alertEvent;
    }

    /// <summary>
    /// Check if user details match any known scam lead lists
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="phoneNumber">User phone number</param>
    /// <param name="knownLeadLists">Dictionary of list name -> list of emails/phones</param>
    /// <returns>List of matching list names</returns>
    public List<string> FindUserInLeadLists(
        string email,
        string phoneNumber,
        Dictionary<string, HashSet<string>> knownLeadLists)
    {
        var matchingLists = new List<string>();

        foreach (var (listName, contacts) in knownLeadLists)
        {
            if (!string.IsNullOrEmpty(email) && contacts.Contains(email.ToLowerInvariant()))
            {
                matchingLists.Add(listName);
                continue;
            }

            if (!string.IsNullOrEmpty(phoneNumber) && contacts.Contains(NormalizePhoneNumber(phoneNumber)))
            {
                matchingLists.Add(listName);
            }
        }

        return matchingLists;
    }

    /// <summary>
    /// Calculate confidence score based on number of lists and data quality
    /// </summary>
    /// <param name="foundInListsCount">Number of lists where user was found</param>
    /// <param name="hasEmail">Whether email was matched</param>
    /// <param name="hasPhone">Whether phone was matched</param>
    /// <returns>Confidence score (0.0-1.0)</returns>
    public double CalculateConfidenceScore(
        int foundInListsCount,
        bool hasEmail,
        bool hasPhone)
    {
        double confidence = 0.0;

        // Base confidence from list count
        if (foundInListsCount >= 3)
            confidence += 0.5;
        else if (foundInListsCount == 2)
            confidence += 0.3;
        else if (foundInListsCount == 1)
            confidence += 0.2;

        // Email match adds confidence
        if (hasEmail)
            confidence += 0.25;

        // Phone match adds confidence
        if (hasPhone)
            confidence += 0.25;

        return Math.Clamp(confidence, 0, 1);
    }

    /// <summary>
    /// Normalize phone number for comparison (remove spaces, dashes, country code variations)
    /// </summary>
    private string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber))
            return string.Empty;

        // Remove common separators
        var normalized = phoneNumber.Replace(" ", "")
                                    .Replace("-", "")
                                    .Replace("(", "")
                                    .Replace(")", "")
                                    .Replace("+", "");

        return normalized;
    }
}
