namespace Common.Interfaces;

public interface ITag
{
    Models.Key Key { get; set; }
    string Name { get; set; }
    string Type { get; set; }
}
