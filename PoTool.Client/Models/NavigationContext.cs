namespace PoTool.Client.Models;

/// <summary>
/// Represents the navigation context that shapes workspace behavior.
/// This is the central artifact that controls how workspaces adapt to user intent.
/// </summary>
public record NavigationContext
{
    /// <summary>
    /// The intent that triggered this navigation.
    /// </summary>
    public required Intent Intent { get; init; }

    /// <summary>
    /// The current scope of work.
    /// </summary>
    public required Scope Scope { get; init; }

    /// <summary>
    /// What triggered entry into the current workspace.
    /// </summary>
    public Trigger? Trigger { get; init; }

    /// <summary>
    /// Time perspective for data.
    /// </summary>
    public TimeHorizon TimeHorizon { get; init; } = TimeHorizon.Current;

    /// <summary>
    /// Source context for back navigation.
    /// </summary>
    public NavigationContext? Parent { get; init; }

}

/// <summary>
/// Represents the user's primary intent when navigating.
/// </summary>
public enum Intent
{
    /// <summary>
    /// Build context and choose scope - "What am I looking at?"
    /// </summary>
    Overzien,

    /// <summary>
    /// Structure and look ahead - "What should happen next?"
    /// </summary>
    Plannen
}

/// <summary>
/// Represents the scope of work in the navigation context.
/// </summary>
public record Scope
{
    /// <summary>
    /// The level of scope (Portfolio, Product, or Team).
    /// </summary>
    public ScopeLevel Level { get; init; }

    /// <summary>
    /// The profile ID (always required after profile selection).
    /// </summary>
    public int? ProfileId { get; init; }

    /// <summary>
    /// Optional project alias for project-scoped routing and filtering.
    /// </summary>
    public string? ProjectAlias { get; init; }

    /// <summary>
    /// The product ID (required for Product/Team scope).
    /// </summary>
    public int? ProductId { get; init; }

    /// <summary>
    /// The team ID (required for Team scope).
    /// </summary>
    public int? TeamId { get; init; }
}

/// <summary>
/// Represents the level of scope in the navigation context.
/// </summary>
public enum ScopeLevel
{
    /// <summary>
    /// All products under the profile.
    /// </summary>
    Portfolio,

    /// <summary>
    /// Single product.
    /// </summary>
    Product,

    /// <summary>
    /// Single team.
    /// </summary>
    Team
}

/// <summary>
/// Represents what triggered entry into the current workspace.
/// </summary>
public record Trigger
{
    /// <summary>
    /// The type of trigger.
    /// </summary>
    public TriggerType Type { get; init; }

    /// <summary>
    /// Identifier of the triggering element.
    /// </summary>
    public string? SourceId { get; init; }

    /// <summary>
    /// Human-readable source description.
    /// </summary>
    public string? SourceLabel { get; init; }
}

/// <summary>
/// Represents the type of trigger that initiated navigation.
/// </summary>
public enum TriggerType
{
    /// <summary>
    /// User explicitly selected this path.
    /// </summary>
    Choice,

    /// <summary>
    /// System detected an issue requiring attention.
    /// </summary>
    Deviation,

    /// <summary>
    /// External request (e.g., from communication).
    /// </summary>
    Request
}

/// <summary>
/// Represents the time perspective for data in the navigation context.
/// </summary>
public enum TimeHorizon
{
    /// <summary>
    /// Now, active sprint, current state.
    /// </summary>
    Current,

    /// <summary>
    /// Past sprints, trends, history.
    /// </summary>
    Historical,

    /// <summary>
    /// Forecasts, plans, projections.
    /// </summary>
    Future
}
