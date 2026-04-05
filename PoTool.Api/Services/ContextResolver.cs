using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Filters;

namespace PoTool.Api.Services;

public sealed record ContextResolutionRequest(
    FilterSelection<int> ProductIds,
    FilterSelection<int> TeamIds,
    IReadOnlyList<int> SprintIds,
    bool RequireExplicitProductSelection = false,
    bool HasExplicitProductSelection = true,
    bool DeriveProductsFromTeams = false);

public sealed record ContextResolutionResult(
    FilterSelection<int> ProductIds,
    IReadOnlyList<int> ValidTeamIds,
    IReadOnlyList<int> ValidSprintIds,
    FilterValidationResult Validation);

public sealed class ContextResolver
{
    private readonly PoToolDbContext _context;

    public ContextResolver(PoToolDbContext context)
    {
        _context = context;
    }

    public async Task<ContextResolutionResult> ResolveAsync(
        ContextResolutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<FilterValidationIssue>();
        var effectiveProductIds = request.ProductIds;

        if (request.RequireExplicitProductSelection && !request.HasExplicitProductSelection)
        {
            issues.Add(new FilterValidationIssue(nameof(request.ProductIds), "An explicit product selection is required for this query."));
        }

        if (request.DeriveProductsFromTeams && request.ProductIds.IsAll && !request.TeamIds.IsAll)
        {
            var derivedProductIds = await _context.ProductTeamLinks
                .AsNoTracking()
                .Where(link => request.TeamIds.Values.Contains(link.TeamId))
                .Select(link => link.ProductId)
                .Distinct()
                .OrderBy(productId => productId)
                .ToArrayAsync(cancellationToken);

            effectiveProductIds = FilterSelection<int>.Selected(derivedProductIds);
        }

        var normalizedSprintIds = request.SprintIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        var validTeamIds = Array.Empty<int>();
        if (!effectiveProductIds.IsAll)
        {
            validTeamIds = await _context.ProductTeamLinks
                .AsNoTracking()
                .Where(link => effectiveProductIds.Values.Contains(link.ProductId))
                .Select(link => link.TeamId)
                .Distinct()
                .OrderBy(teamId => teamId)
                .ToArrayAsync(cancellationToken);

            if (validTeamIds.Length == 0 && (!request.TeamIds.IsAll || normalizedSprintIds.Length > 0))
            {
                issues.Add(new FilterValidationIssue(nameof(request.ProductIds), "Selected products do not have any linked teams."));
            }
            else if (!request.TeamIds.IsAll)
            {
                var invalidTeamIds = request.TeamIds.Values
                    .Except(validTeamIds)
                    .ToArray();
                if (invalidTeamIds.Length > 0)
                {
                    issues.Add(new FilterValidationIssue(nameof(request.TeamIds), "Selected team scope is outside the selected product scope."));
                }
            }
        }

        if (normalizedSprintIds.Length == 0)
        {
            return new ContextResolutionResult(
                effectiveProductIds,
                validTeamIds,
                normalizedSprintIds,
                FilterValidationResult.FromIssues(issues));
        }

        var sprintTeams = await _context.Sprints
            .AsNoTracking()
            .Where(sprint => normalizedSprintIds.Contains(sprint.Id))
            .Select(sprint => new { sprint.Id, sprint.TeamId })
            .ToListAsync(cancellationToken);

        if (sprintTeams.Count != normalizedSprintIds.Length)
        {
            issues.Add(new FilterValidationIssue(nameof(request.SprintIds), "One or more selected sprints were not found."));
        }

        var resolvedSprintIds = sprintTeams
            .Select(sprint => sprint.Id)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        var sprintTeamIds = sprintTeams
            .Select(sprint => sprint.TeamId)
            .Distinct()
            .ToArray();

        if (!request.TeamIds.IsAll && sprintTeamIds.Any(teamId => !request.TeamIds.Values.Contains(teamId)))
        {
            issues.Add(new FilterValidationIssue(nameof(request.SprintIds), "Selected sprint scope does not match the selected team scope."));
        }

        if (!effectiveProductIds.IsAll && sprintTeamIds.Any(teamId => !validTeamIds.Contains(teamId)))
        {
            issues.Add(new FilterValidationIssue(nameof(request.SprintIds), "Selected sprint scope does not belong to the selected product scope."));
        }

        return new ContextResolutionResult(
            effectiveProductIds,
            validTeamIds,
            resolvedSprintIds,
            FilterValidationResult.FromIssues(issues));
    }
}
