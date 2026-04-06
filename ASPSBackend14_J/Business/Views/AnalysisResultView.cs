using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Entities;
using Common.Interfaces;
using Common.Models;
using NetTopologySuite.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace Business.Views
{
    public class AnalysisResultView : ASItemView, IAnalysisResultView
    {
        protected AnalysisResultView() { }
        //public AnalysisResultView(AnalysisResult analysisResult)   //, IIndicator[] indicators, IProtectiveAction[] protectiveActions, string analyzerName)
        //     : base(entity.Key, "a r ")
        //{
        //    this.UserKey = new Key(nameof(User), entity.UserKeyField);
        //    this.Discriminator = entity.Discriminator;
        //    //this.Indicators = indicators;
        //    //this.ProtectiveActions = protectiveActions;
        //    //this.AnalyzerName = analyzerName;
        //    this.HasError = entity.HasError;
        //    this.ErrorMessage = entity.ErrorMessage;
        //    this.Timestamp = entity.Timestamp;
        //    this.IsFromCache = entity.IsFromCache;
        //    this.DeviceAlertKey = new Key(Discriminator, entity.DeviceAlertKeyField);
        //}

        public AnalysisResultView(AnalysisResultContainer entity)   //, IIndicator[] indicators, IProtectiveAction[] protectiveActions, string analyzerName)
            : base(entity.Key, "a r ")
        {
            this.UserKey = new Key(nameof(User), entity.UserKeyField);
            this.Discriminator = entity.Discriminator;
            //this.Indicators = indicators;
            //this.ProtectiveActions = protectiveActions;
            //this.AnalyzerName = analyzerName;
            this.HasError = entity.HasError;
            this.ErrorMessage = entity.ErrorMessage;
            this.Timestamp = entity.Timestamp;
            this.IsFromCache = entity.IsFromCache;
            this.DeviceAlertKey = new Key(Discriminator, entity.DeviceAlertKeyField);
        }

        public Key UserKey { get; set; }
        public string Discriminator { get; set; } = string.Empty;
        public DeviceAlert? Alert { get; set; }
        public bool? HasError { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }

        public bool IsFromCache { get; set; }
        public Key? DeviceAlertKey { get; set; }
        public IAnalysisResult AnalysisResult { get; set; }

        
        public IIndicator[]? Indicators { get; set; }
        
        public IProtectiveAction[]? ProtectiveActions { get; set; }
        
        public string AnalyzerName { get; set; }

    }
  
}
