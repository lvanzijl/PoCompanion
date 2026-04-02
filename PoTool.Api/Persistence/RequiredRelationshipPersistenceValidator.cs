using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace PoTool.Api.Persistence;

/// <summary>
/// Enforces the repository-wide persistence contract before EF Core writes to the database.
/// Required dependent relationships must be fully resolved through explicit foreign keys or
/// tracked parent navigations before persistence starts.
/// </summary>
internal static class RequiredRelationshipPersistenceValidator
{
    public static void ValidatePendingRequiredRelationships(
        PoToolDbContext context,
        string operation)
    {
        ValidatePendingRequiredRelationshipsCore(
            context,
            operation,
            resolvePrincipal: (entityType, keyValues) => context.Find(entityType.ClrType, keyValues));
    }

    public static Task ValidatePendingRequiredRelationshipsAsync(
        PoToolDbContext context,
        string operation,
        CancellationToken cancellationToken)
    {
        return ValidatePendingRequiredRelationshipsCore(
            context,
            operation,
            resolvePrincipal: (entityType, keyValues) => context.FindAsync(entityType.ClrType, keyValues, cancellationToken).AsTask());
    }

    private static async Task ValidatePendingRequiredRelationshipsCore(
        PoToolDbContext context,
        string operation,
        Func<IEntityType, object?[], object?> resolvePrincipal)
    {
        var pendingEntries = context.ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .ToList();

        foreach (var entry in pendingEntries)
        {
            foreach (var foreignKey in entry.Metadata.GetForeignKeys().Where(IsRequiredForeignKey))
            {
                if (HasTrackedPrincipalNavigation(entry, foreignKey))
                {
                    continue;
                }

                var foreignKeyValues = foreignKey.Properties
                    .Select(property => entry.Property(property.Name).CurrentValue)
                    .ToArray();

                if (HasMissingForeignKeyValue(foreignKey.Properties, foreignKeyValues))
                {
                    throw new InvalidOperationException(
                        $"Persistence contract validation failed while {operation}: entity '{entry.Metadata.ClrType.Name}' is missing required foreign key '{FormatPropertyList(foreignKey.Properties)}' for parent '{foreignKey.PrincipalEntityType.ClrType.Name}'.");
                }

                if (FindTrackedPrincipal(context, foreignKey, foreignKeyValues) is not null)
                {
                    continue;
                }

                var resolvedPrincipal = resolvePrincipal(foreignKey.PrincipalEntityType, foreignKeyValues);
                if (resolvedPrincipal is Task<object?> principalTask)
                {
                    resolvedPrincipal = await principalTask;
                }

                if (resolvedPrincipal is null)
                {
                    throw new InvalidOperationException(
                        $"Persistence contract validation failed while {operation}: entity '{entry.Metadata.ClrType.Name}' references missing parent '{foreignKey.PrincipalEntityType.ClrType.Name}' via '{FormatPropertyList(foreignKey.Properties)}' = {FormatValues(foreignKeyValues)}.");
                }
            }
        }
    }

    private static bool IsRequiredForeignKey(IForeignKey foreignKey)
        => foreignKey.Properties.All(property => !property.IsNullable);

    private static bool HasTrackedPrincipalNavigation(EntityEntry entry, IForeignKey foreignKey)
    {
        if (foreignKey.DependentToPrincipal?.Name is not { Length: > 0 } navigationName)
        {
            return false;
        }

        return entry.Navigation(navigationName).CurrentValue is not null;
    }

    private static bool HasMissingForeignKeyValue(
        IReadOnlyList<IProperty> properties,
        IReadOnlyList<object?> values)
    {
        for (var index = 0; index < properties.Count; index++)
        {
            if (IsMissingValue(values[index], properties[index].ClrType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMissingValue(object? value, Type clrType)
    {
        if (value is null)
        {
            return true;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text);
        }

        var effectiveType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (!effectiveType.IsValueType)
        {
            return false;
        }

        var defaultValue = Activator.CreateInstance(effectiveType);
        return Equals(value, defaultValue);
    }

    private static EntityEntry? FindTrackedPrincipal(
        PoToolDbContext context,
        IForeignKey foreignKey,
        IReadOnlyList<object?> foreignKeyValues)
    {
        return context.ChangeTracker.Entries()
            .Where(entry => entry.Metadata == foreignKey.PrincipalEntityType && entry.State != EntityState.Deleted)
            .FirstOrDefault(entry => PrincipalKeyMatches(entry, foreignKey, foreignKeyValues));
    }

    private static bool PrincipalKeyMatches(
        EntityEntry principalEntry,
        IForeignKey foreignKey,
        IReadOnlyList<object?> foreignKeyValues)
    {
        for (var index = 0; index < foreignKey.PrincipalKey.Properties.Count; index++)
        {
            var principalValue = principalEntry.Property(foreignKey.PrincipalKey.Properties[index].Name).CurrentValue;
            if (!Equals(principalValue, foreignKeyValues[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatPropertyList(IEnumerable<IProperty> properties)
        => string.Join(", ", properties.Select(property => property.Name));

    private static string FormatValues(IEnumerable<object?> values)
        => $"[{string.Join(", ", values.Select(FormatValue))}]";

    private static string FormatValue(object? value)
        => value switch
        {
            null => "null",
            string text when string.IsNullOrWhiteSpace(text) => "\"\"",
            _ => value.ToString() ?? string.Empty
        };
}
