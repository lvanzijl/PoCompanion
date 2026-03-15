using DomainConcentrationCanonicalRules = PoTool.Core.Domain.EffortDiagnostics.EffortConcentrationCanonicalRules;
using DomainConcentrationRiskLevel = PoTool.Core.Domain.EffortDiagnostics.ConcentrationRiskLevel;
using DomainCanonicalEffortDiagnosticsStatistics = PoTool.Core.Domain.EffortDiagnostics.CanonicalEffortDiagnosticsStatistics;
using DomainEffortDiagnosticsStatistics = PoTool.Core.Domain.EffortDiagnostics.EffortDiagnosticsStatistics;
using DomainImbalanceCanonicalRules = PoTool.Core.Domain.EffortDiagnostics.EffortImbalanceCanonicalRules;
using DomainImbalanceRiskLevel = PoTool.Core.Domain.EffortDiagnostics.ImbalanceRiskLevel;

namespace PoTool.Core.Metrics.EffortDiagnostics;

/// <summary>
/// Pure analyzer for canonical effort imbalance and concentration math.
/// </summary>
public sealed class EffortDiagnosticsAnalyzer
{
    private readonly DomainEffortDiagnosticsStatistics _statistics;

    /// <summary>
    /// Initializes a new instance of the <see cref="EffortDiagnosticsAnalyzer"/> class.
    /// </summary>
    public EffortDiagnosticsAnalyzer()
        : this(new DomainCanonicalEffortDiagnosticsStatistics())
    {
    }

    internal EffortDiagnosticsAnalyzer(DomainEffortDiagnosticsStatistics statistics)
    {
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
    }

    /// <summary>
    /// Analyzes bucket imbalance across area and iteration families.
    /// </summary>
    public EffortImbalanceAnalysis AnalyzeImbalance(
        IReadOnlyDictionary<string, int> areaBuckets,
        IReadOnlyDictionary<string, int> iterationBuckets,
        double threshold)
    {
        ArgumentNullException.ThrowIfNull(areaBuckets);
        ArgumentNullException.ThrowIfNull(iterationBuckets);

        if (threshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be greater than zero.");
        }

        var analyzedAreaBuckets = AnalyzeImbalanceBuckets(areaBuckets, threshold);
        var analyzedIterationBuckets = AnalyzeImbalanceBuckets(iterationBuckets, threshold);
        var allDeviations = analyzedAreaBuckets
            .Select(bucket => bucket.DeviationFromMean)
            .Concat(analyzedIterationBuckets.Select(bucket => bucket.DeviationFromMean))
            .ToArray();

        if (allDeviations.Length == 0)
        {
            return new EffortImbalanceAnalysis(
                Array.Empty<EffortImbalanceBucket>(),
                Array.Empty<EffortImbalanceBucket>(),
                ImbalanceRiskLevel.Low,
                0);
        }

        var imbalanceScore = DomainImbalanceCanonicalRules.ComputeImbalanceScore(allDeviations);
        var overallRiskLevel = ToMetricsImbalanceRiskLevel(
            DomainImbalanceCanonicalRules.ClassifyOverallRisk(allDeviations.Max()));

        return new EffortImbalanceAnalysis(
            analyzedAreaBuckets,
            analyzedIterationBuckets,
            overallRiskLevel,
            imbalanceScore);
    }

    /// <summary>
    /// Analyzes bucket concentration across area and iteration families.
    /// </summary>
    public EffortConcentrationAnalysis AnalyzeConcentration(
        IReadOnlyDictionary<string, int> areaBuckets,
        IReadOnlyDictionary<string, int> iterationBuckets)
    {
        ArgumentNullException.ThrowIfNull(areaBuckets);
        ArgumentNullException.ThrowIfNull(iterationBuckets);

        var totalEffort = ResolveSharedTotalEffort(areaBuckets, iterationBuckets);
        var analyzedAreaBuckets = AnalyzeConcentrationBuckets(areaBuckets, totalEffort);
        var analyzedIterationBuckets = AnalyzeConcentrationBuckets(iterationBuckets, totalEffort);
        var allShares = analyzedAreaBuckets
            .Select(bucket => bucket.EffortShare)
            .Concat(analyzedIterationBuckets.Select(bucket => bucket.EffortShare))
            .ToArray();

        if (allShares.Length == 0)
        {
            return new EffortConcentrationAnalysis(
                Array.Empty<EffortConcentrationBucket>(),
                Array.Empty<EffortConcentrationBucket>(),
                ConcentrationRiskLevel.None,
                0);
        }

        var concentrationIndex = DomainConcentrationCanonicalRules.ComputeConcentrationIndex(allShares, _statistics);
        var overallRiskLevel = ToMetricsConcentrationRiskLevel(
            DomainConcentrationCanonicalRules.ClassifyOverallRisk(allShares.Max()));

        return new EffortConcentrationAnalysis(
            analyzedAreaBuckets,
            analyzedIterationBuckets,
            overallRiskLevel,
            concentrationIndex);
    }

    private List<EffortImbalanceBucket> AnalyzeImbalanceBuckets(
        IReadOnlyDictionary<string, int> buckets,
        double threshold)
    {
        if (buckets.Count == 0)
        {
            return new List<EffortImbalanceBucket>();
        }

        var meanEffort = _statistics.Mean(buckets.Values.Select(value => (double)value));

        return buckets
            .Select(bucket =>
            {
                var effortAmount = (double)bucket.Value;
                var deviation = _statistics.DeviationFromMean(effortAmount, meanEffort);
                var riskLevel = ToMetricsImbalanceRiskLevel(
                    DomainImbalanceCanonicalRules.ClassifyBucketRisk(deviation, threshold));

                return new EffortImbalanceBucket(
                    bucket.Key,
                    effortAmount,
                    meanEffort,
                    deviation,
                    riskLevel);
            })
            .OrderByDescending(bucket => bucket.DeviationFromMean)
            .ToList();
    }

    private List<EffortConcentrationBucket> AnalyzeConcentrationBuckets(
        IReadOnlyDictionary<string, int> buckets,
        double totalEffort)
    {
        if (buckets.Count == 0)
        {
            return new List<EffortConcentrationBucket>();
        }

        return buckets
            .Select(bucket =>
            {
                var effortAmount = (double)bucket.Value;
                var effortShare = _statistics.ShareOfTotal(effortAmount, totalEffort);
                var riskLevel = ToMetricsConcentrationRiskLevel(
                    DomainConcentrationCanonicalRules.ClassifyBucketRisk(effortShare));

                return new EffortConcentrationBucket(
                    bucket.Key,
                    effortAmount,
                    effortShare,
                    riskLevel);
            })
            .OrderByDescending(bucket => bucket.EffortShare)
            .ToList();
    }

    private static double ResolveSharedTotalEffort(
        IReadOnlyDictionary<string, int> areaBuckets,
        IReadOnlyDictionary<string, int> iterationBuckets)
    {
        var areaTotal = areaBuckets.Values.Sum(value => (double)value);
        var iterationTotal = iterationBuckets.Values.Sum(value => (double)value);

        if (areaTotal > 0 && iterationTotal > 0 && Math.Abs(areaTotal - iterationTotal) > 0.0001d)
        {
            throw new ArgumentException("Area and iteration bucket totals must describe the same observed effort.");
        }

        return Math.Max(areaTotal, iterationTotal);
    }

    private static ImbalanceRiskLevel ToMetricsImbalanceRiskLevel(DomainImbalanceRiskLevel riskLevel)
    {
        return riskLevel switch
        {
            DomainImbalanceRiskLevel.Low => ImbalanceRiskLevel.Low,
            DomainImbalanceRiskLevel.Medium => ImbalanceRiskLevel.Medium,
            DomainImbalanceRiskLevel.High => ImbalanceRiskLevel.High,
            DomainImbalanceRiskLevel.Critical => ImbalanceRiskLevel.Critical,
            _ => throw new ArgumentOutOfRangeException(nameof(riskLevel), riskLevel, null)
        };
    }

    private static ConcentrationRiskLevel ToMetricsConcentrationRiskLevel(DomainConcentrationRiskLevel riskLevel)
    {
        return riskLevel switch
        {
            DomainConcentrationRiskLevel.None => ConcentrationRiskLevel.None,
            DomainConcentrationRiskLevel.Low => ConcentrationRiskLevel.Low,
            DomainConcentrationRiskLevel.Medium => ConcentrationRiskLevel.Medium,
            DomainConcentrationRiskLevel.High => ConcentrationRiskLevel.High,
            DomainConcentrationRiskLevel.Critical => ConcentrationRiskLevel.Critical,
            _ => throw new ArgumentOutOfRangeException(nameof(riskLevel), riskLevel, null)
        };
    }
}
