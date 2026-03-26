using Business.DomainEvents;

namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Detects OTP interception attempts by correlating SMS OTP reception with browser input under Remote Access
/// ASPS-368: OTP Interception
/// </summary>
public class OtpInterceptionDetector
{
    private readonly int _maxCorrelationWindowSeconds;
    private readonly double _minConfidenceThreshold;

    /// <summary>
    /// Creates a new OTP Interception Detector
    /// </summary>
    /// <param name="maxCorrelationWindowSeconds">Maximum time window (in seconds) between SMS and browser input to consider correlation</param>
    /// <param name="minConfidenceThreshold">Minimum confidence threshold (0.0-1.0) to trigger alert</param>
    public OtpInterceptionDetector(int maxCorrelationWindowSeconds = 60, double minConfidenceThreshold = 0.7)
    {
        _maxCorrelationWindowSeconds = maxCorrelationWindowSeconds;
        _minConfidenceThreshold = Clamp(minConfidenceThreshold, 0, 1);
    }

    /// <summary>
    /// Detect OTP interception attempt
    /// </summary>
    /// <param name="userKeyField">User key</param>
    /// <param name="mobileDeviceUid">Mobile device UID that received the SMS</param>
    /// <param name="browserDeviceUid">Browser device UID where input was attempted</param>
    /// <param name="otpCode">The OTP code from SMS</param>
    /// <param name="smsReceivedTimestamp">When the SMS was received</param>
    /// <param name="browserInputTimestamp">When browser input was attempted</param>
    /// <param name="remoteAccessApp">Remote access application name (TeamViewer, AnyDesk, etc.)</param>
    /// <param name="targetUrl">Target URL where OTP is being entered</param>
    /// <param name="isRemoteAccessActive">Whether remote access is currently active</param>
    /// <returns>OtpInterceptionTriggered event if correlation detected, null otherwise</returns>
    public OtpInterceptionTriggered? DetectInterception(
        string userKeyField,
        string mobileDeviceUid,
        string browserDeviceUid,
        string otpCode,
        DateTime smsReceivedTimestamp,
        DateTime browserInputTimestamp,
        string remoteAccessApp,
        string targetUrl,
        bool isRemoteAccessActive)
    {
        // Calculate time difference
        var timeDifference = Math.Abs((browserInputTimestamp - smsReceivedTimestamp).TotalSeconds);

        // Not within correlation window
        if (timeDifference > _maxCorrelationWindowSeconds)
            return null;

        // Calculate correlation confidence
        double confidence = CalculateCorrelationConfidence(
            timeDifference,
            isRemoteAccessActive,
            !string.IsNullOrEmpty(otpCode)
        );

        // Below threshold
        if (confidence < _minConfidenceThreshold)
            return null;

        // Determine if should block
        bool shouldBlock = confidence >= 0.85 && isRemoteAccessActive;

        return new OtpInterceptionTriggered(
            userKeyField: userKeyField,
            mobileDeviceUid: mobileDeviceUid,
            browserDeviceUid: browserDeviceUid,
            otpCode: otpCode,
            smsReceivedTimestamp: smsReceivedTimestamp,
            browserInputTimestamp: browserInputTimestamp,
            remoteAccessApp: remoteAccessApp,
            targetUrl: targetUrl,
            isBlocked: shouldBlock,
            correlationConfidence: confidence
        );
    }

    /// <summary>
    /// Calculate correlation confidence based on multiple factors
    /// </summary>
    /// <param name="timeDifferenceSeconds">Time difference between SMS and browser input</param>
    /// <param name="isRemoteAccessActive">Whether remote access is active</param>
    /// <param name="hasOtpCode">Whether we have the actual OTP code</param>
    /// <returns>Confidence score (0.0-1.0)</returns>
    private double CalculateCorrelationConfidence(
        double timeDifferenceSeconds,
        bool isRemoteAccessActive,
        bool hasOtpCode)
    {
        double confidence = 0.0;

        // Time proximity factor (closer = higher confidence)
        // 0-10 seconds: 0.5, 10-30: 0.3, 30-60: 0.2
        if (timeDifferenceSeconds <= 10)
            confidence += 0.5;
        else if (timeDifferenceSeconds <= 30)
            confidence += 0.3;
        else
            confidence += 0.2;

        // Remote access active = +0.3
        if (isRemoteAccessActive)
            confidence += 0.3;

        // Have actual OTP code = +0.2
        if (hasOtpCode)
            confidence += 0.2;

        return Clamp(confidence, 0, 1);
    }

    /// <summary>
    /// Helper to clamp values
    /// </summary>
    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
