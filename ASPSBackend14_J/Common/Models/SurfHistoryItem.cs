using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class SurfHistoryItem
    {
        protected SurfHistoryItem() { }
        public SurfHistoryItem(
            string url, 
            DateTime timestamp, 
            DateTime? leaveTime = null
            )
        {
            Url = url;
            Timestamp = timestamp;
            LeaveTime = leaveTime;
        }

        public string Url { get; set; }
        public DateTime Timestamp { get; set; } 
        public DateTime? LeaveTime { get; set; }
    }
}
