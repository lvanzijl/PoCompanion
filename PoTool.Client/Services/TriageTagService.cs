using PoTool.Client.ApiClient;
using PoTool.Shared.BugTriage;

namespace PoTool.Client.Services;

/// <summary>
/// Client service for triage tag catalog management.
/// Communicates with the TriageTagsController via generated API client.
/// </summary>
public class TriageTagService
{
    private readonly ITriageTagsClient _triageTagsClient;

    public TriageTagService(ITriageTagsClient triageTagsClient)
    {
        _triageTagsClient = triageTagsClient;
    }

    /// <summary>
    /// Gets all triage tags.
    /// </summary>
    public async Task<List<TriageTagDto>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        return (await _triageTagsClient.GetAllTagsAsync(cancellationToken)).ToList();
    }

    /// <summary>
    /// Gets only enabled triage tags.
    /// </summary>
    public async Task<List<TriageTagDto>> GetEnabledTagsAsync(CancellationToken cancellationToken = default)
    {
        return (await _triageTagsClient.GetEnabledTagsAsync(cancellationToken)).ToList();
    }

    /// <summary>
    /// Creates a new triage tag.
    /// </summary>
    public async Task<TriageTagOperationResponse> CreateTagAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateTriageTagRequest(name);
        return await _triageTagsClient.CreateTagAsync(request, cancellationToken);
    }

    /// <summary>
    /// Updates a triage tag.
    /// </summary>
    public async Task<TriageTagOperationResponse> UpdateTagAsync(
        int id,
        string? name = null,
        bool? isEnabled = null,
        int? displayOrder = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateTriageTagRequest(id, name, isEnabled, displayOrder);
        return await _triageTagsClient.UpdateTagAsync(id, request, cancellationToken);
    }

    /// <summary>
    /// Deletes a triage tag.
    /// </summary>
    public async Task<TriageTagOperationResponse> DeleteTagAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _triageTagsClient.DeleteTagAsync(id, cancellationToken);
    }

    /// <summary>
    /// Reorders triage tags.
    /// </summary>
    public async Task<TriageTagOperationResponse> ReorderTagsAsync(
        List<int> tagIds,
        CancellationToken cancellationToken = default)
    {
        return await _triageTagsClient.ReorderTagsAsync(tagIds, cancellationToken);
    }
}
