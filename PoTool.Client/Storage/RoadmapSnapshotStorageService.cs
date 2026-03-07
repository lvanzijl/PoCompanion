using System.Text.Json;
using Microsoft.JSInterop;
using PoTool.Client.Services;

namespace PoTool.Client.Storage;

/// <summary>
/// Browser-based storage for roadmap snapshots using localStorage.
/// Persists snapshots across navigation and page refreshes.
/// </summary>
public class RoadmapSnapshotStorageService
{
    private readonly IJSRuntime _jsRuntime;
    private const string StorageKey = "roadmap_snapshots";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RoadmapSnapshotStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    /// <summary>
    /// Saves a snapshot to localStorage.
    /// New snapshots are prepended (newest first).
    /// </summary>
    public async Task SaveSnapshotAsync(RoadmapSnapshot snapshot)
    {
        var snapshots = await LoadAllSnapshotsAsync();
        var list = new List<RoadmapSnapshot>(snapshots) { snapshot };
        list.Sort((a, b) => b.CreatedAtUtc.CompareTo(a.CreatedAtUtc));
        await PersistAsync(list);
    }

    /// <summary>
    /// Loads all stored snapshots, ordered newest first.
    /// </summary>
    public async Task<IReadOnlyList<RoadmapSnapshot>> LoadAllSnapshotsAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(json))
                return [];

            return JsonSerializer.Deserialize<List<RoadmapSnapshot>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Gets a single snapshot by its ID.
    /// </summary>
    public async Task<RoadmapSnapshot?> GetSnapshotAsync(string id)
    {
        var snapshots = await LoadAllSnapshotsAsync();
        return snapshots.FirstOrDefault(s => s.Id == id);
    }

    /// <summary>
    /// Deletes a snapshot by its ID.
    /// </summary>
    public async Task DeleteSnapshotAsync(string id)
    {
        var snapshots = await LoadAllSnapshotsAsync();
        var filtered = snapshots.Where(s => s.Id != id).ToList();
        await PersistAsync(filtered);
    }

    private async Task PersistAsync(List<RoadmapSnapshot> snapshots)
    {
        try
        {
            var json = JsonSerializer.Serialize(snapshots, JsonOptions);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch
        {
            // Silently fail — snapshot persistence is not critical
        }
    }
}
