using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

public sealed record GetReleaseNotesQuery : IQuery<IReadOnlyList<ReleaseNoteDto>>;
