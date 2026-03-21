namespace PoTool.Api.Services.BuildQuality;

/// <summary>
/// Scoped raw build fact for BuildQuality computation.
/// </summary>
public sealed record BuildQualityBuildFact(int BuildId, string? Result);

/// <summary>
/// Scoped raw test-run fact for BuildQuality computation.
/// </summary>
public sealed record BuildQualityTestRunFact(int BuildId, int TotalTests, int PassedTests, int NotApplicableTests);

/// <summary>
/// Scoped raw coverage fact for BuildQuality computation.
/// </summary>
public sealed record BuildQualityCoverageFact(int BuildId, int CoveredLines, int TotalLines);
