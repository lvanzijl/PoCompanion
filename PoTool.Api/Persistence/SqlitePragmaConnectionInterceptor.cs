using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoTool.Core.Configuration;

namespace PoTool.Api.Persistence;

/// <summary>
/// Applies SQLite PRAGMAs once per opened connection to optimize ingestion performance.
/// </summary>
public sealed class SqlitePragmaConnectionInterceptor : DbConnectionInterceptor
{
    private readonly IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions> _optionsMonitor;
    private readonly ILogger<SqlitePragmaConnectionInterceptor> _logger;
    private readonly bool _isDevelopment;

    public SqlitePragmaConnectionInterceptor(
        IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions> optionsMonitor,
        ILogger<SqlitePragmaConnectionInterceptor> logger,
        IHostEnvironment environment)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _isDevelopment = environment.IsDevelopment();
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyPragmas(connection);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        return ApplyPragmasAsync(connection, cancellationToken);
    }

    private void ApplyPragmas(DbConnection connection)
    {
        if (!ShouldApplyPragmas(connection, out var options))
        {
            return;
        }

        using var command = connection.CreateCommand();
        ExecutePragmas(command, options);
    }

    private async Task ApplyPragmasAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (!ShouldApplyPragmas(connection, out var options))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        await ExecutePragmasAsync(command, options, cancellationToken);
    }

    private bool ShouldApplyPragmas(
        DbConnection connection,
        out RevisionIngestionPersistenceOptimizationOptions options)
    {
        options = _optionsMonitor.CurrentValue;
        if (connection is not SqliteConnection)
        {
            return false;
        }

        return options.Enabled && options.SqlitePragmasEnabled;
    }

    private void ExecutePragmas(DbCommand command, RevisionIngestionPersistenceOptimizationOptions options)
    {
        var synchronous = ResolveSynchronousMode(options.SqliteSynchronous);

        // WAL + NORMAL provides high-throughput ingestion with strong durability; override to OFF in development only.
        ExecutePragma(command, "journal_mode=WAL");
        ExecutePragma(command, $"synchronous={synchronous}");

        if (options.SqliteTempStoreMemory)
        {
            ExecutePragma(command, "temp_store=MEMORY");
        }

        if (options.SqliteCacheSizeKb > 0)
        {
            ExecutePragma(command, $"cache_size=-{options.SqliteCacheSizeKb}");
        }
    }

    private async Task ExecutePragmasAsync(
        DbCommand command,
        RevisionIngestionPersistenceOptimizationOptions options,
        CancellationToken cancellationToken)
    {
        var synchronous = ResolveSynchronousMode(options.SqliteSynchronous);

        // WAL + NORMAL provides high-throughput ingestion with strong durability; override to OFF in development only.
        await ExecutePragmaAsync(command, "journal_mode=WAL", cancellationToken);
        await ExecutePragmaAsync(command, $"synchronous={synchronous}", cancellationToken);

        if (options.SqliteTempStoreMemory)
        {
            await ExecutePragmaAsync(command, "temp_store=MEMORY", cancellationToken);
        }

        if (options.SqliteCacheSizeKb > 0)
        {
            await ExecutePragmaAsync(command, $"cache_size=-{options.SqliteCacheSizeKb}", cancellationToken);
        }
    }

    private static void ExecutePragma(DbCommand command, string pragma)
    {
        command.CommandText = $"PRAGMA {pragma};";
        command.ExecuteNonQuery();
    }

    private static async Task ExecutePragmaAsync(
        DbCommand command,
        string pragma,
        CancellationToken cancellationToken)
    {
        command.CommandText = $"PRAGMA {pragma};";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string ResolveSynchronousMode(string? configuredMode)
    {
        if (string.IsNullOrWhiteSpace(configuredMode))
        {
            return "NORMAL";
        }

        if (string.Equals(configuredMode, "OFF", StringComparison.OrdinalIgnoreCase))
        {
            if (_isDevelopment)
            {
                return "OFF";
            }

            _logger.LogError("SqliteSynchronous=OFF is only permitted in Development environment. Overriding to NORMAL for safety.");
            return "NORMAL";
        }

        if (string.Equals(configuredMode, "NORMAL", StringComparison.OrdinalIgnoreCase))
        {
            return "NORMAL";
        }

        _logger.LogWarning("SqliteSynchronous={ConfiguredMode} is invalid. Falling back to NORMAL.", configuredMode);
        return "NORMAL";
    }
}
