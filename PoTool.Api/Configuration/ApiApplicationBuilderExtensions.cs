using System.Text.Json;
using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Hubs;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.Contracts;
using PoTool.Shared.Contracts.TfsVerification;

namespace PoTool.Api.Configuration;

/// <summary>
/// Extension methods for configuring the API application pipeline.
/// </summary>
public static class ApiApplicationBuilderExtensions
{
    // Provider name constant for InMemory database
    private const string InMemoryProviderName = "Microsoft.EntityFrameworkCore.InMemory";

    /// <summary>
    /// Configures the PoTool API middleware pipeline.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="isDevelopment">Whether the application is running in development mode.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication ConfigurePoToolApi(this WebApplication app, bool isDevelopment)
    {
        // Ensure database is created/migrated
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

            try
            {
                // Check if we're using InMemory database (for testing)
                var isInMemory = db.Database.ProviderName == InMemoryProviderName;

                if (isInMemory)
                {
                    // For InMemory database, just ensure it's created
                    logger.LogInformation("Using InMemory database for testing");
                    db.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
                }
                else
                {
                    // For relational databases (SQLite, SQL Server), handle migrations and legacy detection
                    var canConnect = db.Database.CanConnectAsync().GetAwaiter().GetResult();
                    if (canConnect)
                    {
                        var appliedMigrations = db.Database.GetAppliedMigrationsAsync().GetAwaiter().GetResult();
                        var pendingMigrations = db.Database.GetPendingMigrationsAsync().GetAwaiter().GetResult();

                        // Detect database created with EnsureCreated: has tables but no migration history
                        if (!appliedMigrations.Any() && pendingMigrations.Any())
                        {
                            var connection = db.Database.GetDbConnection();
                            try
                            {
                                connection.OpenAsync().GetAwaiter().GetResult();

                                // Check if any tables exist (indicates database was created with EnsureCreated)
                                // Use provider-specific SQL to check for tables
                                bool hasTables = false;
                                using var checkCmd = connection.CreateCommand();
                                
                                if (db.Database.IsSqlite())
                                {
                                    checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
                                    var tableCount = Convert.ToInt32(checkCmd.ExecuteScalarAsync().GetAwaiter().GetResult());
                                    hasTables = tableCount > 0;
                                }
                                else if (db.Database.IsSqlServer())
                                {
                                    checkCmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
                                    var tableCount = Convert.ToInt32(checkCmd.ExecuteScalarAsync().GetAwaiter().GetResult());
                                    hasTables = tableCount > 0;
                                }

                                if (hasTables)
                                {
                                    // Database has tables but no migration history - was created with EnsureCreated
                                    var dbPath = connection.DataSource;

                                    // Get appropriate instructions based on database provider
                                    string deleteInstructions;
                                    if (db.Database.IsSqlite())
                                    {
                                        // For SQLite, provide file deletion command
                                        var fullDbPath = Path.GetFullPath(dbPath);
                                        var isWindows = OperatingSystem.IsWindows();
                                        var deleteCommand = isWindows
                                            ? $"del \"{fullDbPath}\""
                                            : $"rm \"{fullDbPath}\"";
                                        var osLabel = isWindows ? "Windows" : "Linux/Mac";
                                        
                                        deleteInstructions = $"Command ({osLabel}): {deleteCommand}";
                                        logger.LogCritical("Database location: {DatabasePath}", fullDbPath);
                                    }
                                    else
                                    {
                                        // For SQL Server, provide DROP DATABASE command
                                        var dbName = connection.Database;
                                        deleteInstructions = $"DROP DATABASE [{dbName}] (must be executed by a database administrator)";
                                        logger.LogCritical("Database name: {DatabaseName} on server: {ServerName}", dbName, dbPath);
                                    }

                                    logger.LogCritical("INCOMPATIBLE DATABASE DETECTED: Database was created without migrations.");
                                    logger.LogCritical("This usually happens when the database was created with EnsureCreated() instead of migrations.");
                                    logger.LogCritical("To fix this issue, delete the database and restart the application:");
                                    logger.LogCritical("  {DeleteInstructions}", deleteInstructions);

                                    throw new InvalidOperationException(
                                        $"Database was created without migration history. " +
                                        $"This can cause migration failures when trying to apply schema changes. " +
                                        $"Please delete the database and restart. " +
                                        $"A new database with proper migration tracking will be created automatically on restart.");
                                }
                            }
                            finally
                            {
                                connection.CloseAsync().GetAwaiter().GetResult();
                            }
                        }
                    }

                    // Try apply migrations
                    db.Database.MigrateAsync().GetAwaiter().GetResult();
                }
            }
            catch (InvalidOperationException)
            {
                // Re-throw database compatibility exceptions
                throw;
            }
            catch (Exception ex)
            {
                // Log migration failures and re-throw for visibility
                // Do NOT fallback to EnsureCreated as it creates databases without migration history
                // which will cause failures on subsequent runs
                logger.LogCritical(ex, "EF migrations could not be applied. This may indicate a corrupt database or migration issue.");
                logger.LogCritical("If the database exists, try deleting it and restarting the application.");
                logger.LogCritical("To create migrations locally, use: dotnet ef migrations add <Name> --project PoTool.Api --startup-project PoTool.Api");
                throw;
            }
        }

        // Configure the HTTP request pipeline
        if (isDevelopment)
        {
            app.MapOpenApi();

            // Enable Swagger UI for development at /swagger
            app.UseOpenApi();
            app.UseSwaggerUi();
        }
        else
        {
            // In production, use exception handler
            app.UseExceptionHandler("/Error");
        }

        // Only enable HTTPS redirection when an HTTPS URL is configured.
        // This allows running an HTTP-only debug profile without requiring a dev certificate.
        var hasHttpsEndpoint = app.Urls.Any(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            || (app.Configuration["ASPNETCORE_URLS"]?.Contains("https://", StringComparison.OrdinalIgnoreCase) ?? false);

        if (hasHttpsEndpoint)
        {
            app.UseHttpsRedirection();
        }

        // Serve Blazor WebAssembly static files
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();

        // Add routing so middleware such as CORS apply to endpoints including SignalR
        app.UseRouting();

        app.UseCors("AllowBlazorClient");

        // Add DataSourceMode middleware to set Cache/Live mode based on route and ProductOwner cache state
        app.UseMiddleware<PoTool.Api.Middleware.DataSourceModeMiddleware>();

        // Add workspace guard middleware in development only (throws exception if workspace uses Live mode)
        if (isDevelopment)
        {
            app.UseMiddleware<PoTool.Api.Middleware.WorkspaceGuardMiddleware>();
        }

        app.MapControllers();

        // Map SignalR hub for cache sync progress updates
        app.MapHub<CacheSyncHub>("/hubs/cachesync");
        
        // Map SignalR hub for TFS config progress updates
        app.MapHub<TfsConfigHub>("/hubs/tfsconfig");

        // Health check endpoint
        app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

        // Add minimal endpoints to manage TFS config from client
        app.MapGet("/api/tfsconfig", async Task<IResult> (TfsConfigurationService svc) =>
        {
            var cfg = await svc.GetConfigAsync();
            if (cfg == null) return Results.NoContent();
            return TypedResults.Ok(cfg);
        })
        .Produces<TfsConfig>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status204NoContent)
        .WithName("GetTfsConfig");

        app.MapPost("/api/tfsconfig", async (TfsConfigurationService svc, TfsConfigRequest req) =>
        {
            // Authentication uses Windows credentials (NTLM) - no PAT needed
            // DefaultAreaPath is now derived from Project name, but accept parameter for backward compatibility
            await svc.SaveConfigAsync(
                req.Url ?? string.Empty,
                req.Project ?? string.Empty,
                req.DefaultAreaPath ?? string.Empty, // Ignored - derived from Project
                req.UseDefaultCredentials,
                req.TimeoutSeconds,
                req.ApiVersion ?? "7.0");
            return Results.Ok();
        });

        app.MapGet("/api/tfsvalidate", async (ITfsClient client, TfsConfigurationService configService, ILogger<Program> logger) =>
        {
            try
            {
                var ok = await client.ValidateConnectionAsync();
                if (ok)
                {
                    // Update TFS config to mark connection as tested successfully
                    var config = await configService.GetConfigEntityAsync();
                    if (config != null)
                    {
                        config.HasTestedConnectionSuccessfully = true;
                        config.LastValidated = DateTimeOffset.UtcNow;
                        await configService.SaveConfigEntityAsync(config);
                    }

                    return Results.Ok(new { success = true, message = "Connection validated successfully" });
                }
                else
                {
                    // Validation failed - return detailed error
                    logger.LogWarning("TFS validation endpoint: Connection test failed (returned false)");
                    return Results.Json(
                        new
                        {
                            success = false,
                            message = "Connection test failed",
                            details = "The TFS server did not respond successfully. Check the logs for more details about HTTP status codes and error responses."
                        },
                        statusCode: 500);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "TFS validation endpoint: Exception during connection test");
                return Results.Json(
                    new
                    {
                        success = false,
                        message = "Connection test failed with exception",
                        details = ex.Message,
                        exceptionType = ex.GetType().Name
                    },
                    statusCode: 500);
            }
        });

        app.MapPost("/api/tfsverify", async (ITfsClient client, TfsConfigurationService configService, TfsVerifyRequest req, CancellationToken ct) =>
        {
            var report = await client.VerifyCapabilitiesAsync(
                req.IncludeWriteChecks,
                req.WorkItemIdForWriteCheck,
                ct);

            // If all checks passed, mark TFS API as verified
            if (report.Success)
            {
                var config = await configService.GetConfigEntityAsync(ct);
                if (config != null)
                {
                    config.HasVerifiedTfsApiSuccessfully = true;
                    config.LastValidated = DateTimeOffset.UtcNow;
                    await configService.SaveConfigEntityAsync(config, ct);
                }
            }

            return Results.Ok(report);
        })
        .Produces<TfsVerificationReport>(StatusCodes.Status200OK);

        // Combined save, test, and verify with SignalR progress broadcasting
        app.MapPost("/api/tfsconfig/save-and-verify", async (
            TfsConfigurationService configService,
            ITfsClient client,
            ITfsConfigProgressBroadcaster progressBroadcaster,
            TfsConfigRequest req,
            HttpResponse response,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            response.ContentType = "application/json";
            response.Headers.Append("Cache-Control", "no-cache");
            response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering

            var writer = new StreamWriter(response.Body);
            
            // Helper to broadcast via SignalR AND write to HTTP stream for compatibility
            async Task BroadcastAndWriteAsync(string phase, ProgressState state, string message, int? percentComplete, string? details)
            {
                var update = new TfsConfigProgressUpdate(phase, state, message, percentComplete, details);
                await progressBroadcaster.BroadcastProgressAsync(update, ct);
                await WriteProgressAsync(writer, phase, state, message, percentComplete, details, ct);
            }

            try
            {
                // Phase 1: Save configuration
                await BroadcastAndWriteAsync("Saving Configuration", ProgressState.Running, "Saving TFS configuration...", 10, null);
                // DefaultAreaPath is now derived from Project name, but accept parameter for backward compatibility
                await configService.SaveConfigAsync(
                    req.Url ?? string.Empty,
                    req.Project ?? string.Empty,
                    req.DefaultAreaPath ?? string.Empty, // Ignored - derived from Project
                    req.UseDefaultCredentials,
                    req.TimeoutSeconds,
                    req.ApiVersion ?? "7.0",
                    ct);
                await BroadcastAndWriteAsync("Saving Configuration", ProgressState.Succeeded, "Configuration saved successfully", 20, null);

                // Phase 2: Test connection
                await BroadcastAndWriteAsync("Testing Connection", ProgressState.Running, "Validating TFS connection...", 30, null);
                var connectionOk = await client.ValidateConnectionAsync(ct);
                if (connectionOk)
                {
                    // Update config entity to mark as tested
                    var config = await configService.GetConfigEntityAsync(ct);
                    if (config != null)
                    {
                        config.HasTestedConnectionSuccessfully = true;
                        config.LastValidated = DateTimeOffset.UtcNow;
                        await configService.SaveConfigEntityAsync(config, ct);
                    }
                    await BroadcastAndWriteAsync("Testing Connection", ProgressState.Succeeded, "Connection validated successfully", 40, null);
                }
                else
                {
                    await BroadcastAndWriteAsync("Testing Connection", ProgressState.Failed, "Connection validation failed", 40, "The TFS server did not respond successfully");
                    return;
                }

                // Phase 3: Verify TFS API capabilities
                await BroadcastAndWriteAsync("Verifying API", ProgressState.Running, "Running TFS API capability checks...", 50, null);
                var report = await client.VerifyCapabilitiesAsync(false, null, ct);
                
                // Report individual check results as sub-progress
                int checkIndex = 0;
                int totalChecks = report.Checks.Count;
                foreach (var check in report.Checks)
                {
                    checkIndex++;
                    int progressPercent = 50 + (int)((checkIndex / (double)totalChecks) * 40);
                    var checkState = check.Success ? ProgressState.Succeeded : ProgressState.Failed;
                    var errorDetails = check.Success ? null : check.RawEvidence ?? check.ObservedBehavior;
                    await BroadcastAndWriteAsync(
                        $"Verifying API - {check.CapabilityId}",
                        checkState,
                        check.Success ? $"✓ {check.CapabilityId}" : $"✗ {check.CapabilityId}",
                        progressPercent,
                        errorDetails);
                }

                if (report.Success)
                {
                    // Update config entity to mark as verified
                    var config = await configService.GetConfigEntityAsync(ct);
                    if (config != null)
                    {
                        config.HasVerifiedTfsApiSuccessfully = true;
                        config.LastValidated = DateTimeOffset.UtcNow;
                        await configService.SaveConfigEntityAsync(config, ct);
                    }
                    await BroadcastAndWriteAsync("Verifying API", ProgressState.Succeeded, $"All {report.Checks.Count} API checks passed", 90, null);
                }
                else
                {
                    var failedCount = report.Checks.Count(c => !c.Success);
                    await BroadcastAndWriteAsync(
                        "Verifying API",
                        ProgressState.Failed,
                        $"{failedCount} of {report.Checks.Count} API checks failed",
                        90,
                        report.Summary);
                }

                // Phase 4: Complete
                await BroadcastAndWriteAsync("Complete", ProgressState.Succeeded, "TFS configuration and verification complete", 100, null);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during save and verify operation");
                await BroadcastAndWriteAsync("Error", ProgressState.Failed, "An error occurred", null, ex.Message);
            }
            finally
            {
                await writer.FlushAsync(ct);
            }
        });

        // Fallback to index.html for client-side routing
        app.MapFallbackToFile("index.html");

        return app;
    }

    /// <summary>
    /// Helper method to write progress updates to the HTTP response stream.
    /// </summary>
    private static async Task WriteProgressAsync(
        StreamWriter writer,
        string phase,
        ProgressState state,
        string message,
        int? percentComplete = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var progress = new
        {
            phase,
            state = state.ToString(),
            message,
            percentComplete,
            details
        };

        var json = JsonSerializer.Serialize(progress);
        await writer.WriteLineAsync(json);
        await writer.FlushAsync(cancellationToken);
    }
}

/// <summary>
/// Request model for TFS configuration endpoint.
/// </summary>
public record TfsConfigRequest(
    string? Url,
    string? Project,
    string? DefaultAreaPath,
    bool UseDefaultCredentials = true,
    int TimeoutSeconds = 30,
    string? ApiVersion = "7.0");

/// <summary>
/// Request model for TFS API verification endpoint.
/// </summary>
public record TfsVerifyRequest(bool IncludeWriteChecks, int? WorkItemIdForWriteCheck);
