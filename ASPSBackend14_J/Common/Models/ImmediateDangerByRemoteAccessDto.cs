using Common.Entities;
using Common.Enums;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    // [DataContract] is inherited from ImmediateDangerDto, which puts Newtonsoft
    // into opt-in mode. Without [DataMember] on the derived properties below,
    // SensitiveUrl and RemoteAccessApp are silently dropped from the JSON
    // payload of ImmediateDangerNotification — and the desktop agent's
    // "Sensitive URL" details row renders as "—".
    [DataContract]
    public class ImmediateDangerByRemoteAccessDto : ImmediateDangerDto
    {
        public ImmediateDangerByRemoteAccessDto(Key key, RemoteAccessApp? remoteAccessApp, string? sensitiveUrl, string deviceUid, string userKey, Key? deviceKey, Key? deviceAlertKey,
            Key? scamInProgressKey, ProtectiveAction[] protectiveActions
            ) : base(key, deviceUid, userKey, deviceKey, deviceAlertKey)
        {
            this.RemoteAccessApp = remoteAccessApp;
                this.SensitiveUrl = sensitiveUrl;
        }

        public ImmediateDangerByRemoteAccessDto(ImmediateDangerByRemoteAccess entity) : base(entity)
        {
            this.RemoteAccessApp = entity.RemoteAccessApp;
            this.SensitiveUrl = entity.SensitiveUrl;
        }

        [DataMember]
        public RemoteAccessApp? RemoteAccessApp { get; set; }

        [DataMember]
        public string? SensitiveUrl { get; set; }

    }
}
