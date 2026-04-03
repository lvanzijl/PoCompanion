namespace PoTool.Client.Services;

public sealed class GlobalFilterUiState
{
    public bool IsExpanded { get; private set; }

    public string? CorrectionMessage { get; private set; }

    public event Action? Changed;

    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
        Changed?.Invoke();
    }

    public void Expand()
    {
        if (IsExpanded)
        {
            return;
        }

        IsExpanded = true;
        Changed?.Invoke();
    }

    public void Collapse()
    {
        if (!IsExpanded)
        {
            return;
        }

        IsExpanded = false;
        Changed?.Invoke();
    }

    public void ShowCorrection(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        CorrectionMessage = message;
        Changed?.Invoke();
    }

    public void DismissCorrection()
    {
        if (CorrectionMessage is null)
        {
            return;
        }

        CorrectionMessage = null;
        Changed?.Invoke();
    }
}
