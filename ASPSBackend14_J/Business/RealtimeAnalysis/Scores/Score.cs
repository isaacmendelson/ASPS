# nullable enable

using System.ComponentModel.DataAnnotations;

namespace Business.RealtimeAnalysis
{
    public abstract class Score : IScore
    {
        public Score() { }

        public Score(float value, float confidence) { 
            this.Value = value;
            this.Confidence = confidence;
        }

        
        public virtual object? Value { get; set; }

        [Range(0, 1)]
        public float Confidence { get; set; }

        public bool IsValid { get; private set; } = true;

        public virtual ValueType ValueType { get; set; }

        public void SetInvaid()
        {
            this.IsValid = false;
        }
    }
}
