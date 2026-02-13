using Common.Entities;
using Common.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Views
{
    public class UrlAlertView: DeviceAlertView
    {

        public UrlAlertView(UrlAlertEntity entity)
            : base(entity)
        {
            this.Url = entity.Url;
            this.TrackerKeys = JsonConvert.DeserializeObject<Key[]>(entity.TrackerKeys);
            this.IFrameDomains = entity.IFrameDomains.Split(",");
            this.UserAgent = entity.UserAgent;
        }

        
        public string Url { get; private set; }
        public Key[] TrackerKeys { get; private set; }
        public string[] IFrameDomains { get; private set; }
        public string UserAgent { get; private set; }
    }
}
