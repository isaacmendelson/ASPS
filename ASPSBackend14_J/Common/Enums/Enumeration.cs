#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Common;

public abstract class Enumeration : IEquatable<Enumeration>
{
    private static readonly Dictionary<int, Enumeration> _enumerations = new();
    private static readonly object sync = new();

    private static int nextKey;

    private readonly int key;
    private readonly int value;
    private readonly string name;


#nullable disable
    protected Enumeration()
    {

    }
#nullable enable

    public Enumeration(int value, string name)
    {
        lock (sync)
        {
            this.key = nextKey++;
            _enumerations.Add(this.key, this);
        }
        this.value = value;
        this.name = name;
    }

    internal virtual string EnumerationName
    {
        get { return this.GetType().Name; }
    }


    public int Value
    {
        get { return this.value; }
    }

    public string Name
    {
        get { return this.name; }
    }

    public int Key
    {
        get { return this.key; }
    }

    public static T[] GetValues<T>() where T : Enumeration
    {
        lock (sync)
        {
            return _enumerations.Values.OfType<T>().ToArray();
        }
    }

    public static Enumeration[] GetValues(Type type)
    {
        lock (sync)
        {
            return _enumerations.Values.Where(t => t.GetType() == type).ToArray();
        }
    }

    public static Enumeration? FindByName(string enumerationTypeName, string name)
    {
        lock (sync)
        {
            return _enumerations.Values.FirstOrDefault(e => (e.EnumerationName == enumerationTypeName) && (e.Name == name));
        }
    }

    public static Enumeration? FindByValue(string enumerationTypeName, int value)
    {
        lock (sync)
        {
            return _enumerations.Values.FirstOrDefault(e => (e.EnumerationName == enumerationTypeName) && (e.Value == value));
        }
    }

    public static T? FindByValue<T>(int value) where T : Enumeration
    {
        lock (sync)
        {
            return _enumerations.Values.FirstOrDefault(e => (e is T) && (e.Value == value)) as T;
        }
    }

    public static T? FindByName<T>(string name) where T : Enumeration
    {
        lock (sync)
        {
            return _enumerations.Values.FirstOrDefault(e => (e is T) && (e.Name == name)) as T;
        }
    }


    public static T FromValue<T>(int value) where T : Enumeration
    {
        lock (sync)
        {
            T? item = _enumerations.Values.FirstOrDefault(e => (e is T) && (e.Value == value)) as T;
            if (item == null)
            {
                throw new ArgumentException($"Invalid value for {typeof(T).Name}.", nameof(value));
            }
            return item;
        }
    }

    protected static T Parse<T>(string name) where T : Enumeration
    {
        lock (sync)
        {
            var item = _enumerations.Values.FirstOrDefault(e => (e is T) && (e.Name == name)) as T;
            if (item == null)
            {
                throw new ArgumentException($"Invalid name for {typeof(T).Name}.", nameof(name));
            }
            return item;
        }
    }


    public override bool Equals(object? obj)
    {
        lock (sync)
        {
            var other = obj as Enumeration;
            return this.Equals(other);
        }
    }

    public bool Equals(Enumeration? other)
    {
        return (other is not null) && (this.key == other.key);
    }

    public static bool operator ==(Enumeration? lhs, Enumeration? rhs)
    {
        if (lhs is null && rhs is null)
        {
            return true;
        }
        return (lhs is not null && (lhs.Equals(rhs)));
    }

    public static bool operator !=(Enumeration? lhs, Enumeration? rhs)
    {
        if (lhs is null && rhs is null)
        {
            return false;
        }
        return (lhs is null || (!lhs.Equals(rhs)));
    }

    public override int GetHashCode()
    {
        return this.key;
    }

    public override string ToString()
    {
        return this.name;
    }

    public virtual string GetLocalizationKey()
    {
        return string.Format("str_{0}_{1}", this.GetType().Name, this.name);
    }

    private class EnumerationItem
    {
        private readonly List<Enumeration> values = [];

        public EnumerationItem(string enumName)
        {
            this.EnumName = enumName;
        }

        public string EnumName { get; private set; }
        public IReadOnlyCollection<Enumeration> Values
        {
            get { return this.values.AsReadOnly(); }
        }
    }
}
