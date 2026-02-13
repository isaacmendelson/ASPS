using Common.Entities;
using Common.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Views
{
    public class RemoteAccessAlertView : DeviceAlertView
    {
        public RemoteAccessAlertView(RemoteAccessAlertEntity entity)
          : base(entity)
        {
            this.RemoteAccessApp = entity.RemoteAccessApp;
            this.RunningProcesses = entity.RunningProcesses;
            this.ConnectionUrl = entity.ConnectionUrl;
            this.ConnectionStatus = entity.ConnectionStatus;
            this.ConnectionsCount = entity.ConnectionsCount;
            this.SessionStatus = entity.SessionStatus;
            this.ConnectionStatus = entity.ConnectionStatus;

        }

        public RemoteAccessApp RemoteAccessApp { get; set; }
        public int RunningProcesses { get; set; }
        public string ConnectionUrl { get; set; } = string.Empty;
        public ConnectionStatus ConnectionStatus { get; set; }
        public int ConnectionsCount { get; set; }
        public int SessionStatus { get; set; }

        [NotMapped]
        public string TypeName => "RemoteAccessAlert";
    }
}
