using System.Diagnostics.CodeAnalysis;
using Common.Entities;

namespace Common.Models;

public static class TypeNameMapper
{
    private static readonly Dictionary<string, Type> _nameToType = new Dictionary<string, Type>();
    private static readonly Dictionary<Type, string> typeToName = new Dictionary<Type, string>();
    
    static TypeNameMapper()
    {
        var types = typeof(TypeNameMapper).Assembly.GetTypes()
            .Where(t => t.IsClass)
            .Where(t => t.IsSubclassOf(typeof(Entity)) 
                     || t.IsSubclassOf(typeof(UserDevice)) 
                     || t.IsSubclassOf(typeof(UserAccount))
                     || t.IsSubclassOf(typeof(DeviceAlert)) 
                     || t.IsSubclassOf(typeof(AnalysisResultContainer)));
        
        foreach (var type in types)
        {
            _nameToType.Add(type.Name, type);
            typeToName.Add(type, type.Name);
        }
    }
    
    public static bool IsOfType<T>(string typeName)
    {
        return _nameToType.TryGetValue(typeName, out Type? t) && 
               typeof(T).IsAssignableFrom(t);
    }
    
    public static bool IsOfType<T>(Key key)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }
        return _nameToType.TryGetValue(key.Type, out Type? t) &&
               typeof(T).IsAssignableFrom(t);
    }
    
    public static bool IsOfType<T>(Tag tag)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }
        return _nameToType.TryGetValue(tag.Type, out Type? t) &&
               typeof(T).IsAssignableFrom(t);
    }
    
    public static Type GetItemType(string name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        return _nameToType[name];
    }
    
    public static string GetItemName(Type type)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }
        return typeToName[type];
    }
    
    public static bool TryGetItemType(string name, [NotNullWhen(true)] out Type? type)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        return _nameToType.TryGetValue(name, out type);
    }
    
    public static bool TryGetItemName(Type type, [NotNullWhen(true)] out string? name)
    {
        return typeToName.TryGetValue(type, out name);
    }
}
