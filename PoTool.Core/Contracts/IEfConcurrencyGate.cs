namespace PoTool.Core.Contracts;

/// <summary>
/// Provides a concurrency gate for Entity Framework Core operations to prevent
/// "A second operation was started on this context instance before a previous operation completed" exceptions.
/// 
/// This gate serializes all EF Core operations within the same DI scope (same request).
/// It does NOT block across different scopes/requests.
/// 
/// Use this when:
/// - A service performs EF operations AND may be called from parallel code paths
/// - You need a safety net to guarantee no concurrent EF access
/// 
/// Registration: Scoped lifetime (one instance per request/scope)
/// </summary>
public interface IEfConcurrencyGate
{
    /// <summary>
    /// Executes an operation with EF concurrency protection, returning a result.
    /// Only one operation can execute at a time within the same scope.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation with EF concurrency protection, without returning a result.
    /// Only one operation can execute at a time within the same scope.
    /// </summary>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);
}
