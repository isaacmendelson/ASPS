using Business.Commands;
using Business.Views;

namespace Business.Handlers;

public class SystemCommandHandlers
{
    private readonly ASView _asView;

    public SystemCommandHandlers(ASView asView)
    {
        _asView = asView;
    }

    public virtual async Task<ReInitializeASViewCommandResult> HandleAsync(ReInitializeASViewCommand command)
    {
        try
        {
            await _asView.ReInitializeAsync();

            return new ReInitializeASViewCommandResult
            {
                Success = true,
                Message = "ASView re-initialized successfully!"
            };
        }
        catch (Exception ex)
        {
            return new ReInitializeASViewCommandResult
            {
                Success = false,
                Message = $"Error re-initializing ASView: {ex.Message}"
            };
        }
    }
}
