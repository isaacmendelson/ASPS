using System;
using System.Runtime.Serialization;

namespace Business.Messaging
{
    /// <summary>
    /// Backend-controlled override of the desktop agent's BrowserTabs inclusion
    /// policy. Sent on the device topic; the agent applies the override until
    /// <see cref="ValidUntil"/> elapses, then reverts to its default
    /// ('incoming_only').
    /// </summary>
    [DataContract]
    public class SetBrowserTabsPolicyNotification : DeviceNotification
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        protected SetBrowserTabsPolicyNotification()
#pragma warning restore CS8618
        {
        }

        public SetBrowserTabsPolicyNotification(string deviceUid, string userKey, string mode, DateTime? validUntil)
        {
            DeviceUid = deviceUid;
            UserKey = userKey;
            Mode = mode;
            ValidUntil = validUntil;
            Timestamp = DateTime.UtcNow;
        }

        [DataMember]
        public string DeviceUid { get; set; }

        [DataMember]
        public string UserKey { get; set; }

        /// <summary>One of: "incoming_only" | "always" | "never".</summary>
        [DataMember]
        public string Mode { get; set; }

        /// <summary>UTC. Null = no expiry (override stays until next override or agent restart).</summary>
        [DataMember]
        public DateTime? ValidUntil { get; set; }
    }
}
