namespace PoTool.Client.Services;

public sealed class StartupGateNotificationService
{
    public event Action? ReevaluationRequested;

    public void RequestReevaluation()
    {
        ReevaluationRequested?.Invoke();
    }
}
