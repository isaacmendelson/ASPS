// Mirrors Common.Models.Key in the .NET backend.
// Serialized as { "type": "User", "value": "abc123" }.

export interface Key {
  type: string;
  value: string;
  instanceName?: string;
}
