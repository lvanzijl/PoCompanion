using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PoTool.Api.Configuration;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Api.Services.MockData;
using PoTool.Api.Services.Onboarding;
using PoTool.Core.Contracts;
using PoTool.Shared.Onboarding;

namespace PoTool.Tests.Unit.Services.Onboarding;

[TestClass]
public sealed class OnboardingVerificationScenarioTests
{
    [TestMethod]
    public async Task HappyBindingChainScenario_IsExecutableEndToEnd()
    {
        await using var provider = await CreateSqliteServiceProviderAsync(OnboardingVerificationScenarioNames.HappyBindingChain);
        var seedService = provider.GetRequiredService<MockConfigurationSeedHostedService>();
        await seedService.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var scenarioService = scope.ServiceProvider.GetRequiredService<IOnboardingVerificationScenarioService>();
        var crudService = CreateCrudService(context, scenarioService);
        var statusService = new OnboardingStatusService(context, Mock.Of<IOnboardingObservability>());

        var connection = await context.OnboardingTfsConnections.SingleAsync();
        var project = await crudService.CreateProjectAsync(new CreateProjectSourceRequest(connection.Id, "battleship-systems-project"), CancellationToken.None);
        Assert.IsTrue(project.Succeeded);

        var root = await crudService.CreateRootAsync(new CreateProductRootRequest(project.Data!.Id, "1001"), CancellationToken.None);
        Assert.IsTrue(root.Succeeded);

        var projectBinding = await crudService.CreateBindingAsync(
            new CreateProductSourceBindingRequest(root.Data!.Id, OnboardingProductSourceTypeDto.Project, ProjectSourceId: project.Data.Id),
            CancellationToken.None);
        Assert.IsTrue(projectBinding.Succeeded);

        var team = await crudService.CreateTeamAsync(new CreateTeamSourceRequest(project.Data.Id, "team-incident-response"), CancellationToken.None);
        Assert.IsTrue(team.Succeeded);

        var teamBinding = await crudService.CreateBindingAsync(
            new CreateProductSourceBindingRequest(root.Data.Id, OnboardingProductSourceTypeDto.Team, TeamSourceId: team.Data!.Id),
            CancellationToken.None);
        Assert.IsTrue(teamBinding.Succeeded);

        var status = await statusService.GetStatusAsync(CancellationToken.None);
        Assert.IsTrue(status.Succeeded);
        Assert.AreEqual(OnboardingConfigurationStatus.Complete, status.Data!.OverallStatus);
        Assert.AreEqual(1, status.Data.Counts.ProjectSourcesValid);
        Assert.AreEqual(1, status.Data.Counts.ProductRootsValid);
        Assert.AreEqual(2, status.Data.Counts.BindingsValid);
    }

    [TestMethod]
    public async Task PermissionDeniedScenario_IsReproducible()
    {
        var lookupClient = CreateLookupClient(OnboardingVerificationScenarioNames.PermissionDenied);

        var result = await lookupClient.GetProjectsAsync(CreateConnection(), null, 10, 0, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.PermissionDenied, result.Error!.Code);
    }

    [TestMethod]
    public async Task StaleProjectScenario_IsReproducible()
    {
        var connection = CreateConnection();
        var lookupClient = CreateLookupClient(OnboardingVerificationScenarioNames.StaleProject);
        var validationService = new OnboardingValidationService(lookupClient, new OnboardingSnapshotMapper(), Mock.Of<IOnboardingObservability>());
        var project = new ProjectSource { ProjectExternalId = "battleship-systems-project" };

        var result = await validationService.ValidateProjectSourceAsync(connection, project, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.NotFound, result.Error!.Code);
    }

    [TestMethod]
    public async Task TeamAssignmentScenario_IsReachable()
    {
        await using var provider = await CreateSqliteServiceProviderAsync(OnboardingVerificationScenarioNames.TeamAssignment);
        var seedService = provider.GetRequiredService<MockConfigurationSeedHostedService>();
        await seedService.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var scenarioService = scope.ServiceProvider.GetRequiredService<IOnboardingVerificationScenarioService>();
        var lookupClient = CreateLookupClient(scenarioService);
        var statusService = new OnboardingStatusService(context, Mock.Of<IOnboardingObservability>());

        var status = await statusService.GetStatusAsync(CancellationToken.None);
        var teams = await lookupClient.GetTeamsAsync(CreateConnection(), "battleship-systems-project", null, 10, 0, CancellationToken.None);

        Assert.IsTrue(status.Succeeded);
        CollectionAssert.Contains(status.Data!.BlockingReasons.Select(issue => issue.Code).ToList(), "TEAM_BINDING_SOURCE_INVALID");
        Assert.IsTrue(teams.Succeeded);
        Assert.HasCount(2, teams.Data!);
    }

    [TestMethod]
    public async Task TeamAssignmentScenario_CanReplaceInvalidBindingAndClearBlocker()
    {
        await using var provider = await CreateSqliteServiceProviderAsync(OnboardingVerificationScenarioNames.TeamAssignment);
        var seedService = provider.GetRequiredService<MockConfigurationSeedHostedService>();
        await seedService.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var scenarioService = scope.ServiceProvider.GetRequiredService<IOnboardingVerificationScenarioService>();
        var crudService = CreateCrudService(context, scenarioService);
        var statusService = new OnboardingStatusService(context, Mock.Of<IOnboardingObservability>());

        var binding = await context.OnboardingProductSourceBindings.SingleAsync(item => item.SourceType == ProductSourceType.Team);
        var replacementTeam = await context.OnboardingTeamSources.SingleAsync(item => item.TeamExternalId != binding.SourceExternalId);

        var update = await crudService.UpdateBindingAsync(
            binding.Id,
            new UpdateProductSourceBindingRequest(true, null, null, replacementTeam.Id, null, null, null),
            CancellationToken.None);
        var status = await statusService.GetStatusAsync(CancellationToken.None);

        Assert.IsTrue(update.Succeeded);
        Assert.AreEqual(replacementTeam.TeamExternalId, update.Data!.SourceExternalId);
        CollectionAssert.DoesNotContain(status.Data!.BlockingReasons.Select(issue => issue.Code).ToList(), "TEAM_BINDING_SOURCE_INVALID");
    }

    [TestMethod]
    public async Task PipelineAssignmentScenario_CanReplaceInvalidBindingAndClearBlocker()
    {
        await using var provider = await CreateSqliteServiceProviderAsync(OnboardingVerificationScenarioNames.PipelineAssignment);
        var seedService = provider.GetRequiredService<MockConfigurationSeedHostedService>();
        await seedService.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var scenarioService = scope.ServiceProvider.GetRequiredService<IOnboardingVerificationScenarioService>();
        var crudService = CreateCrudService(context, scenarioService);
        var statusService = new OnboardingStatusService(context, Mock.Of<IOnboardingObservability>());

        var binding = await context.OnboardingProductSourceBindings.SingleAsync(item => item.SourceType == ProductSourceType.Pipeline);
        var replacementPipeline = await context.OnboardingPipelineSources.SingleAsync(item => item.PipelineExternalId != binding.SourceExternalId);

        var update = await crudService.UpdateBindingAsync(
            binding.Id,
            new UpdateProductSourceBindingRequest(true, null, null, null, replacementPipeline.Id, null, null),
            CancellationToken.None);
        var status = await statusService.GetStatusAsync(CancellationToken.None);

        Assert.IsTrue(update.Succeeded);
        Assert.AreEqual(replacementPipeline.PipelineExternalId, update.Data!.SourceExternalId);
        CollectionAssert.DoesNotContain(status.Data!.BlockingReasons.Select(issue => issue.Code).ToList(), "PIPELINE_BINDING_SOURCE_INVALID");
    }

    [TestMethod]
    public async Task PipelineAndRootLookups_ReturnValidCandidates()
    {
        var lookupClient = CreateLookupClient(OnboardingVerificationScenarioNames.HappyBindingChain);

        var pipelines = await lookupClient.GetPipelinesAsync(CreateConnection(), "battleship-systems-project", null, 10, 0, CancellationToken.None);
        var roots = await lookupClient.SearchWorkItemsAsync(CreateConnection(), null, "battleship-systems-project", new[] { "Epic" }, 10, 0, CancellationToken.None);

        Assert.IsTrue(pipelines.Succeeded);
        Assert.IsTrue(roots.Succeeded);
        Assert.IsGreaterThanOrEqualTo(2, pipelines.Data!.Count);
        Assert.IsGreaterThanOrEqualTo(2, roots.Data!.Count);
    }

    [TestMethod]
    public void SameScenario_ProducesSameResultsOnRerun()
    {
        var first = CreateScenarioService(OnboardingVerificationScenarioNames.HappyBindingChain);
        var second = CreateScenarioService(OnboardingVerificationScenarioNames.HappyBindingChain);

        Assert.AreEqual(first.SelectedScenarioName, second.SelectedScenarioName);
        CollectionAssert.AreEqual(
            first.CurrentScenario!.Lookup.Projects.Select(project => project.ProjectExternalId).ToArray(),
            second.CurrentScenario!.Lookup.Projects.Select(project => project.ProjectExternalId).ToArray());
        CollectionAssert.AreEqual(
            first.CurrentScenario.Lookup.WorkItems.Select(workItem => workItem.WorkItemExternalId).ToArray(),
            second.CurrentScenario.Lookup.WorkItems.Select(workItem => workItem.WorkItemExternalId).ToArray());
        CollectionAssert.AreEqual(
            first.CurrentScenario.Lookup.Pipelines.Select(pipeline => pipeline.PipelineExternalId).ToArray(),
            second.CurrentScenario.Lookup.Pipelines.Select(pipeline => pipeline.PipelineExternalId).ToArray());
    }

    private static OnboardingCrudService CreateCrudService(PoToolDbContext context, IOnboardingVerificationScenarioService scenarioService)
    {
        var lookupClient = CreateLookupClient(scenarioService);
        var validationService = new OnboardingValidationService(lookupClient, new OnboardingSnapshotMapper(), Mock.Of<IOnboardingObservability>());
        var statusService = new OnboardingStatusService(context, Mock.Of<IOnboardingObservability>());
        return new OnboardingCrudService(context, validationService, statusService);
    }

    private static OnboardingLiveLookupClient CreateLookupClient(string scenarioName)
        => CreateLookupClient(CreateScenarioService(scenarioName));

    private static OnboardingLiveLookupClient CreateLookupClient(IOnboardingVerificationScenarioService scenarioService)
        => new(
            new UnusedScopedTfsClientFactory(),
            Mock.Of<IOnboardingObservability>(),
            NullLogger<OnboardingLiveLookupClient>.Instance,
            scenarioService);

    private static IOnboardingVerificationScenarioService CreateScenarioService(string scenarioName)
        => new OnboardingVerificationScenarioService(
            new TfsRuntimeMode(useMockClient: true),
            Options.Create(new OnboardingVerificationOptions
            {
                SelectedScenario = scenarioName
            }));

    private static TfsConnection CreateConnection()
        => new()
        {
            OrganizationUrl = "https://dev.azure.com/mock",
            AuthenticationMode = "Ntlm",
            TimeoutSeconds = 30,
            ApiVersion = "7.0"
        };

    private static async Task<ServiceProvider> CreateSqliteServiceProviderAsync(string scenarioName)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PoToolDbContext>(options => options.UseSqlite(connection));
        services.AddSingleton(new TfsRuntimeMode(useMockClient: true));
        services.AddSingleton<IOptions<OnboardingVerificationOptions>>(Options.Create(new OnboardingVerificationOptions
        {
            SelectedScenario = scenarioName
        }));
        services.AddSingleton<IOnboardingVerificationScenarioService, OnboardingVerificationScenarioService>();
        services.AddSingleton<BattleshipWorkItemGenerator>();
        services.AddSingleton<BattleshipDependencyGenerator>();
        services.AddSingleton<BattleshipPullRequestGenerator>();
        services.AddSingleton<BattleshipPipelineGenerator>();
        services.AddSingleton<MockDataValidator>();
        services.AddSingleton<BattleshipMockDataFacade>();
        services.AddSingleton<MockConfigurationSeedHostedService>();

        var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        await context.Database.EnsureCreatedAsync();
        return provider;
    }

    private sealed class UnusedScopedTfsClientFactory : IOnboardingScopedTfsClientFactory
    {
        public IAsyncDisposableTfsClientSession CreateSession(TfsConnection connection, string? projectName = null, string? defaultAreaPath = null)
            => new UnusedSession();

        private sealed class UnusedSession : IAsyncDisposableTfsClientSession
        {
            public ITfsClient Client => throw new InvalidOperationException("Verification scenarios should satisfy onboarding lookups without using the TFS client.");

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
