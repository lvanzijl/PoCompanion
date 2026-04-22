namespace PoTool.Tests.Unit.Audits;

[TestClass]
[TestCategory("Governance")]
public sealed class PlanBoardExecutionHintMarkupAuditTests
{
    [TestMethod]
    public void PlanBoardExecutionHintSection_RendersExecutionRealityHintExactlyOnce_AfterSprintHeatCopy()
    {
        var content = File.ReadAllText(GetHintSectionPath());

        Assert.AreEqual(1, CountOccurrences(content, "<ExecutionRealityHint "));

        var explanationIndex = content.IndexOf("ProductPlanningBoardUxText.SprintHeatSummary", StringComparison.Ordinal);
        var hintIndex = content.IndexOf("<ExecutionRealityHint ", StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, explanationIndex, "Sprint heat explanatory copy must remain present in the reusable hint section.");
        Assert.IsGreaterThan(explanationIndex, hintIndex, "Execution hint must render below the sprint heat explanatory copy.");
    }

    [TestMethod]
    public void PlanBoard_RendersEarlyHintPreviewBeforeBoardDataState()
    {
        var content = File.ReadAllText(GetPlanBoardPath());

        var previewIndex = content.IndexOf("@if (ShouldRenderEarlyExecutionHintPreview)", StringComparison.Ordinal);
        var boardStateIndex = content.IndexOf("<CanonicalDataStateView TData=\"ProductPlanningBoardRenderModel\"", StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, previewIndex, "Plan Board must expose an early hint preview section.");
        Assert.IsGreaterThan(previewIndex, boardStateIndex, "Early hint preview must render before the slow board data-state region.");
    }

    [TestMethod]
    public void PlanBoard_RendersLoadedHintSectionAboveSprintGrid_AndBeforeTrackMarkup()
    {
        var content = File.ReadAllText(GetPlanBoardPath());

        Assert.AreEqual(2, CountOccurrences(content, "<PlanBoardExecutionHintSection "));

        var loadedSectionIndex = content.IndexOf("<PlanBoardExecutionHintSection Hint=\"@renderModel.Board.ExecutionHint\"", StringComparison.Ordinal);
        var sprintGridIndex = content.IndexOf("<div class=\"planning-board-sprint-signal-grid\"", StringComparison.Ordinal);
        var trackLoopIndex = content.IndexOf("@foreach (var track in renderModel.Tracks)", StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, loadedSectionIndex, "Loaded board markup must keep the reusable hint section.");
        Assert.IsGreaterThan(loadedSectionIndex, sprintGridIndex, "Loaded hint section must render before the sprint grid.");
        Assert.IsGreaterThan(loadedSectionIndex, trackLoopIndex, "Loaded hint section must render before any track markup.");
    }

    [TestMethod]
    public void PlanBoardExecutionHintSection_EarlyAndLoadedMarkupMatchSnapshots()
    {
        var planBoardContent = File.ReadAllText(GetPlanBoardPath());
        var hintSectionContent = File.ReadAllText(GetHintSectionPath());

        var earlySnapshot = ExtractNormalizedBlock(
            planBoardContent,
            "@if (ShouldRenderEarlyExecutionHintPreview)",
            "</MudPaper>");
        var loadedSnapshot = ExtractNormalizedBlock(
            planBoardContent,
            "<PlanBoardExecutionHintSection Hint=\"@renderModel.Board.ExecutionHint\"",
            "</PlanBoardExecutionHintSection>");
        var componentSnapshot = NormalizeWhitespace(hintSectionContent);

        Assert.AreEqual(
            NormalizeWhitespace("""
                @if (ShouldRenderEarlyExecutionHintPreview)
                {
                    <MudPaper Elevation="1" Class="pa-3 planning-board-sprint-heat-preview">
                        <PlanBoardExecutionHintSection Hint="@_earlyExecutionHint"
                                                      ProductId="@_selectedProductId"
                                                      ContextTeamId="@CurrentBoardTeamId" />
                    </MudPaper>
                """),
            earlySnapshot);
        Assert.AreEqual(
            NormalizeWhitespace("""
                <PlanBoardExecutionHintSection Hint="@renderModel.Board.ExecutionHint"
                                              ProductId="@_selectedProductId"
                                              ContextTeamId="@CurrentBoardTeamId">
                    <div class="planning-board-sprint-signal-grid" style="@GetTrackGridStyle(renderModel.MaxSprintCount)">
                """),
            ExtractLoadedSnapshotPrefix(loadedSnapshot));
        Assert.AreEqual(
            NormalizeWhitespace("""
                @using PoTool.Client.Models
                @using PoTool.Shared.Planning
                <MudStack Spacing="1">
                    <MudText Typo="Typo.subtitle2">Sprint heat</MudText>
                    <MudText Typo="Typo.body2" Color="Color.Secondary">
                        @ProductPlanningBoardUxText.SprintHeatSummary
                    </MudText>
                    <ExecutionRealityHint Hint="@Hint"
                                          ProductId="@ProductId"
                                          ContextTeamId="@ContextTeamId" />
                    @if (ChildContent is not null)
                    {
                        @ChildContent
                    }
                </MudStack>
                @code {
                    [Parameter]
                    public ProductPlanningExecutionHintDto? Hint { get; set; }
                    [Parameter]
                    public int? ProductId { get; set; }
                    [Parameter]
                    public int? ContextTeamId { get; set; }
                    [Parameter]
                    public RenderFragment? ChildContent { get; set; }
                }
                """),
            componentSnapshot);
    }

    private static string GetPlanBoardPath()
        => Path.Combine(GetRepositoryRoot(), "PoTool.Client", "Pages", "Home", "PlanBoard.razor");

    private static string GetHintSectionPath()
        => Path.Combine(GetRepositoryRoot(), "PoTool.Client", "Components", "Planning", "PlanBoardExecutionHintSection.razor");

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PoTool.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }

    private static string ExtractNormalizedBlock(string content, string startMarker, string endMarker)
    {
        var startIndex = content.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, startIndex, $"Could not find block starting with '{startMarker}'.");

        var endIndex = content.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, endIndex, $"Could not find block ending with '{endMarker}'.");

        return NormalizeWhitespace(content[startIndex..(endIndex + endMarker.Length)]);
    }

    private static string NormalizeWhitespace(string content)
        => string.Join('\n', content
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string ExtractLoadedSnapshotPrefix(string loadedSnapshot)
    {
        const string gridMarker = "<div class=\"planning-board-sprint-signal-grid\" style=\"@GetTrackGridStyle(renderModel.MaxSprintCount)\">";
        var gridIndex = loadedSnapshot.IndexOf(gridMarker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, gridIndex, "Loaded snapshot must include the sprint heat grid marker.");

        return NormalizeWhitespace(loadedSnapshot.Substring(0, gridIndex + gridMarker.Length));
    }

    private static int CountOccurrences(string content, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = content.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
