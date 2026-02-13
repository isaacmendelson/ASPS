namespace Common.Models;

public abstract class LocalizableMessage
{
    public string Key { get; protected set; } = string.Empty;
    
    public abstract override string ToString();
}
