#nullable enable

using Common.Enums;
using Common.Interfaces;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class ProtectiveAction : IProtectiveAction
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        protected ProtectiveAction() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        public ProtectiveAction(Key subjectKey, ProtectiveActionType actionType, AnalysisLevel level, string message, string? alertId)
        {
            this.SubjectKey = subjectKey;
            this.ActionType = actionType;
            this.Level = level;
            this.AlertId = alertId;
            this.Message = message;
            this.Timestamp = DateTime.UtcNow;
        }
        //public ProtectiveActionSubject Subject { get; set; }
        public Key SubjectKey { get; set; }
        public ProtectiveActionType ActionType { get; set; }
        public string? AlertId { get; set; }
        public string? Message { get; set; }
        public AnalysisLevel Level { get; protected set; }
        public DateTime Timestamp { get; protected set; }
        public DateTime? ExecutionTime { get; protected set; }

        public ProtectiveActionStatus Status { get; set; } = ProtectiveActionStatus.None;


    }
}
