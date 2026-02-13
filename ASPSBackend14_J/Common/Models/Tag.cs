using Common.Interfaces;

namespace Common.Models;

public class Tag : ITag, IEquatable<Tag>
{
    public Key Key { get; set; } = new Key();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? BaseType { get; set; }

    public Tag()
    {
    }

    public Tag(Key key, string name, string type, string? baseType = null)
    {
        Key = key;
        Name = name;
        Type = type;
        BaseType = baseType;
    }

    public bool Equals(Tag? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Key.Equals(other.Key) && Name == other.Name && Type == other.Type && BaseType == other.BaseType;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Tag);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Key, Name, Type, BaseType);
    }

    public static bool operator ==(Tag? left, Tag? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(Tag? left, Tag? right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        return $"{Name} ({Type})";
    }
}
