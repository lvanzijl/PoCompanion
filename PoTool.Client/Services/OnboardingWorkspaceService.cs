using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Shared.Onboarding;

namespace PoTool.Client.Services;

public sealed class OnboardingWorkspaceService : IOnboardingWorkspaceService
{
    private static readonly IReadOnlyList<OnboardingFilterOption<OnboardingConfigurationStatus>> StatusOptions =
    [
        new(null, "All statuses"),
        new(OnboardingConfigurationStatus.NotConfigured, "Not configured"),
        new(OnboardingConfigurationStatus.PartiallyConfigured, "Partially configured"),
        new(OnboardingConfigurationStatus.Complete, "Complete")
    ];

    private readonly IOnboardingCrudClient _crudClient;
    private readonly IOnboardingStatusClient _statusClient;

    public OnboardingWorkspaceService(
        IOnboardingCrudClient crudClient,
        IOnboardingStatusClient statusClient)
    {
        _crudClient = crudClient;
        _statusClient = statusClient;
    }

    public async Task<OnboardingWorkspaceData> GetWorkspaceDataAsync(OnboardingWorkspaceFilter filter, CancellationToken cancellationToken = default)
    {
        try
        {
            var statusTask = _statusClient.GetStatusAsync(cancellationToken);
            var connectionsTask = _crudClient.ListConnectionsAsync(filter.Status, cancellationToken);
            var projectsTask = _crudClient.ListProjectsAsync(filter.ConnectionId, filter.Status, cancellationToken);
            var teamsTask = _crudClient.ListTeamsAsync(filter.ConnectionId, filter.ProjectId, filter.Status, cancellationToken);
            var pipelinesTask = _crudClient.ListPipelinesAsync(filter.ConnectionId, filter.ProjectId, filter.Status, cancellationToken);
            var rootsTask = _crudClient.ListRootsAsync(filter.ConnectionId, filter.ProjectId, filter.Status, cancellationToken);
            var bindingsTask = _crudClient.ListBindingsAsync(filter.ConnectionId, filter.ProjectId, filter.ProductRootId, filter.Status, cancellationToken);

            var connectionOptionsTask = _crudClient.ListConnectionsAsync(null, cancellationToken);
            var projectOptionsTask = _crudClient.ListProjectsAsync(filter.ConnectionId, null, cancellationToken);
            var rootOptionsTask = _crudClient.ListRootsAsync(filter.ConnectionId, filter.ProjectId, null, cancellationToken);

            await Task.WhenAll(
                statusTask,
                connectionsTask,
                projectsTask,
                teamsTask,
                pipelinesTask,
                rootsTask,
                bindingsTask,
                connectionOptionsTask,
                projectOptionsTask,
                rootOptionsTask);

            return new OnboardingWorkspaceData(
                filter,
                new OnboardingWorkspaceFilterOptions(
                    BuildConnectionOptions(await connectionOptionsTask),
                    BuildProjectOptions(await projectOptionsTask),
                    BuildRootOptions(await rootOptionsTask),
                    StatusOptions),
                (await statusTask).Data ?? throw new InvalidOperationException("Onboarding status payload was empty."),
                ((await connectionsTask).Data ?? []).ToList(),
                ((await projectsTask).Data ?? []).ToList(),
                ((await teamsTask).Data ?? []).ToList(),
                ((await pipelinesTask).Data ?? []).ToList(),
                ((await rootsTask).Data ?? []).ToList(),
                ((await bindingsTask).Data ?? []).ToList());
        }
        catch (ApiException exception)
        {
            throw GeneratedClientErrorTranslator.ToHttpRequestException(exception);
        }
    }

    private static IReadOnlyList<OnboardingFilterOption<int>> BuildConnectionOptions(OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingTfsConnectionDto envelope)
        => [new(null, "All connections"), .. (envelope.Data ?? []).Select(item => new OnboardingFilterOption<int>(item.Id, item.OrganizationUrl))];

    private static IReadOnlyList<OnboardingFilterOption<int>> BuildProjectOptions(OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingProjectSourceDto envelope)
        => [new(null, "All projects"), .. (envelope.Data ?? []).Select(item => new OnboardingFilterOption<int>(item.Id, $"{item.Snapshot.Name} ({item.ProjectExternalId})"))];

    private static IReadOnlyList<OnboardingFilterOption<int>> BuildRootOptions(OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingProductRootDto envelope)
        => [new(null, "All product roots"), .. (envelope.Data ?? []).Select(item => new OnboardingFilterOption<int>(item.Id, $"{item.Snapshot.Title} ({item.WorkItemExternalId})"))];
}
