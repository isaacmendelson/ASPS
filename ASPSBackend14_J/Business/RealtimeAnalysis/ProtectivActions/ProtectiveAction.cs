#nullable enable

using Common.Enums;
using Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis
{
    public class ProtectiveAction : IProtectiveAction
    {
        protected ProtectiveAction() { }
        public ProtectiveAction(ProtectiveActionSubject subject, ProtectiveActionType actionType, AnalysisLevel level, string message, string? alertId)
        {
            this.Subject = subject;
            this.ActionType = actionType;
            this.Level = level;
            this.AlertId = alertId;
            this.Message = message;
        }
        public ProtectiveActionSubject Subject { get; set; }
        public ProtectiveActionType ActionType { get; set; }
        public string? AlertId { get; set; }
        public string? Message { get; set; }
        public AnalysisLevel Level { get; protected set; }

    }
}
