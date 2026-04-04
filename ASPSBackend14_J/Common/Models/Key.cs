using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Common.Models;

[DataContract]
public sealed class Key : IEquatable<Key>, IXmlSerializable
//public class Key : IEquatable<Key>, IXmlSerializable
{
    private static readonly char[] _splitChars = { '#' };

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

    //public override string ToString()
    //{
    //    return InstanceName != null ? $"{Type}:{Value}:{InstanceName}" : $"{Type}:{Value}";
    //}

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


    public override string ToString()
    {
        if (this.InstanceName == null)
        {
            return $"{this.Type}#{this.Value}";
        }
        return $"{this.Type}#{this.Value}#{this.InstanceName}";
    }


    public string ToString(char separator)
    {
        if (this.InstanceName == null)
        {
            return $"{this.Type}{separator}{this.Value}";
        }
        return $"{this.Type}{separator}{this.Value}{separator}{this.InstanceName}";

    }
    public static Key Parse(string text)
    {
        var splits = text.Trim().Split(_splitChars, StringSplitOptions.RemoveEmptyEntries);
        return ParseParts(splits);
    }


    public static Key Parse(string text, char separator)
    {
        var splits = text.Trim().Split(separator, StringSplitOptions.RemoveEmptyEntries);
        return ParseParts(splits);
    }


    public static bool TryParse(string? text, [NotNullWhen(true)] out Key? key)
    {
        if (text != null)
        {
            var splits = text.Trim().Split(_splitChars, StringSplitOptions.RemoveEmptyEntries);
            if (splits.Length == 2)
            {
                key = new Key(splits[0], splits[1]);
                return true;
            }
            if (splits.Length == 3)
            {
                key = new Key(splits[0], splits[1], splits[2]);
                return true;
            }
        }

        key = null;
        return false;
    }

    public static bool TryParse(string text, char separator, [NotNullWhen(true)] out Key? key)
    {
        if (text != null)
        {
            var splits = text.Trim().Split(separator, StringSplitOptions.RemoveEmptyEntries);
            if (splits.Length == 2)
            {
                key = new Key(splits[0], splits[1]);
                return true;
            }
            if (splits.Length == 3)
            {
                key = new Key(splits[0], splits[1], splits[2]);
                return true;
            }
        }

        key = null;
        return false;
    }

    private static Key ParseParts(string[] splits)
    {
        if (splits.Length == 2)
        {
            return new Key(splits[0], splits[1]);
        }
        if (splits.Length == 3)
        {
            return new Key(splits[0], splits[1], splits[2]);
        }

        throw new FormatException("The specified string does not contains a valid key.");
    }
}
