using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Business.DomainEvents
{
    [DataContract]
    public class ImmediateDangerEnded : DomainEvent
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public ImmediateDangerEnded() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        public ImmediateDangerEnded(Key immediateDangerKey, Key userKey)
        {
            ImmediateDangerKey = immediateDangerKey;
            UserKey = userKey;
        }

        [DataMember]
        public Key ImmediateDangerKey { get; set; }
        
        [DataMember] 
        public Key UserKey { get; set; }
    }
}
