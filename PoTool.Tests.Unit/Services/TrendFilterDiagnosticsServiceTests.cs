using Microsoft.Extensions.Logging;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Metrics;
using PoTool.Shared.PullRequests;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class TrendFilterDiagnosticsServiceTests
{
    [TestMethod]
    public void BuildSignature_InDevelopment_WithMetadata_ReturnsSignature()
    {
        var service = new TrendFilterDiagnosticsService(new StubHostEnvironment("Development"), new ListLogger<TrendFilterDiagnosticsService>());
        var metadata = new CanonicalFilterMetadata(
            CanonicalFilterKind.PullRequest,
            new PullRequestFilterContextDto
            {
                ProductIds = new FilterSelectionDto<int> { IsAll = false, Values = [5] },
                TeamIds = new FilterSelectionDto<int> { IsAll = false, Values = [7] },
                RepositoryNames = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                IterationPaths = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                CreatedBys = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                Statuses = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                Time = new FilterTimeSelectionDto { Mode = FilterTimeSelectionModeDto.Sprint, SprintId = 100, SprintIds = [] }
            },
            new PullRequestFilterContextDto
            {
                ProductIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                TeamIds = new FilterSelectionDto<int> { IsAll = false, Values = [7] },
                RepositoryNames = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                IterationPaths = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                CreatedBys = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                Statuses = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                Time = new FilterTimeSelectionDto { Mode = FilterTimeSelectionModeDto.Sprint, SprintId = 100, SprintIds = [] }
            },
            ["productIds"],
            [new FilterValidationIssueDto { Field = "productIds", Message = "Product scope was normalized." }],
            new Dictionary<int, string>(),
            new Dictionary<int, string>());

        var signature = service.BuildSignature(
            "Pull request insights",
            metadata,
            ["productIds"],
            [new FilterValidationIssueDto { Field = "productIds", Message = "Product scope was normalized." }],
            "Product scope was normalized.");

        Assert.IsFalse(string.IsNullOrWhiteSpace(signature));
    }

    [TestMethod]
    public void BuildSignature_OutsideDevelopment_ReturnsNull()
    {
        var service = new TrendFilterDiagnosticsService(new StubHostEnvironment("Production"), new ListLogger<TrendFilterDiagnosticsService>());

        var signature = service.BuildSignature("Pull request insights", null, Array.Empty<string>(), Array.Empty<FilterValidationIssueDto>(), null);

        Assert.IsNull(signature);
    }

    private sealed class StubHostEnvironment(string environment) : Microsoft.AspNetCore.Components.WebAssembly.Hosting.IWebAssemblyHostEnvironment
    {
        public string Environment { get; } = environment;
        public string BaseAddress { get; } = "http://localhost/";
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
