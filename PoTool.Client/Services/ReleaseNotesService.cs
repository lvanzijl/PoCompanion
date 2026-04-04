using PoTool.Client.ApiClient;
using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

public sealed class ReleaseNotesService
{
    private readonly ISettingsClient _settingsClient;
    private IReadOnlyList<ReleaseNoteDto>? _cachedEntries;

    public ReleaseNotesService(ISettingsClient settingsClient)
    {
        _settingsClient = settingsClient;
    }

    public async Task<IReadOnlyList<ReleaseNoteDto>> GetReleaseNotesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedEntries is not null)
        {
            return _cachedEntries;
        }

        try
        {
            _cachedEntries = (await _settingsClient.GetReleaseNotesAsync(cancellationToken)).ToList();
            return _cachedEntries;
        }
        catch (ApiException ex)
        {
            throw GeneratedClientErrorTranslator.ToHttpRequestException(ex);
        }
    }
}
