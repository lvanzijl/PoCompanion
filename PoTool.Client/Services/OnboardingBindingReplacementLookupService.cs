using System.Net;
using System.Text.Json;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Shared.Onboarding;

namespace PoTool.Client.Services;

public interface IOnboardingBindingReplacementLookupService
{
    Task<OnboardingBindingReplacementLookupResult> GetCandidatesAsync(
        OnboardingProjectContextViewModel? projectContext,
        OnboardingProductSourceTypeDto sourceType,
        IReadOnlyList<OnboardingTeamSourceDto> availableTeams,
        IReadOnlyList<OnboardingPipelineSourceDto> availablePipelines,
        CancellationToken cancellationToken = default);
}

public sealed record OnboardingBindingReplacementCandidateViewModel(
    int SourceId,
    OnboardingProductSourceTypeDto SourceType,
    string DisplayName,
    string Identifier);

public sealed record OnboardingBindingReplacementLookupResult(
    IReadOnlyList<OnboardingBindingReplacementCandidateViewModel> Candidates,
    string? Message,
    bool LookupFailed)
{
    public static OnboardingBindingReplacementLookupResult Success(IReadOnlyList<OnboardingBindingReplacementCandidateViewModel> candidates, string? message = null)
        => new(candidates, message, false);

    public static OnboardingBindingReplacementLookupResult Failure(string message)
        => new(Array.Empty<OnboardingBindingReplacementCandidateViewModel>(), message, true);
}

public sealed class OnboardingBindingReplacementLookupService : IOnboardingBindingReplacementLookupService
{
    private const int DefaultLookupLimit = 100;
    private static readonly JsonSerializerOptions ErrorSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IOnboardingLookupClient _lookupClient;

    public OnboardingBindingReplacementLookupService(IOnboardingLookupClient lookupClient)
    {
        _lookupClient = lookupClient;
    }

    public async Task<OnboardingBindingReplacementLookupResult> GetCandidatesAsync(
        OnboardingProjectContextViewModel? projectContext,
        OnboardingProductSourceTypeDto sourceType,
        IReadOnlyList<OnboardingTeamSourceDto> availableTeams,
        IReadOnlyList<OnboardingPipelineSourceDto> availablePipelines,
        CancellationToken cancellationToken = default)
    {
        if (projectContext is null || string.IsNullOrWhiteSpace(projectContext.ProjectExternalId))
        {
            return OnboardingBindingReplacementLookupResult.Failure("Project context is required before loading replacement candidates.");
        }

        try
        {
            return sourceType switch
            {
                OnboardingProductSourceTypeDto.Team => await LoadTeamCandidatesAsync(projectContext, availableTeams, cancellationToken),
                OnboardingProductSourceTypeDto.Pipeline => await LoadPipelineCandidatesAsync(projectContext, availablePipelines, cancellationToken),
                _ => OnboardingBindingReplacementLookupResult.Failure("Only team and pipeline bindings support replacement selection.")
            };
        }
        catch (ApiException exception)
        {
            return OnboardingBindingReplacementLookupResult.Failure(ParseLookupFailureMessage(exception));
        }
    }

    private async Task<OnboardingBindingReplacementLookupResult> LoadTeamCandidatesAsync(
        OnboardingProjectContextViewModel projectContext,
        IReadOnlyList<OnboardingTeamSourceDto> availableTeams,
        CancellationToken cancellationToken)
    {
        var envelope = await _lookupClient.GetTeamsAsync(projectContext.ProjectExternalId, null, DefaultLookupLimit, 0, cancellationToken);
        var lookupTeams = envelope.Data ?? [];
        if (lookupTeams.Count == 0)
        {
            return OnboardingBindingReplacementLookupResult.Success([], $"No replacement team candidates are available for {projectContext.ProjectName}.");
        }

        var lookupIds = lookupTeams
            .Select(item => item.TeamExternalId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = availableTeams
            .Where(item => lookupIds.Contains(item.TeamExternalId))
            .OrderBy(item => item.Snapshot.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.TeamExternalId, StringComparer.OrdinalIgnoreCase)
            .Select(item => new OnboardingBindingReplacementCandidateViewModel(
                item.Id,
                OnboardingProductSourceTypeDto.Team,
                item.Snapshot.Name,
                item.TeamExternalId))
            .ToList();

        return candidates.Count > 0
            ? OnboardingBindingReplacementLookupResult.Success(candidates)
            : OnboardingBindingReplacementLookupResult.Success([], $"Team lookup returned candidates for {projectContext.ProjectName}, but none are available in the current onboarding context.");
    }

    private async Task<OnboardingBindingReplacementLookupResult> LoadPipelineCandidatesAsync(
        OnboardingProjectContextViewModel projectContext,
        IReadOnlyList<OnboardingPipelineSourceDto> availablePipelines,
        CancellationToken cancellationToken)
    {
        var envelope = await _lookupClient.GetPipelinesAsync(projectContext.ProjectExternalId, null, DefaultLookupLimit, 0, cancellationToken);
        var lookupPipelines = envelope.Data ?? [];
        if (lookupPipelines.Count == 0)
        {
            return OnboardingBindingReplacementLookupResult.Success([], $"No replacement pipeline candidates are available for {projectContext.ProjectName}.");
        }

        var lookupIds = lookupPipelines
            .Select(item => item.PipelineExternalId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = availablePipelines
            .Where(item => lookupIds.Contains(item.PipelineExternalId))
            .OrderBy(item => item.Snapshot.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.PipelineExternalId, StringComparer.OrdinalIgnoreCase)
            .Select(item => new OnboardingBindingReplacementCandidateViewModel(
                item.Id,
                OnboardingProductSourceTypeDto.Pipeline,
                item.Snapshot.Name,
                item.PipelineExternalId))
            .ToList();

        return candidates.Count > 0
            ? OnboardingBindingReplacementLookupResult.Success(candidates)
            : OnboardingBindingReplacementLookupResult.Success([], $"Pipeline lookup returned candidates for {projectContext.ProjectName}, but none are available in the current onboarding context.");
    }

    private static string ParseLookupFailureMessage(ApiException exception)
    {
        if (!string.IsNullOrWhiteSpace(exception.Response))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<OnboardingErrorDto>(exception.Response, ErrorSerializerOptions);
                if (!string.IsNullOrWhiteSpace(parsed?.Message))
                {
                    return parsed.Message;
                }
            }
            catch (JsonException)
            {
            }
        }

        return (HttpStatusCode)exception.StatusCode switch
        {
            HttpStatusCode.Forbidden => "TFS denied the requested lookup.",
            HttpStatusCode.ServiceUnavailable => "TFS is currently unavailable.",
            _ => "The replacement lookup could not be completed."
        };
    }
}
