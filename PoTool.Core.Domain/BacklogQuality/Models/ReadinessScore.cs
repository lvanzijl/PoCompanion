namespace PoTool.Core.Domain.BacklogQuality.Models;

/// <summary>
/// Value object representing backlog readiness as a bounded percentage.
/// </summary>
public readonly record struct ReadinessScore : IComparable<ReadinessScore>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReadinessScore"/> struct.
    /// </summary>
    public ReadinessScore(int value)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Readiness score must be between 0 and 100.");
        }

        Value = value;
    }

    /// <summary>
    /// Gets the numeric score.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Gets a value indicating whether the score represents fully ready scope.
    /// </summary>
    public bool IsFullyReady => Value == 100;

    /// <summary>
    /// Gets a value indicating whether the score represents blocked scope.
    /// </summary>
    public bool IsBlocked => Value == 0;

    /// <summary>
    /// Rounds an average score using midpoint-to-even semantics.
    /// </summary>
    public static ReadinessScore Average(IEnumerable<ReadinessScore> scores)
    {
        ArgumentNullException.ThrowIfNull(scores);

        var values = scores.Select(score => score.Value).ToList();
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one readiness score is required.", nameof(scores));
        }

        return new ReadinessScore((int)Math.Round(values.Average(), MidpointRounding.ToEven));
    }

    /// <inheritdoc />
    public int CompareTo(ReadinessScore other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public override string ToString() => $"{Value}%";
}
