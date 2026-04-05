using PoTool.Client.Models;
using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

public sealed class GlobalFilterContextResolver
{
    public IReadOnlyList<int> GetAllowedTeamIds(int? productId, IReadOnlyList<ProductDto> products)
    {
        if (!productId.HasValue)
        {
            return products
                .SelectMany(product => product.TeamIds)
                .Distinct()
                .OrderBy(teamId => teamId)
                .ToArray();
        }

        return products
            .Where(product => product.Id == productId.Value)
            .SelectMany(product => product.TeamIds)
            .Distinct()
            .OrderBy(teamId => teamId)
            .ToArray();
    }

    public IReadOnlyList<TeamDto> GetAvailableTeams(
        int? productId,
        IReadOnlyList<ProductDto> products,
        IReadOnlyList<TeamDto> teams)
    {
        var allowedTeamIds = GetAllowedTeamIds(productId, products);
        if (!productId.HasValue)
        {
            return teams
                .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var allowedTeamSet = allowedTeamIds.ToHashSet();
        return teams
            .Where(team => allowedTeamSet.Contains(team.Id))
            .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public FilterLocalBridgeState NormalizeState(
        FilterState currentState,
        FilterLocalBridgeState proposedState,
        IReadOnlyList<ProductDto> products)
    {
        var nextProductId = proposedState.ProductId;
        var nextTeamId = proposedState.TeamId;

        if (nextTeamId.HasValue && !IsTeamAllowed(nextProductId, nextTeamId.Value, products))
        {
            nextTeamId = null;
        }

        return proposedState with
        {
            TeamId = nextTeamId,
            SprintId = nextTeamId.HasValue ? proposedState.SprintId : null,
            FromSprintId = nextTeamId.HasValue ? proposedState.FromSprintId : null,
            ToSprintId = nextTeamId.HasValue ? proposedState.ToSprintId : null
        };
    }

    private bool IsTeamAllowed(int? productId, int teamId, IReadOnlyList<ProductDto> products)
    {
        if (!productId.HasValue)
        {
            return true;
        }

        return products.Any(product => product.Id == productId.Value && product.TeamIds.Contains(teamId));
    }
}
