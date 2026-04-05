using PoTool.Client.Models;

namespace PoTool.Client.Services;

public sealed class GlobalFilterCorrectionService
{
    private const int DefaultRollingDays = 180;

    private readonly GlobalFilterStore _globalFilterStore;
    private readonly GlobalFilterRouteService _globalFilterRouteService;
    private readonly GlobalFilterUiState _globalFilterUiState;

    public GlobalFilterCorrectionService(
        GlobalFilterStore globalFilterStore,
        GlobalFilterRouteService globalFilterRouteService,
        GlobalFilterUiState globalFilterUiState)
    {
        _globalFilterStore = globalFilterStore;
        _globalFilterRouteService = globalFilterRouteService;
        _globalFilterUiState = globalFilterUiState;
    }

    public async Task<string?> BuildCorrectedUriAsync(string currentUri, CancellationToken cancellationToken = default)
    {
        var usage = _globalFilterStore.CurrentUsage;
        if (usage is null || usage.Status != FilterResolutionStatus.Invalid)
        {
            return null;
        }

        if (usage.HasRouteProjectAuthority || usage.HasRouteProductAuthority)
        {
            return null;
        }

        var correctedState = usage.State;
        string? correctionMessage = null;

        if (usage.StateIssues.Any(issue => issue.Contains("project alias", StringComparison.OrdinalIgnoreCase)))
        {
            correctedState = correctedState with { ProjectIds = Array.Empty<string>() };
            correctionMessage = "Invalid project selection was removed from the active page state.";
        }

        if (usage.StateIssues.Any(issue => issue.Contains("rolling time selection", StringComparison.OrdinalIgnoreCase)))
        {
            correctedState = correctedState with
            {
                Time = new FilterTimeSelection(
                    FilterTimeMode.Rolling,
                    RollingWindow: DefaultRollingDays,
                    RollingUnit: FilterTimeUnit.Days)
            };
            correctionMessage = "Invalid rolling window was reset to the default 180 day window.";
        }

        if (correctionMessage is null)
        {
            return null;
        }

        _globalFilterUiState.ShowCorrection(correctionMessage);
        return await _globalFilterRouteService.BuildCurrentPageUriAsync(currentUri, correctedState, cancellationToken);
    }
}
