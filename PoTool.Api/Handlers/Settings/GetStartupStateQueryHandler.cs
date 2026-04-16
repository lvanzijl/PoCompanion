using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Settings.Queries;
using PoTool.Shared.Settings;

namespace PoTool.Api.Handlers.Settings;

public sealed class GetStartupStateQueryHandler : IQueryHandler<GetStartupStateQuery, StartupStateResponseDto>
{
    private readonly StartupStateResolutionService _startupStateResolutionService;

    public GetStartupStateQueryHandler(StartupStateResolutionService startupStateResolutionService)
    {
        _startupStateResolutionService = startupStateResolutionService;
    }

    public async ValueTask<StartupStateResponseDto> Handle(GetStartupStateQuery query, CancellationToken cancellationToken)
    {
        return await _startupStateResolutionService.ResolveAsync(query.ReturnUrl, query.ProfileHintId, cancellationToken);
    }
}
