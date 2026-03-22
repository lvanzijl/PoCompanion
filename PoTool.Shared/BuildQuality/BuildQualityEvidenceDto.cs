namespace PoTool.Shared.BuildQuality;

/// <summary>
/// Supporting BuildQuality evidence and explicit unknown state transport.
/// </summary>
public sealed class BuildQualityEvidenceDto
{
    public int EligibleBuilds { get; set; }

    public int SucceededBuilds { get; set; }

    public int FailedBuilds { get; set; }

    public int PartiallySucceededBuilds { get; set; }

    public int CanceledBuilds { get; set; }

    public int TotalTests { get; set; }

    public int PassedTests { get; set; }

    public int NotApplicableTests { get; set; }

    public int CoveredLines { get; set; }

    public int TotalLines { get; set; }

    public bool BuildThresholdMet { get; set; }

    public bool TestThresholdMet { get; set; }

    public bool SuccessRateUnknown { get; set; }

    public string? SuccessRateUnknownReason { get; set; }

    public bool TestPassRateUnknown { get; set; }

    public string? TestPassRateUnknownReason { get; set; }

    public bool CoverageUnknown { get; set; }

    public string? CoverageUnknownReason { get; set; }
}
