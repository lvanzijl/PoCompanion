using PoTool.Shared.BuildQuality;

namespace PoTool.Api.Services.BuildQuality;

/// <summary>
/// Computes canonical BuildQuality from already scoped raw facts.
/// </summary>
public interface IBuildQualityProvider
{
    BuildQualityResultDto Compute(
        IEnumerable<BuildQualityBuildFact> builds,
        IEnumerable<BuildQualityTestRunFact> testRuns,
        IEnumerable<BuildQualityCoverageFact> coverages);
}
