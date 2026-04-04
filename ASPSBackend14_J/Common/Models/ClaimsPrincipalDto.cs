#nullable enable

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    [DataContract]
    public class ClaimsPrincipalDto 
    {
        public ClaimsPrincipalDto()
        {
            Claims = Array.Empty<ClaimDto>();
        }

        public ClaimsPrincipalDto(ClaimsPrincipal principal)
        {
            Claims = principal.Claims.Select(c => new ClaimDto(c.Type, c.Value, c.ValueType)).ToArray();
        }

        [JsonProperty(Required = Required.Always)]
        [DataMember(Order = 1)]
        public ClaimDto[] Claims { get; private set; }

        public ClaimDto? FindFirst(string type)
        {
            return Claims.FirstOrDefault(i => i.Type == type);
        }

        public IEnumerable<ClaimDto> FindAll(string type)
        {
            return Claims.Where(i => i.Type == type);
        }

        public void AddClaim(ClaimDto claim)
        {
            var claims = Claims.ToList();
            claims.Add(claim);
            Claims = claims.ToArray();
        }

    }

    [DataContract]
    public class ClaimDto
    {
        private ClaimDto()
        {
        }

        public ClaimDto(string type,  string? value, string valueType)
        {
            Type = type;
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            Value = value;
        }

        public ClaimDto(string type, string value, string valueType, string scope, string client, string path)
        {
            Type = type;
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            Value = value;
        }

        [DataMember(Order = 1)]
        public string Type { get; private set; }

        [DataMember(Order = 2)]
        public string ValueType { get; private set; }

        [DataMember(Order = 3)]
        public string? Value { get; private set; }

        public override string ToString()
        {
            return $"{Type}: {ValueType}: {Value}";
        }
    }
}
