using PoTool.Client.Models;

namespace PoTool.Client.Services;

/// <summary>
/// Service interface for managing navigation context throughout the application.
/// Provides immutable context management with URL serialization support.
/// </summary>
public interface INavigationContextService
{
    /// <summary>
    /// Gets the current active context (immutable).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no profile is selected.</exception>
    NavigationContext Current { get; }

    /// <summary>
    /// Gets the current context or null if none is set.
    /// Use this for checking context state without throwing exceptions.
    /// </summary>
    NavigationContext? CurrentOrDefault { get; }

    /// <summary>
    /// Navigates to a route with a new context (creates new immutable context).
    /// </summary>
    /// <param name="route">The route to navigate to.</param>
    /// <param name="context">The navigation context to use.</param>
    Task NavigateWithContextAsync(string route, NavigationContext context);

    /// <summary>
    /// Navigates back to the parent context.
    /// </summary>
    Task NavigateBackAsync();

    /// <summary>
    /// Creates a new context based on the current context using the immutable update pattern.
    /// </summary>
    /// <param name="updater">Function that takes the current context and returns a new modified context.</param>
    /// <returns>The new context with updates applied.</returns>
    NavigationContext WithUpdates(Func<NavigationContext, NavigationContext> updater);

    /// <summary>
    /// Checks if the current context allows a specific action.
    /// </summary>
    /// <param name="action">The action to check.</param>
    /// <returns>True if the action is allowed, false otherwise.</returns>
    bool CanPerform(string action);

    /// <summary>
    /// Validates that the current context has a valid profile selected.
    /// </summary>
    /// <returns>True if a valid profile is selected, false otherwise.</returns>
    bool HasValidProfile();

    /// <summary>
    /// Sets the initial context from an intent selection entry point.
    /// </summary>
    /// <param name="intent">The selected intent.</param>
    /// <param name="profileId">The current profile ID.</param>
    void SetInitialContext(Intent intent, int profileId);

    /// <summary>
    /// Clears the current context (e.g., when switching profiles).
    /// </summary>
    void ClearContext();

    /// <summary>
    /// Creates context URL parameters for deep-linking.
    /// </summary>
    /// <param name="context">The context to serialize.</param>
    /// <returns>URL query string representation of the context.</returns>
    string ToQueryString(NavigationContext context);

    /// <summary>
    /// Parses context from URL parameters.
    /// </summary>
    /// <param name="queryString">The URL query string.</param>
    /// <returns>Partial context reconstructed from URL, or null if parsing fails.</returns>
    NavigationContext? FromQueryString(string queryString);

    /// <summary>
    /// Event raised when the navigation context changes.
    /// </summary>
    event EventHandler<NavigationContextChangedEventArgs>? ContextChanged;
}

/// <summary>
/// Event arguments for context change events.
/// </summary>
public class NavigationContextChangedEventArgs : EventArgs
{
    /// <summary>
    /// The previous context (may be null for initial context).
    /// </summary>
    public NavigationContext? Previous { get; init; }

    /// <summary>
    /// The new current context (may be null when context is cleared).
    /// </summary>
    public NavigationContext? Current { get; init; }
}
