using Common.Models;

namespace Common.Interfaces;

public interface IHasTag
{
    Tag Tag { get; }
    Key Key { get; }
}
