using System.Net.Http.Json;
using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

public sealed class ReleaseNotesService
{
    private readonly HttpClient _httpClient;
    private IReadOnlyList<ReleaseNoteDto>? _cachedEntries;

    public ReleaseNotesService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ReleaseNoteDto>> GetReleaseNotesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedEntries is not null)
        {
            return _cachedEntries;
        }

        var entries = await _httpClient.GetFromJsonAsync<List<ReleaseNoteDto>>("api/settings/release-notes", cancellationToken);
        _cachedEntries = entries ?? [];
        return _cachedEntries;
    }
}
