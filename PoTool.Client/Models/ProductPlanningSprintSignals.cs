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
    private const double SystemicLoadBaselineThreshold = 4d;
    private const double SystemicLoadContribution = 0.55d;
    private const double ConfidenceDecayAcrossHorizon = 1.9d;
    private const double FarHorizonHighConfidenceBoundary = 0.5d;
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
            .Select(metric =>
            {
                var riskLevel = ClassifyRisk(metric);
                var confidenceLevel = ClassifyConfidence(metric);

                return new ProductPlanningSprintColumn(
                    metric.SprintIndex,
                    $"Sprint {metric.SprintIndex + 1}",
                    riskLevel,
                    confidenceLevel,
                    BuildRiskLabel(riskLevel),
                    BuildConfidenceLabel(confidenceLevel),
                    BuildHeatStyle(riskLevel, confidenceLevel),
                    BuildChips(metric, riskLevel, confidenceLevel),
                    BuildTooltip(metric, riskLevel, confidenceLevel));
            })
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
                    GetSystemicLoadRiskContribution(metric) +
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

    private static double GetSystemicLoadRiskContribution(SprintSignalMetrics metric)
    {
        if (metric.ActiveEpicCount == 0)
        {
            return 0d;
        }

        return metric.LoadBaseline >= SystemicLoadBaselineThreshold &&
               metric.ActiveEpicCount >= Math.Max(4, (int)Math.Floor(metric.LoadBaseline))
            ? SystemicLoadContribution
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
        penalty += GetChangedEpicConfidencePenalty(metric);
        penalty += GetStructureConfidencePenalty(metric);
        penalty += GetForwardShiftConfidencePenalty(metric);

        var confidenceLevel = penalty switch
        {
            >= 2.75d => PlanningBoardSprintConfidenceLevel.Low,
            >= 1.15d => PlanningBoardSprintConfidenceLevel.Medium,
            _ => PlanningBoardSprintConfidenceLevel.High
        };

        return confidenceLevel == PlanningBoardSprintConfidenceLevel.High &&
               IsFarHorizonHighConfidenceCapped(metric)
            ? PlanningBoardSprintConfidenceLevel.Medium
            : confidenceLevel;
    }

    private static double GetChangedEpicConfidencePenalty(SprintSignalMetrics metric)
    {
        var substantialChangeThreshold = Math.Max(3, (int)Math.Ceiling(metric.ActiveEpicCount * SubstantialChangeShare));

        return metric.ChangedEpicCount switch
        {
            var count when count >= substantialChangeThreshold => 1.00d,
            >= 2 => 0.55d,
            1 => 0.30d,
            _ => 0d
        };
    }

    private static double GetStructureConfidencePenalty(SprintSignalMetrics metric)
        => (metric.HasParallelStructureChange, metric.HasOverlapChange) switch
        {
            (true, true) => 0.75d,
            (true, false) or (false, true) => 0.45d,
            _ => 0d
        };

    private static double GetForwardShiftConfidencePenalty(SprintSignalMetrics metric)
        => metric.ForwardShiftCount switch
        {
            > 0 when metric.ForwardShiftCount >= Math.Max(2, (int)Math.Ceiling(metric.ActiveEpicCount * HighForwardShiftShare)) => 0.55d,
            > 0 => 0.30d,
            _ => 0d
        };

    private static bool IsFarHorizonHighConfidenceCapped(SprintSignalMetrics metric)
        => metric.ActiveEpicCount > 0 && metric.DistanceRatio >= FarHorizonHighConfidenceBoundary;

    private static string BuildRiskLabel(PlanningBoardSprintRiskLevel riskLevel)
        => riskLevel switch
        {
            PlanningBoardSprintRiskLevel.High => "Strain elevated",
            PlanningBoardSprintRiskLevel.Medium => "Needs attention",
            _ => "Within typical range"
        };

    private static string BuildConfidenceLabel(PlanningBoardSprintConfidenceLevel confidenceLevel)
        => confidenceLevel switch
        {
            PlanningBoardSprintConfidenceLevel.Low => "Plan provisional",
            PlanningBoardSprintConfidenceLevel.Medium => "Plan less settled",
            _ => "Plan stable (near-term)"
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
        var chips = BuildRiskFactors(metric, riskLevel)
            .Take(2)
            .Concat(BuildConfidenceFactors(metric, confidenceLevel).Take(2))
            .Select(static factor => factor.Chip)
            .Where(static chip => !string.IsNullOrWhiteSpace(chip))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxExplanationChips)
            .ToArray();

        return chips.Length == 0 ? ["Load within board norm", "Near-term plan stable"] : chips;
    }

    private static string BuildTooltip(
        SprintSignalMetrics metric,
        PlanningBoardSprintRiskLevel riskLevel,
        PlanningBoardSprintConfidenceLevel confidenceLevel)
    {
        var riskSentence = BuildRiskFactors(metric, riskLevel)[0].Sentence;
        var confidenceSentence = BuildConfidenceFactors(metric, confidenceLevel)[0].Sentence;

        return $"{riskSentence} {confidenceSentence}";
    }

    private static IReadOnlyList<SignalExplanationFactor> BuildRiskFactors(
        SprintSignalMetrics metric,
        PlanningBoardSprintRiskLevel riskLevel)
    {
        var factors = new List<SignalExplanationFactor>();
        var systemicLoadContribution = GetSystemicLoadRiskContribution(metric);
        var loadContribution = GetLoadRiskContribution(metric);
        var trackContribution = GetTrackRiskContribution(metric);
        var forwardShiftContribution = GetForwardShiftRiskContribution(metric);
        var overlapContribution = GetOverlapRiskContribution(metric);

        if (systemicLoadContribution > 0d)
        {
            factors.Add(new SignalExplanationFactor(
                "Board load already high",
                "Based on the current plan, this suggests higher planning strain because the board is already carrying a heavy load across most active sprints.",
                loadContribution > 0d ? loadContribution + 0.05d : systemicLoadContribution));
        }

        var sitsAboveBoardNorm = metric.ActiveEpicCount > Math.Ceiling(metric.LoadBaseline);

        if (loadContribution > 0d && (systemicLoadContribution == 0d || sitsAboveBoardNorm))
        {
            var chip = loadContribution >= 1.10d
                ? "Load well above board norm"
                : "Load above board norm";
            var sentence = loadContribution >= 1.10d
                ? "Based on the current plan, this suggests higher planning strain because this sprint lands heavier than the board usually carries."
                : "Based on the current plan, this suggests higher planning strain because this sprint sits above the board's usual load.";
            factors.Add(new SignalExplanationFactor(chip, sentence, loadContribution));
        }
        else if (riskLevel == PlanningBoardSprintRiskLevel.Low)
        {
            factors.Add(new SignalExplanationFactor(
                "Load within board norm",
                "Based on the current plan, this sprint sits within the board's usual load and shape.",
                0d));
        }

        if (trackContribution > 0d)
        {
            var chip = metric.ActiveTrackCount >= 3 ? "Parallel work high" : "Parallel work active";
            var sentence = metric.ActiveTrackCount >= 3
                ? "Based on the current plan, this suggests higher planning strain because work is spread across several parallel lanes at once."
                : "Based on the current plan, this suggests higher planning strain because this sprint uses more parallel work than the board usually needs.";
            factors.Add(new SignalExplanationFactor(chip, sentence, trackContribution));
        }

        if (overlapContribution > 0d)
        {
            var chip = metric.OverlapPairCount > metric.OverlapBaseline
                ? "Overlap above board norm"
                : "Overlap pressure";
            var sentence = metric.OverlapPairCount > metric.OverlapBaseline
                ? "Based on the current plan, this suggests higher planning strain because more Epics overlap here than the board typically carries."
                : "Based on the current plan, this suggests higher planning strain because overlapping work is adding pressure in this sprint.";
            factors.Add(new SignalExplanationFactor(chip, sentence, overlapContribution));
        }

        if (forwardShiftContribution > 0d)
        {
            var sentence = forwardShiftContribution >= 0.95d
                ? "Based on the current plan, this suggests higher planning strain because recent pull-ins compressed several Epics into this sprint."
                : "Based on the current plan, this suggests higher planning strain because work was pulled forward into this sprint.";
            factors.Add(new SignalExplanationFactor("Work pulled forward", sentence, forwardShiftContribution));
        }

        return factors
            .OrderByDescending(static factor => factor.Weight)
            .ThenBy(static factor => factor.Chip, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<SignalExplanationFactor> BuildConfidenceFactors(
        SprintSignalMetrics metric,
        PlanningBoardSprintConfidenceLevel confidenceLevel)
    {
        var factors = new List<SignalExplanationFactor>();
        var changedEpicPenalty = GetChangedEpicConfidencePenalty(metric);
        var structurePenalty = GetStructureConfidencePenalty(metric);
        var forwardShiftPenalty = GetForwardShiftConfidencePenalty(metric);

        if (confidenceLevel == PlanningBoardSprintConfidenceLevel.High)
        {
            factors.Add(new SignalExplanationFactor(
                "Near-term plan stable",
                "Based on the current plan, this sprint looks relatively stable because it is near-term and its shape has stayed steady.",
                0d));

            return factors;
        }

        if (IsFarHorizonHighConfidenceCapped(metric) || metric.DistanceRatio >= 0.65d)
        {
            var chip = confidenceLevel == PlanningBoardSprintConfidenceLevel.Low
                ? "Far-future plan provisional"
                : "Far-future view provisional";
            var sentence = confidenceLevel == PlanningBoardSprintConfidenceLevel.Low
                ? "Based on the current plan, this sprint looks more provisional because it sits far enough out that reshaping can still change it materially."
                : "Based on the current plan, this sprint stays provisional because it sits far enough out that even a steady shape should not be read as certain.";
            factors.Add(new SignalExplanationFactor(chip, sentence, Math.Max(metric.DistanceRatio, 0.60d)));
        }

        if (changedEpicPenalty > 0d)
        {
            var chip = metric.ChangedEpicCount >= 2 ? "Plan frequently changed" : "Recent plan changes";
            var sentence = metric.ChangedEpicCount >= 2
                ? "Based on the current plan, this sprint looks less settled because several Epics in it changed recently."
                : "Based on the current plan, this sprint looks less settled because it still reflects a recent plan change.";
            factors.Add(new SignalExplanationFactor(chip, sentence, changedEpicPenalty));
        }

        if (structurePenalty > 0d)
        {
            factors.Add(new SignalExplanationFactor(
                "Structure still shifting",
                "Based on the current plan, this sprint looks less settled because the sprint structure is still changing around it.",
                structurePenalty));
        }

        if (forwardShiftPenalty > 0d)
        {
            factors.Add(new SignalExplanationFactor(
                "Work pulled forward",
                "Based on the current plan, this sprint looks less settled because work was recently pulled into it.",
                forwardShiftPenalty));
        }

        if (factors.Count == 0)
        {
            factors.Add(new SignalExplanationFactor(
                confidenceLevel == PlanningBoardSprintConfidenceLevel.Medium ? "Plan less settled" : "Plan provisional",
                confidenceLevel == PlanningBoardSprintConfidenceLevel.Medium
                    ? "Based on the current plan, this sprint looks less settled because recent reshaping still affects this view."
                    : "Based on the current plan, this sprint looks more provisional because recent changes still make it volatile.",
                0d));
        }

        return factors
            .OrderByDescending(static factor => factor.Weight)
            .ThenBy(static factor => factor.Chip, StringComparer.Ordinal)
            .ToArray();
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
                PlanningBoardSprintRiskLevel.High => $"{currentSignals[increased].Label} now suggests higher planning strain than usual.",
                PlanningBoardSprintRiskLevel.Medium => $"{currentSignals[increased].Label} now needs closer planning attention.",
                _ => $"{currentSignals[increased].Label} now looks more settled."
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
                PlanningBoardSprintRiskLevel.Low => $"{currentSignals[decreased].Label} now sits back within its typical range.",
                _ => $"{currentSignals[decreased].Label} now suggests less planning strain after the latest change."
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
                PlanningBoardSprintConfidenceLevel.Low => $"{currentSignals[decreased].Label} now looks more provisional after recent changes.",
                _ => $"{currentSignals[decreased].Label} now looks less settled after the latest reshaping."
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
            summary = $"{currentSignals[increased].Label} now looks more settled in the current plan.";
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

    /// <summary>
    /// Captures the raw per-sprint counts collected directly from the board before any horizon or baseline calibration is applied.
    /// </summary>
    private sealed record RawSprintSignalMetrics(
        int SprintIndex,
        int ActiveEpicCount,
        int ActiveTrackCount,
        int OverlapPairCount,
        int ChangedEpicCount,
        int ForwardShiftCount,
        bool HasParallelStructureChange,
        bool HasOverlapChange);

    /// <summary>
    /// Carries the calibrated per-sprint signal inputs, including raw counts, board-relative baselines, and normalized horizon distance.
    /// </summary>
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

    private sealed record SignalExplanationFactor(
        string Chip,
        string Sentence,
        double Weight);
}
