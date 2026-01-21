using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Client-side service for work item state classifications.
/// Caches classifications and provides helper methods for state classification queries.
/// </summary>
public class StateClassificationService
{
    private readonly ISettingsClient _settingsClient;
    private PoTool.Shared.Settings.GetStateClassificationsResponse? _cachedResponse;
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    public StateClassificationService(ISettingsClient settingsClient)
    {
        _settingsClient = settingsClient ?? throw new ArgumentNullException(nameof(settingsClient));
    }

    /// <summary>
    /// Gets all state classifications, using cached data if available and not expired.
    /// </summary>
    public async Task<PoTool.Shared.Settings.GetStateClassificationsResponse> GetClassificationsAsync(CancellationToken cancellationToken = default)
    {
        // Return cached response if available and not expired
        if (_cachedResponse != null && DateTime.UtcNow - _lastFetch < _cacheExpiration)
        {
            return _cachedResponse;
        }

        // Fetch from API - returns ApiClient type, need to convert to Shared type
        var apiResponse = await _settingsClient.GetStateClassificationsAsync(cancellationToken);
        
        // Convert from API client type to Shared type
        _cachedResponse = new PoTool.Shared.Settings.GetStateClassificationsResponse
        {
            ProjectName = apiResponse.ProjectName,
            Classifications = apiResponse.Classifications.Select(c => new PoTool.Shared.Settings.WorkItemStateClassificationDto
            {
                WorkItemType = c.WorkItemType,
                StateName = c.StateName,
                Classification = (PoTool.Shared.Settings.StateClassification)c.Classification
            }).ToList(),
            IsDefault = apiResponse.IsDefault
        };
        
        _lastFetch = DateTime.UtcNow;

        return _cachedResponse;
    }

    /// <summary>
    /// Gets the classification for a specific work item type and state.
    /// </summary>
    public async Task<PoTool.Shared.Settings.StateClassification> GetClassificationAsync(
        string workItemType,
        string state,
        CancellationToken cancellationToken = default)
    {
        var response = await GetClassificationsAsync(cancellationToken);

        var match = response.Classifications.FirstOrDefault(c =>
            c.WorkItemType.Equals(workItemType, StringComparison.OrdinalIgnoreCase) &&
            c.StateName.Equals(state, StringComparison.OrdinalIgnoreCase));

        return match?.Classification ?? PoTool.Shared.Settings.StateClassification.New; // Default to New if not found
    }

    /// <summary>
    /// Gets all states that are classified as "In Progress" for a specific work item type.
    /// </summary>
    public async Task<List<string>> GetInProgressStatesAsync(
        string workItemType,
        CancellationToken cancellationToken = default)
    {
        var response = await GetClassificationsAsync(cancellationToken);

        return response.Classifications
            .Where(c => c.WorkItemType.Equals(workItemType, StringComparison.OrdinalIgnoreCase) &&
                       c.Classification == PoTool.Shared.Settings.StateClassification.InProgress)
            .Select(c => c.StateName)
            .ToList();
    }

    /// <summary>
    /// Checks if a specific state is classified as "In Progress" for a work item type.
    /// </summary>
    public async Task<bool> IsInProgressStateAsync(
        string workItemType,
        string state,
        CancellationToken cancellationToken = default)
    {
        var classification = await GetClassificationAsync(workItemType, state, cancellationToken);
        return classification == PoTool.Shared.Settings.StateClassification.InProgress;
    }

    /// <summary>
    /// Checks if a specific state is classified as "Done" for a work item type.
    /// </summary>
    public async Task<bool> IsDoneStateAsync(
        string workItemType,
        string state,
        CancellationToken cancellationToken = default)
    {
        var classification = await GetClassificationAsync(workItemType, state, cancellationToken);
        return classification == PoTool.Shared.Settings.StateClassification.Done;
    }

    /// <summary>
    /// Clears the cache, forcing a fresh fetch on the next request.
    /// </summary>
    public void ClearCache()
    {
        _cachedResponse = null;
        _lastFetch = DateTime.MinValue;
    }
}
