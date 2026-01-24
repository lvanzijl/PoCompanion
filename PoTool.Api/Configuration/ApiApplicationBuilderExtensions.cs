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

                        // Detect legacy database: has tables but no migration history
                        if (!appliedMigrations.Any() && pendingMigrations.Any())
                        {
                            var connection = db.Database.GetDbConnection();
                            try
                            {
                                connection.OpenAsync().GetAwaiter().GetResult();

                                // Check if TfsConfigs or WorkItems tables exist (legacy database indicators)
                                using var checkCmd = connection.CreateCommand();
                                checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('TfsConfigs', 'WorkItems')";
                                var legacyTableCount = Convert.ToInt32(checkCmd.ExecuteScalarAsync().GetAwaiter().GetResult());

                                if (legacyTableCount > 0)
                                {
                                    // Legacy database detected - provide clear error message
                                    var dbPath = connection.DataSource;

                                    // Get the full absolute path for the database file
                                    var fullDbPath = Path.GetFullPath(dbPath);

                                    // Detect OS and provide appropriate delete command
                                    var isWindows = OperatingSystem.IsWindows();
                                    var deleteCommand = isWindows
                                        ? $"del \"{fullDbPath}\""
                                        : $"rm \"{fullDbPath}\"";
                                    var osLabel = isWindows ? "Windows" : "Linux/Mac";

                                    logger.LogCritical("LEGACY DATABASE DETECTED: Cannot start application with incompatible database schema.");
                                    logger.LogCritical("Database location: {DatabasePath}", fullDbPath);
                                    logger.LogCritical("To fix this issue, delete the database file and restart the application:");
                                    logger.LogCritical("  Command ({OS}): {DeleteCommand}", osLabel, deleteCommand);

                                    throw new InvalidOperationException(
                                        $"Legacy database detected at '{fullDbPath}'. " +
                                        $"The database schema is incompatible with the current version. " +
                                        $"Please delete the database file using: {deleteCommand}. " +
                                        $"A new database with the correct schema will be created automatically on restart.");
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
                // Re-throw legacy database exceptions
                throw;
            }
            catch (Exception ex)
            {
                // If migrations fail for other reasons, fallback to EnsureCreated
                logger.LogWarning(ex, "EF migrations could not be applied; falling back to EnsureCreated. Create migrations locally using 'dotnet ef migrations add <Name> --project PoTool.Api --startup-project PoTool.Api'");
                db.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
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

        app.MapControllers();

        // Map SignalR hub for cache sync progress updates
        app.MapHub<CacheSyncHub>("/hubs/cachesync");

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

        // Combined save, test, and verify with streaming progress
        app.MapPost("/api/tfsconfig/save-and-verify", async (
            TfsConfigurationService configService,
            ITfsClient client,
            TfsConfigRequest req,
            HttpResponse response,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            response.ContentType = "application/json";
            response.Headers.Append("Cache-Control", "no-cache");
            response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering

            var writer = new StreamWriter(response.Body);

            try
            {
                // Phase 1: Save configuration
                await WriteProgressAsync(writer, "Saving Configuration", ProgressState.Running, "Saving TFS configuration...", 10, null, ct);
                // DefaultAreaPath is now derived from Project name, but accept parameter for backward compatibility
                await configService.SaveConfigAsync(
                    req.Url ?? string.Empty,
                    req.Project ?? string.Empty,
                    req.DefaultAreaPath ?? string.Empty, // Ignored - derived from Project
                    req.UseDefaultCredentials,
                    req.TimeoutSeconds,
                    req.ApiVersion ?? "7.0",
                    ct);
                await WriteProgressAsync(writer, "Saving Configuration", ProgressState.Succeeded, "Configuration saved successfully", 20, null, ct);

                // Phase 2: Test connection
                await WriteProgressAsync(writer, "Testing Connection", ProgressState.Running, "Validating TFS connection...", 30, null, ct);
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
                    await WriteProgressAsync(writer, "Testing Connection", ProgressState.Succeeded, "Connection validated successfully", 40, null, ct);
                }
                else
                {
                    await WriteProgressAsync(writer, "Testing Connection", ProgressState.Failed, "Connection validation failed", 40, "The TFS server did not respond successfully", ct);
                    return;
                }

                // Phase 3: Verify TFS API capabilities
                await WriteProgressAsync(writer, "Verifying API", ProgressState.Running, "Running TFS API capability checks...", 50, null, ct);
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
                    await WriteProgressAsync(
                        writer,
                        $"Verifying API - {check.CapabilityId}",
                        checkState,
                        check.Success ? $"✓ {check.CapabilityId}" : $"✗ {check.CapabilityId}",
                        progressPercent,
                        errorDetails,
                        ct);
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
                    await WriteProgressAsync(writer, "Verifying API", ProgressState.Succeeded, $"All {report.Checks.Count} API checks passed", 90, null, ct);
                }
                else
                {
                    var failedCount = report.Checks.Count(c => !c.Success);
                    await WriteProgressAsync(
                        writer,
                        "Verifying API",
                        ProgressState.Failed,
                        $"{failedCount} of {report.Checks.Count} API checks failed",
                        90,
                        report.Summary,
                        ct);
                }

                // Phase 4: Complete
                await WriteProgressAsync(writer, "Complete", ProgressState.Succeeded, "TFS configuration and verification complete", 100, null, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during save and verify operation");
                await WriteProgressAsync(writer, "Error", ProgressState.Failed, "An error occurred", null, ex.Message, ct);
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
