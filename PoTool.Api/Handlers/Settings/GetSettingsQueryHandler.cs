using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for retrieving application settings.
/// </summary>
public class GetSettingsQueryHandler : IQueryHandler<GetSettingsQuery, SettingsDto?>
{
    private readonly ISettingsRepository _repository;

    public GetSettingsQueryHandler(ISettingsRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<SettingsDto?> Handle(GetSettingsQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetSettingsAsync(cancellationToken);
    }
}
