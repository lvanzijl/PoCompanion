using System.Text.Json;
using Mediator;
using Microsoft.Extensions.Hosting;
using PoTool.Core.Settings.Queries;
using PoTool.Shared.Settings;

namespace PoTool.Api.Handlers.Settings;

public sealed class GetReleaseNotesQueryHandler : IQueryHandler<GetReleaseNotesQuery, IReadOnlyList<ReleaseNoteDto>>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHostEnvironment _hostEnvironment;

    public GetReleaseNotesQueryHandler(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    public async ValueTask<IReadOnlyList<ReleaseNoteDto>> Handle(GetReleaseNotesQuery query, CancellationToken cancellationToken)
    {
        var releaseNotesPath = Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, "..", "docs", "release-notes.json"));

        if (!File.Exists(releaseNotesPath))
        {
            return [];
        }

        await using var stream = File.OpenRead(releaseNotesPath);
        var notes = await JsonSerializer.DeserializeAsync<List<ReleaseNoteDto>>(stream, SerializerOptions, cancellationToken);
        return notes ?? [];
    }
}
