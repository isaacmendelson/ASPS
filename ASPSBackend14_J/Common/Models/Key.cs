using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Common.Models;

public class Key : IEquatable<Key>, IXmlSerializable
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? InstanceName { get; set; }

    public Key()
    {
    }

    public Key(string type, string value, string? instanceName = null)
    {
        Type = type;
        Value = value;
        InstanceName = instanceName;
    }

    public bool Equals(Key? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Type == other.Type && Value == other.Value && InstanceName == other.InstanceName;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Key);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Value, InstanceName);
    }

    public override string ToString()
    {
        return InstanceName != null ? $"{Type}:{Value}:{InstanceName}" : $"{Type}:{Value}";
    }

    public static bool operator ==(Key? left, Key? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(Key? left, Key? right)
    {
        return !(left == right);
    }

    // IXmlSerializable implementation
    public XmlSchema? GetSchema() => null;

    public void ReadXml(XmlReader reader)
    {
        reader.MoveToContent();
        Type = reader.GetAttribute(nameof(Type)) ?? string.Empty;
        Value = reader.GetAttribute(nameof(Value)) ?? string.Empty;
        InstanceName = reader.GetAttribute(nameof(InstanceName));
        reader.Read();
    }

    public void WriteXml(XmlWriter writer)
    {
        writer.WriteAttributeString(nameof(Type), Type);
        writer.WriteAttributeString(nameof(Value), Value);
        if (InstanceName != null)
        {
            writer.WriteAttributeString(nameof(InstanceName), InstanceName);
        }
    }
}
