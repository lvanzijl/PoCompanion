namespace PoTool.Tests.Unit.Audits;

[TestClass]
[TestCategory("Governance")]
public sealed class PlanBoardExecutionHintMarkupAuditTests
{
    [TestMethod]
    public void PlanBoard_RendersExecutionRealityHintExactlyOnce_BetweenSprintHeatCopyAndGrid()
    {
        var content = File.ReadAllText(GetPlanBoardPath());

        Assert.AreEqual(1, CountOccurrences(content, "<ExecutionRealityHint "));

        var explanationIndex = content.IndexOf("not delivery certainty.", StringComparison.Ordinal);
        var hintIndex = content.IndexOf("<ExecutionRealityHint ", StringComparison.Ordinal);
        var sprintGridIndex = content.IndexOf("<div class=\"planning-board-sprint-signal-grid\"", StringComparison.Ordinal);

        Assert.IsTrue(explanationIndex >= 0, "Sprint heat explanatory text must remain present.");
        Assert.IsTrue(hintIndex > explanationIndex, "Execution hint must render below the sprint heat explanatory text.");
        Assert.IsTrue(sprintGridIndex > hintIndex, "Execution hint must render directly above the sprint heat grid.");
    }

    [TestMethod]
    public void PlanBoard_DoesNotRenderExecutionRealityHintInsideTrackOrEpicMarkup()
    {
        var content = File.ReadAllText(GetPlanBoardPath());

        var hintIndex = content.IndexOf("<ExecutionRealityHint ", StringComparison.Ordinal);
        var trackLoopIndex = content.IndexOf("@foreach (var track in renderModel.Tracks)", StringComparison.Ordinal);
        var epicCardIndex = content.IndexOf("<MudCard Class=\"@GetEpicCardClass(epic)\"", StringComparison.Ordinal);

        Assert.IsTrue(hintIndex >= 0, "Execution hint must be present in PlanBoard markup.");
        Assert.IsTrue(trackLoopIndex > hintIndex, "Execution hint must render before any track markup.");
        Assert.IsTrue(epicCardIndex > hintIndex, "Execution hint must not be nested inside epic card markup.");
    }

    private static string GetPlanBoardPath()
        => Path.Combine(GetRepositoryRoot(), "PoTool.Client", "Pages", "Home", "PlanBoard.razor");

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
