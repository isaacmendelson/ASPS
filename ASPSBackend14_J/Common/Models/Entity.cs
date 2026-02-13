using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Common.Exceptions;
using Common.Interfaces;

namespace Common.Models;

public abstract class Entity : IHasTag
{
    private Key? key;
    // Don't rename to "keyField" so that EF will not recognize it as the backing field
    private string myKeyField = string.Empty;
    
    [Key]
    [Column("Key")]
    [MaxLength(36)]
    public string KeyField
    {
        get { return this.myKeyField; }
        set
        {
            this.myKeyField = value;
            this.key = null;
        }
    }
    
    public virtual Key Key
    {
        get
        {
            if (this.key == null)
            {
                this.key = CreateKeyFor(this.TypeName, this.KeyField);
            }
            return this.key;
        }
    }
    
    public abstract Tag Tag { get; }
    public abstract string TypeName { get; }
    
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; set; }
    public DateTime? DateDeleted { get; set; }
    public bool IsDeleted { get; set; } = false;
    public bool IsDisabled { get; set; } = false;
    
    // This is internal for testing purposes only!!!
    internal void SetKeyField(string value)
    {
        this.KeyField = value;
        this.key = null;
    }
    
    internal static Key CreateKeyFor(string typeName, string dbKey)
    {
        return new Key(typeName, dbKey);
    }
    
    internal static Key CreateKey<T>(string dbKey) where T : Entity
    {
        return new Key(TypeNameMapper.GetItemName(typeof(T)), dbKey);
    }
    
    internal static Tag CreateTagFor(string typeName, string dbKey, string? name)
    {
        var key = CreateKeyFor(typeName, dbKey);
        if (name != null)
        {
            return new Tag(key, typeName, name);
        }
        else
        {
            return typeName switch
            {
                "TestPoint" => new Tag(key, typeName, $"TP-{dbKey}"),
                _ => new Tag(key, typeName, typeName)
            };
        }
    }
    
    internal static Tag CreateTag<T>(string dbKey, string? name) where T : Entity
    {
        var typeName = TypeNameMapper.GetItemName(typeof(T));
        return CreateTagFor(typeName, dbKey, name);
    }
    
    public static string GetDbKey(Key key)
    {
        if (Guid.TryParse(key.Value, out Guid result))
        {
            // Valid GUID - return as string
            return result.ToString();
        }
        // Not a GUID - return as-is
        return key.Value;
    }
    
    public static string? GetDbKeyOrDefault(Key? key)
    {
        return key?.Value;
    }
    
    public static string GetDbKey(string type, Key key)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Type cannot be null or empty.", nameof(type));
        }
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }
        if (key.Type != type)
        {
            throw new ArgumentException("Key is not of specified type.", nameof(key));
        }
        return key.Value;
    }
}
