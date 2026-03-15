using PoTool.Core.Domain.BacklogQuality.Models;

namespace PoTool.Core.Domain.BacklogQuality.Services;

/// <summary>
/// Single façade entrypoint for canonical backlog-quality analysis.
/// </summary>
public sealed class BacklogQualityAnalyzer
{
    private readonly BacklogValidationService _backlogValidationService;
    private readonly BacklogReadinessService _backlogReadinessService;
    private readonly ImplementationReadinessService _implementationReadinessService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BacklogQualityAnalyzer"/> class.
    /// </summary>
    public BacklogQualityAnalyzer()
        : this(
            new BacklogValidationService(),
            new BacklogReadinessService(),
            new ImplementationReadinessService())
    {
    }

    internal BacklogQualityAnalyzer(
        BacklogValidationService backlogValidationService,
        BacklogReadinessService backlogReadinessService,
        ImplementationReadinessService implementationReadinessService)
    {
        _backlogValidationService = backlogValidationService ?? throw new ArgumentNullException(nameof(backlogValidationService));
        _backlogReadinessService = backlogReadinessService ?? throw new ArgumentNullException(nameof(backlogReadinessService));
        _implementationReadinessService = implementationReadinessService ?? throw new ArgumentNullException(nameof(implementationReadinessService));
    }

    /// <summary>
    /// Analyzes the canonical backlog graph and returns aligned validation and readiness outputs.
    /// </summary>
    public BacklogQualityAnalysisResult Analyze(BacklogGraph backlogGraph)
    {
        ArgumentNullException.ThrowIfNull(backlogGraph);

        var validation = _backlogValidationService.Validate(backlogGraph);
        var readinessScores = _backlogReadinessService.Compute(backlogGraph);
        var implementationStates = _implementationReadinessService.Compute(backlogGraph);

        var coherentValidation = new BacklogValidationResult(
            validation.IntegrityFindings,
            validation.Findings,
            validation.RefinementStates,
            implementationStates);

        return new BacklogQualityAnalysisResult(coherentValidation, readinessScores);
    }
}
