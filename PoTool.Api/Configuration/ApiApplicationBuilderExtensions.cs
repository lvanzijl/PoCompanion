using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Hubs;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;

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

        app.MapControllers();
        app.MapHub<WorkItemHub>("/hubs/workitems");

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
            await svc.SaveConfigAsync(
                req.Url ?? string.Empty, 
                req.Project ?? string.Empty, 
                req.DefaultAreaPath ?? string.Empty,
                req.UseDefaultCredentials, 
                req.TimeoutSeconds, 
                req.ApiVersion ?? "7.0");
            return Results.Ok();
        });

        app.MapGet("/api/tfsvalidate", async (ITfsClient client, ILogger<Program> logger) =>
        {
            try
            {
                var ok = await client.ValidateConnectionAsync();
                if (ok)
                {
                    return Results.Ok(new { success = true, message = "Connection validated successfully" });
                }
                else
                {
                    // Validation failed - return detailed error
                    logger.LogWarning("TFS validation endpoint: Connection test failed (returned false)");
                    return Results.Json(
                        new { 
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
                    new { 
                        success = false, 
                        message = "Connection test failed with exception", 
                        details = ex.Message,
                        exceptionType = ex.GetType().Name
                    }, 
                    statusCode: 500);
            }
        });

        app.MapPost("/api/tfsverify", async (ITfsClient client, TfsVerifyRequest req, CancellationToken ct) =>
        {
            var report = await client.VerifyCapabilitiesAsync(
                req.IncludeWriteChecks, 
                req.WorkItemIdForWriteCheck, 
                ct);
            return Results.Ok(report);
        })
        .Produces<Core.Contracts.TfsVerification.TfsVerificationReport>(StatusCodes.Status200OK);

        app.MapPost("/api/workitems/sync", async (IMediator mediator, TfsConfigurationService configService, CancellationToken ct) =>
        {
            var config = await configService.GetConfigAsync(ct);
            if (config == null || string.IsNullOrWhiteSpace(config.DefaultAreaPath))
            {
                return Results.BadRequest(new { error = "Default Area Path is not configured. Configure this in TFS settings." });
            }
            
            await mediator.Send(new PoTool.Core.WorkItems.Commands.SyncWorkItemsCommand(config.DefaultAreaPath), ct);
            return Results.Ok();
        });

        // Fallback to index.html for client-side routing
        app.MapFallbackToFile("index.html");

        return app;
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
