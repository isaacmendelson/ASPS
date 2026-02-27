using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class RiskAssessment
    {
        public RiskAssessment(float risk_score, string risk_level, bool is_scam, float confidence)
        {
            this.risk_score = risk_score;
            this.risk_level = risk_level;
            this.is_scam = is_scam;
            this.confidence = confidence;
        }

        public float risk_score { get; set; }
        public string risk_level { get; set; } = string.Empty;
        public bool is_scam { get; set; }
        public float confidence { get; set; }
    }
}
