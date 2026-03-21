using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    /// <summary>
    /// Represents a risk assessment for security analysis.
    /// Risk score interpretation (NEW scale as of ASPS-318):
    /// - 0 = Error or no result
    /// - 1-30 = LOW risk (safe)
    /// - 31-60 = MEDIUM risk
    /// - 61-100 = HIGH risk (dangerous)
    /// </summary>
    public class RiskAssessment
    {
        public RiskAssessment(float risk_score, string risk_level, bool is_scam, float confidence)
        {
            this.risk_score = risk_score;
            this.risk_level = risk_level;
            this.is_scam = is_scam;
            this.confidence = confidence;
        }

        /// <summary>
        /// Risk score on a scale of 0-100.
        /// 0 = error/no result, 1 = safest, 100 = most dangerous
        /// </summary>
        public float risk_score { get; set; }
        
        /// <summary>
        /// Risk level description (e.g., "Safe", "Monitor", "High Risk")
        /// </summary>
        public string risk_level { get; set; } = string.Empty;
        
        /// <summary>
        /// Indicates if this is identified as a scam
        /// </summary>
        public bool is_scam { get; set; }
        
        /// <summary>
        /// Confidence level in this assessment (0.0 - 1.0)
        /// </summary>
        public float confidence { get; set; }
    }
}
