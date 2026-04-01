namespace PoTool.Api.Filters;

public static class ObjectResultTypeContractValidator
{
    public static void EnsureCompatible(Type expectedType, object value, string? displayName)
    {
        ArgumentNullException.ThrowIfNull(expectedType);
        ArgumentNullException.ThrowIfNull(value);

        var actualType = value.GetType();
        var requiresExactType = !expectedType.IsInterface && !expectedType.IsAbstract;
        var isCompatible = requiresExactType
            ? actualType == expectedType
            : expectedType.IsInstanceOfType(value);

        if (isCompatible)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Response contract violation for '{displayName ?? "unknown action"}'. " +
            $"Expected {(requiresExactType ? "exact" : "assignable")} payload type '{expectedType.FullName}' but got '{actualType.FullName}'.");
    }
}
