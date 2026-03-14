using Common.Messaging;

namespace Business.Commands;

public class ReInitializeASViewCommand : Command
{
    public ReInitializeASViewCommand()
    {
        CommandType = nameof(ReInitializeASViewCommand);
    }
    
    public string CommandType { get; set; }
}

public class ReInitializeASViewCommandResult : CommandResult
{
}
