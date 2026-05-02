using Common.Enums;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Entities
{
    public class ImmediateDangerByRemoteAccess : ImmediateDanger
    {
        public ImmediateDangerByRemoteAccess() : base() { }

        public ImmediateDangerByRemoteAccess(RemoteAccessApp? remoteAccessApp, string sensitiveUrl, DateTime timestamp, string deviceUid, string userKeyField, string? deviceKeyField, string? deviceAlertKeyField,
            ProtectiveAction[] protectiveActions, UserDevice? device, DeviceAlert? deviceAlert, DateTime? endTime, User user)
            :base(timestamp, deviceUid, userKeyField, deviceKeyField, deviceAlertKeyField, device, deviceAlert, endTime, user, null, protectiveActions)
        {
            this.RemoteAccessApp = remoteAccessApp;
            this.SensitiveUrl = sensitiveUrl;
        }
        public override string TypeName
        {
            get
            {
                return this.GetType().Name;
            }
        }

        public override Tag Tag
        {
            get
            {
                return new Tag(this.Key, $"{this.Typename}-{this.RemoteAccessApp.ToString()}-{this.SensitiveUrl}", this.Typename);
            }
        }

        public RemoteAccessApp? RemoteAccessApp { get; set; }

        public string? SensitiveUrl { get; set; }

    }
}
