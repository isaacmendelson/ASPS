using System.ComponentModel.DataAnnotations.Schema;
using Common.Enums;
using Common.Models;

namespace Common.Entities;

public class UserAccount : Entity
{
    private Tag? tag;
    
    public string UserKeyField { get; set; } = string.Empty;  // Changed from Guid to string
    public AccountType AccountType { get; set; }
    public string LoginUrl { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool Is2FactorAuth { get; set; }
    public string LoginPhoneNumber { get; set; } = string.Empty;

    // Navigation property removed - use repository to fetch user if needed
    
    // Computed UserKey property for compatibility
    [NotMapped]
    public Key UserKey
    {
        get => new Key("User", UserKeyField);
        set => UserKeyField = value.Value;
    }
    
    [NotMapped]
    public override string TypeName => "UserAccount";
    
    [NotMapped]
    public override Tag Tag
    {
        get
        {
            if (this.tag is not null)
                return this.tag;
                
            if (!string.IsNullOrEmpty(this.KeyField))
            {
                this.tag = this.CreateTag();
                return this.tag;
            }
            
            return this.CreateTag();
        }
    }
    
    protected virtual Tag CreateTag()
    {
        var name = !string.IsNullOrWhiteSpace(UserName) ? UserName : AccountType.ToString();
        return new Tag(this.Key, name, this.TypeName);
    }
}
