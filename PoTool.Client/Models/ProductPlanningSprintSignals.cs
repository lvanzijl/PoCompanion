using System.Globalization;
using PoTool.Shared.Planning;

namespace PoTool.Client.Models;

public enum PlanningBoardSprintRiskLevel
{
    Low,
    Medium,
    High
}

public enum PlanningBoardSprintConfidenceLevel
{
    High,
    Medium,
    Low
}

public static class ProductPlanningSprintSignalFactory
{
    private const double EmptyBoardBaseline = 1d;
    private const double ElevatedLoadBaselineOffset = 0.5d;
    private const double HighLoadBaselineOffset = 1.25d;
    private const double ConfidenceDecayAcrossHorizon = 1.9d;
    private const double SubstantialChangeShare = 0.75d;
    private const double HighForwardShiftShare = 0.5d;
    private const int MaxExplanationChips = 4;

    public static IReadOnlyList<ProductPlanningSprintColumn> BuildColumns(
        ProductPlanningBoardDto board,
        int sprintCount,
        ProductPlanningBoardDto? previousBoard = null)
    {
        ArgumentNullException.ThrowIfNull(board);

        var metrics = BuildMetrics(board, sprintCount, previousBoard);
        return metrics
            .Select(metric => new ProductPlanningSprintColumn(
                metric.SprintIndex,
                $"Sprint {metric.SprintIndex + 1}",
                ClassifyRisk(metric),
                ClassifyConfidence(metric),
                BuildRiskLabel(ClassifyRisk(metric)),
                BuildConfidenceLabel(ClassifyConfidence(metric)),
                BuildHeatStyle(ClassifyRisk(metric), ClassifyConfidence(metric)),
                BuildChips(metric, ClassifyRisk(metric), ClassifyConfidence(metric)),
                BuildTooltip(metric, ClassifyRisk(metric), ClassifyConfidence(metric))))
            .ToArray();
    }

    public static IReadOnlyList<string> BuildDeltaSummaries(
        ProductPlanningBoardDto previousBoard,
        ProductPlanningBoardDto currentBoard)
    {
        ArgumentNullException.ThrowIfNull(previousBoard);
        ArgumentNullException.ThrowIfNull(currentBoard);

        var sprintCount = Math.Max(
            Math.Max(GetSprintCount(previousBoard), GetSprintCount(currentBoard)),
            1);

        var previousSignals = BuildColumns(previousBoard, sprintCount);
        var currentSignals = BuildColumns(currentBoard, sprintCount, previousBoard);
        var summaries = new List<string>();

        if (TryBuildRiskDeltaSummary(previousSignals, currentSignals, out var riskSummary))
        {
            summaries.Add(riskSummary);
        }

        if (TryBuildConfidenceDeltaSummary(previousSignals, currentSignals, out var confidenceSummary))
        {
            summaries.Add(confidenceSummary);
        }

        return summaries;
    }

    private static IReadOnlyList<SprintSignalMetrics> BuildMetrics(
        ProductPlanningBoardDto board,
        int sprintCount,
        ProductPlanningBoardDto? previousBoard)
    {
        var previousEpics = previousBoard?.EpicItems.ToDictionary(static epic => epic.EpicId);
        var previousMetrics = previousBoard is null
            ? new Dictionary<int, SprintSignalMetrics>()
            : BuildMetrics(previousBoard, sprintCount, previousBoard: null).ToDictionary(static metric => metric.SprintIndex);

        var rawMetrics = Enumerable.Range(0, Math.Max(1, sprintCount))
            .Select(sprintIndex =>
            {
                var activeEpics = board.EpicItems.Where(epic => IsActiveInSprint(epic, sprintIndex)).ToArray();
                var activeTrackCount = activeEpics.Select(static epic => epic.TrackIndex).Distinct().Count();
                var overlapPairCount = CountOverlapPairs(activeEpics);
                var changedEpicCount = activeEpics.Count(static epic => epic.IsChanged || epic.IsAffected);
                var forwardShiftCount = previousEpics is null
                    ? 0
                    : activeEpics.Count(epic =>
                        previousEpics.TryGetValue(epic.EpicId, out var previousEpic) &&
                        epic.ComputedStartSprintIndex < previousEpic.ComputedStartSprintIndex);

                previousMetrics.TryGetValue(sprintIndex, out var previousMetric);

                return new RawSprintSignalMetrics(
                    sprintIndex,
                    activeEpics.Length,
                    activeTrackCount,
                    overlapPairCount,
                    changedEpicCount,
                    forwardShiftCount,
                    previousMetric is not null && previousMetric.ActiveTrackCount != activeTrackCount,
                    previousMetric is not null && previousMetric.OverlapPairCount != overlapPairCount);
            })
            .ToArray();

        var activeWindow = rawMetrics.Where(static metric => metric.ActiveEpicCount > 0).ToArray();
        var loadBaseline = activeWindow.Length == 0
            ? EmptyBoardBaseline
            : activeWindow.Average(static metric => (double)metric.ActiveEpicCount);
        var trackBaseline = activeWindow.Length == 0
            ? EmptyBoardBaseline
            : activeWindow.Average(static metric => (double)Math.Max(metric.ActiveTrackCount, 1));
        var overlapBaseline = activeWindow.Length == 0
            ? 0d
            : activeWindow.Average(static metric => (double)metric.OverlapPairCount);
        var sprintHorizon = Math.Max(1, Math.Max(1, sprintCount) - 1);

        return rawMetrics
            .Select(metric => new SprintSignalMetrics(
                metric.SprintIndex,
                metric.ActiveEpicCount,
                metric.ActiveTrackCount,
                metric.OverlapPairCount,
                metric.ChangedEpicCount,
                metric.ForwardShiftCount,
                metric.HasParallelStructureChange,
                metric.HasOverlapChange,
                sprintCount <= 1 ? 0d : (double)metric.SprintIndex / sprintHorizon,
                loadBaseline,
                trackBaseline,
                overlapBaseline))
            .ToArray();
    }

    private static PlanningBoardSprintRiskLevel ClassifyRisk(SprintSignalMetrics metric)
    {
        if (metric.ActiveEpicCount == 0)
        {
            return PlanningBoardSprintRiskLevel.Low;
        }

        var score = GetLoadRiskContribution(metric) +
                    GetTrackRiskContribution(metric) +
                    GetForwardShiftRiskContribution(metric) +
                    GetOverlapRiskContribution(metric);

        return score switch
        {
            >= 3d => PlanningBoardSprintRiskLevel.High,
            >= 1.25d => PlanningBoardSprintRiskLevel.Medium,
            _ => PlanningBoardSprintRiskLevel.Low
        };
    }

    private static double GetLoadRiskContribution(SprintSignalMetrics metric)
    {
        var elevatedLoadThreshold = (int)Math.Ceiling(metric.LoadBaseline + ElevatedLoadBaselineOffset);
        var highLoadThreshold = (int)Math.Ceiling(metric.LoadBaseline + HighLoadBaselineOffset);

        if (metric.ActiveEpicCount >= Math.Max(5, highLoadThreshold))
        {
            return 1.55d;
        }

        if (metric.ActiveEpicCount >= 4)
        {
            return 1.10d;
        }

        return metric.ActiveEpicCount >= Math.Max(2, elevatedLoadThreshold)
            ? 0.80d
            : 0d;
    }

    private static double GetTrackRiskContribution(SprintSignalMetrics metric)
    {
        if (metric.ActiveTrackCount >= 3)
        {
            return 1.10d;
        }

        if (metric.ActiveTrackCount >= 2 && metric.TrackBaseline < 1.5d)
        {
            return 0.55d;
        }

        return metric.ActiveTrackCount >= 2 &&
               metric.ActiveEpicCount >= 3 &&
               metric.ActiveTrackCount > metric.TrackBaseline
            ? 0.65d
            : 0d;
    }

    private static double GetForwardShiftRiskContribution(SprintSignalMetrics metric)
    {
        var highForwardShiftThreshold = Math.Max(2, (int)Math.Ceiling(metric.ActiveEpicCount * HighForwardShiftShare));

        if (metric.ForwardShiftCount >= highForwardShiftThreshold)
        {
            return 0.95d;
        }

        return metric.ForwardShiftCount > 0
            ? 0.40d
            : 0d;
    }

    private static double GetOverlapRiskContribution(SprintSignalMetrics metric)
    {
        var elevatedOverlapThreshold = Math.Max(1, (int)Math.Ceiling(metric.OverlapBaseline + 1d));

        if (metric.OverlapPairCount >= 3 || metric.OverlapPairCount >= elevatedOverlapThreshold + 1)
        {
            return 0.95d;
        }

        if (metric.OverlapPairCount >= elevatedOverlapThreshold)
        {
            return 0.45d;
        }

        return metric.OverlapPairCount > 0 && metric.ActiveTrackCount >= 3
            ? 0.30d
            : 0d;
    }

    private static PlanningBoardSprintConfidenceLevel ClassifyConfidence(SprintSignalMetrics metric)
    {
        var penalty = metric.DistanceRatio * ConfidenceDecayAcrossHorizon;
        var substantialChangeThreshold = Math.Max(3, (int)Math.Ceiling(metric.ActiveEpicCount * SubstantialChangeShare));

        penalty += metric.ChangedEpicCount switch
        {
            var count when count >= substantialChangeThreshold => 1.00d,
            >= 2 => 0.55d,
            1 => 0.30d,
            _ => 0d
        };

        penalty += (metric.HasParallelStructureChange, metric.HasOverlapChange) switch
        {
            (true, true) => 0.75d,
            (true, false) or (false, true) => 0.45d,
            _ => 0d
        };

        penalty += metric.ForwardShiftCount switch
        {
            > 0 when metric.ForwardShiftCount >= Math.Max(2, (int)Math.Ceiling(metric.ActiveEpicCount * HighForwardShiftShare)) => 0.55d,
            > 0 => 0.30d,
            _ => 0d
        };

        return penalty switch
        {
            >= 2.75d => PlanningBoardSprintConfidenceLevel.Low,
            >= 1.15d => PlanningBoardSprintConfidenceLevel.Medium,
            _ => PlanningBoardSprintConfidenceLevel.High
        };
    }

    private static string BuildRiskLabel(PlanningBoardSprintRiskLevel riskLevel)
        => riskLevel switch
        {
            PlanningBoardSprintRiskLevel.High => "Risk high",
            PlanningBoardSprintRiskLevel.Medium => "Risk medium",
            _ => "Risk low"
        };

    private static string BuildConfidenceLabel(PlanningBoardSprintConfidenceLevel confidenceLevel)
        => confidenceLevel switch
        {
            PlanningBoardSprintConfidenceLevel.Low => "Confidence low",
            PlanningBoardSprintConfidenceLevel.Medium => "Confidence medium",
            _ => "Confidence high"
        };

    private static string BuildHeatStyle(
        PlanningBoardSprintRiskLevel riskLevel,
        PlanningBoardSprintConfidenceLevel confidenceLevel)
    {
        var rgb = riskLevel switch
        {
            PlanningBoardSprintRiskLevel.High => "198, 40, 40",
            PlanningBoardSprintRiskLevel.Medium => "245, 124, 0",
            _ => "46, 125, 50"
        };

        var alpha = confidenceLevel switch
        {
            PlanningBoardSprintConfidenceLevel.High => 0.26d,
            PlanningBoardSprintConfidenceLevel.Medium => 0.18d,
            _ => 0.10d
        };

        var borderAlpha = Math.Min(alpha + 0.16d, 0.42d);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"background-color: rgba({rgb}, {alpha:0.00}); border: 1px solid rgba({rgb}, {borderAlpha:0.00});");
    }

    private static IReadOnlyList<string> BuildChips(
        SprintSignalMetrics metric,
        PlanningBoardSprintRiskLevel riskLevel,
        PlanningBoardSprintConfidenceLevel confidenceLevel)
    {
        var chips = new List<string>();

        if (riskLevel == PlanningBoardSprintRiskLevel.High)
        {
            chips.Add("High load");
        }
        else if (riskLevel == PlanningBoardSprintRiskLevel.Medium)
        {
            chips.Add("Load above normal");
        }
        else
        {
            chips.Add("Load in range");
        }

        if (metric.ActiveTrackCount >= 3)
        {
            chips.Add("Parallel work high");
        }
        else if (metric.ActiveTrackCount == 2)
        {
            chips.Add("Parallel work active");
        }

        if (metric.ForwardShiftCount > 0)
        {
            chips.Add("Work pulled forward");
        }
        else if (metric.OverlapPairCount > 0)
        {
            chips.Add("Overlap pressure");
        }

        if (confidenceLevel == PlanningBoardSprintConfidenceLevel.Low && metric.DistanceRatio >= 0.65d)
        {
            chips.Add("Low confidence (far future)");
        }
        else if (metric.ChangedEpicCount >= 2)
        {
            chips.Add("Plan frequently changed");
        }
        else if (metric.HasParallelStructureChange || metric.HasOverlapChange)
        {
            chips.Add("Structure still shifting");
        }
        else if (confidenceLevel == PlanningBoardSprintConfidenceLevel.High)
        {
            chips.Add("Confidence steady");
        }

        return chips
            .Distinct(StringComparer.Ordinal)
            .Take(MaxExplanationChips)
            .ToArray();
    }

    private static string BuildTooltip(
        SprintSignalMetrics metric,
        PlanningBoardSprintRiskLevel riskLevel,
        PlanningBoardSprintConfidenceLevel confidenceLevel)
    {
        var riskSentence = riskLevel switch
        {
            PlanningBoardSprintRiskLevel.High => "This sprint looks strained because several Epics land together, parallel work is elevated, or recent moves compressed the plan.",
            PlanningBoardSprintRiskLevel.Medium => "This sprint needs extra attention because the load is stacking up or the plan overlaps more than usual.",
            _ => "This sprint looks manageable with the current load and shape of work."
        };

        var confidenceSentence = confidenceLevel switch
        {
            PlanningBoardSprintConfidenceLevel.Low when metric.DistanceRatio >= 0.65d => "Confidence is low because this sprint sits farther out and recent reshaping can still change it.",
            PlanningBoardSprintConfidenceLevel.Low => "Confidence is low because this sprint changed recently and the structure is still moving.",
            PlanningBoardSprintConfidenceLevel.Medium => "Confidence is medium because some recent changes still affect how trustworthy this sprint is.",
            _ => "Confidence is high because this sprint is near-term and its shape is relatively steady."
        };

        return $"{riskSentence} {confidenceSentence}";
    }

    private static bool TryBuildRiskDeltaSummary(
        IReadOnlyList<ProductPlanningSprintColumn> previousSignals,
        IReadOnlyList<ProductPlanningSprintColumn> currentSignals,
        out string summary)
    {
        summary = string.Empty;

        var increased = Enumerable.Range(0, currentSignals.Count)
            .Where(index => GetRiskRank(currentSignals[index].RiskLevel) > GetRiskRank(previousSignals[index].RiskLevel))
            .OrderByDescending(index => GetRiskRank(currentSignals[index].RiskLevel) - GetRiskRank(previousSignals[index].RiskLevel))
            .ThenBy(index => index)
            .FirstOrDefault(-1);

        if (increased >= 0)
        {
            summary = currentSignals[increased].RiskLevel switch
            {
                PlanningBoardSprintRiskLevel.High => $"{currentSignals[increased].Label} now above normal load.",
                PlanningBoardSprintRiskLevel.Medium => $"{currentSignals[increased].Label} now needs more planning attention.",
                _ => $"{currentSignals[increased].Label} now looks steadier."
            };

            return true;
        }

        var decreased = Enumerable.Range(0, currentSignals.Count)
            .Where(index => GetRiskRank(currentSignals[index].RiskLevel) < GetRiskRank(previousSignals[index].RiskLevel))
            .OrderBy(index => GetRiskRank(currentSignals[index].RiskLevel) - GetRiskRank(previousSignals[index].RiskLevel))
            .ThenBy(index => index)
            .FirstOrDefault(-1);

        if (decreased >= 0)
        {
            summary = currentSignals[decreased].RiskLevel switch
            {
                PlanningBoardSprintRiskLevel.Low => $"{currentSignals[decreased].Label} now looks manageable again.",
                _ => $"{currentSignals[decreased].Label} pressure eased after the latest change."
            };

            return true;
        }

        return false;
    }

    private static bool TryBuildConfidenceDeltaSummary(
        IReadOnlyList<ProductPlanningSprintColumn> previousSignals,
        IReadOnlyList<ProductPlanningSprintColumn> currentSignals,
        out string summary)
    {
        summary = string.Empty;

        var decreased = Enumerable.Range(0, currentSignals.Count)
            .Where(index => GetConfidenceRank(currentSignals[index].ConfidenceLevel) > GetConfidenceRank(previousSignals[index].ConfidenceLevel))
            .OrderByDescending(index => GetConfidenceRank(currentSignals[index].ConfidenceLevel) - GetConfidenceRank(previousSignals[index].ConfidenceLevel))
            .ThenBy(index => index)
            .FirstOrDefault(-1);

        if (decreased >= 0)
        {
            summary = currentSignals[decreased].ConfidenceLevel switch
            {
                PlanningBoardSprintConfidenceLevel.Low => $"Confidence decreased for {currentSignals[decreased].Label} after recent changes.",
                _ => $"Confidence softened for {currentSignals[decreased].Label} after the latest reshaping."
            };

            return true;
        }

        var increased = Enumerable.Range(0, currentSignals.Count)
            .Where(index => GetConfidenceRank(currentSignals[index].ConfidenceLevel) < GetConfidenceRank(previousSignals[index].ConfidenceLevel))
            .OrderBy(index => GetConfidenceRank(currentSignals[index].ConfidenceLevel) - GetConfidenceRank(previousSignals[index].ConfidenceLevel))
            .ThenBy(index => index)
            .FirstOrDefault(-1);

        if (increased >= 0)
        {
            summary = $"Confidence increased for {currentSignals[increased].Label} because the plan is steadier there.";
            return true;
        }

        return false;
    }

    private static int GetRiskRank(PlanningBoardSprintRiskLevel riskLevel)
        => riskLevel switch
        {
            PlanningBoardSprintRiskLevel.High => 2,
            PlanningBoardSprintRiskLevel.Medium => 1,
            _ => 0
        };

    private static int GetConfidenceRank(PlanningBoardSprintConfidenceLevel confidenceLevel)
        => confidenceLevel switch
        {
            PlanningBoardSprintConfidenceLevel.Low => 2,
            PlanningBoardSprintConfidenceLevel.Medium => 1,
            _ => 0
        };

    private static int GetSprintCount(ProductPlanningBoardDto board)
        => board.EpicItems.Count == 0
            ? 0
            : board.EpicItems.Max(static epic => epic.EndSprintIndexExclusive);

    private static bool IsActiveInSprint(PlanningBoardEpicItemDto epic, int sprintIndex)
        => epic.ComputedStartSprintIndex <= sprintIndex && epic.EndSprintIndexExclusive > sprintIndex;

    private static int CountOverlapPairs(IReadOnlyList<PlanningBoardEpicItemDto> epics)
    {
        var overlapCount = 0;

        for (var index = 0; index < epics.Count; index++)
        {
            for (var innerIndex = index + 1; innerIndex < epics.Count; innerIndex++)
            {
                if (epics[index].ComputedStartSprintIndex < epics[innerIndex].EndSprintIndexExclusive &&
                    epics[innerIndex].ComputedStartSprintIndex < epics[index].EndSprintIndexExclusive)
                {
                    overlapCount++;
                }
            }
        }

        return overlapCount;
    }

    private sealed record RawSprintSignalMetrics(
        int SprintIndex,
        int ActiveEpicCount,
        int ActiveTrackCount,
        int OverlapPairCount,
        int ChangedEpicCount,
        int ForwardShiftCount,
        bool HasParallelStructureChange,
        bool HasOverlapChange);

    private sealed record SprintSignalMetrics(
        int SprintIndex,
        int ActiveEpicCount,
        int ActiveTrackCount,
        int OverlapPairCount,
        int ChangedEpicCount,
        int ForwardShiftCount,
        bool HasParallelStructureChange,
        bool HasOverlapChange,
        double DistanceRatio,
        double LoadBaseline,
        double TrackBaseline,
        double OverlapBaseline);
}
