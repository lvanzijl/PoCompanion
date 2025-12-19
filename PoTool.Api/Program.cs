using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Hubs;
using PoTool.Api.Persistence;
using PoTool.Api.Repositories;
using PoTool.Api.Services;
using PoTool.Core.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Add OpenAPI/Swagger support with NSwag
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "PoTool API";
    config.Version = "v1";
    config.Description = "API for PO Companion work item management";
});

// Add Mediator (source-generated)
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
});

// Configure database: prefer SqlServer if connection string present, otherwise SQLite
// Skip database registration in Testing environment - integration tests will configure their own
if (builder.Environment.EnvironmentName != "Testing")
{
    var sqlServerConn = builder.Configuration.GetConnectionString("SqlServerConnection");
    if (!string.IsNullOrWhiteSpace(sqlServerConn))
    {
        builder.Services.AddDbContext<PoToolDbContext>(options =>
            options.UseSqlServer(sqlServerConn));
    }
    else
    {
        builder.Services.AddDbContext<PoToolDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=potool.db"));
    }
}

// Register repositories
if (builder.Environment.IsDevelopment())
{
    // Use in-memory dev repository to allow frontend development without TFS or DB
    builder.Services.AddSingleton<IWorkItemRepository, DevWorkItemRepository>();
}
else
{
    builder.Services.AddScoped<IWorkItemRepository, WorkItemRepository>();
}
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();

// Register mock data provider
builder.Services.AddSingleton<MockDataProvider>();

// Register TFS configuration and client
builder.Services.AddDataProtection();
builder.Services.AddScoped<TfsConfigurationService>();
builder.Services.AddHttpClient<ITfsClient, TfsClient>();

// Register background services
builder.Services.AddSingleton<WorkItemSyncService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<WorkItemSyncService>());

// Add SignalR
builder.Services.AddSignalR();

// Add CORS for Blazor client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // In development, restrict to known local origins and enable credentials for SignalR
            policy.WithOrigins(
                    "https://localhost:5001",
                    "http://localhost:5000",
                    "http://localhost:5291"  // Allow API self-reference for Swagger UI
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy.WithOrigins("https://localhost:5001", "http://localhost:5000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

var app = builder.Build();

// Ensure database is created/migrated
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    
    // Skip migration logic for in-memory databases (testing)
    var isInMemory = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
    if (isInMemory)
    {
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        try
        {
            // Check for legacy database before attempting migrations
            var canConnect = await db.Database.CanConnectAsync();
            if (canConnect)
            {
                var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
                var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
                
                // Detect legacy database: has tables but no migration history
                if (!appliedMigrations.Any() && pendingMigrations.Any())
                {
                    var connection = db.Database.GetDbConnection();
                    try
                    {
                        await connection.OpenAsync();
                        
                        // Check if TfsConfigs or WorkItems tables exist (legacy database indicators)
                        using var checkCmd = connection.CreateCommand();
                        checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('TfsConfigs', 'WorkItems')";
                        var legacyTableCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                        
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
                        await connection.CloseAsync();
                    }
                }
            }
            
            // Try apply migrations
            await db.Database.MigrateAsync();
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
            await db.Database.EnsureCreatedAsync();
        }
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    // Enable Swagger UI for development at /swagger
    app.UseOpenApi();
    app.UseSwaggerUi();
    
    // Enable WebAssembly debugging in development
    app.UseWebAssemblyDebugging();
}
else
{
    // In production, use exception handler
    app.UseExceptionHandler("/Error");
}

// Only enable HTTPS redirection when an HTTPS URL is configured.
// This allows running an HTTP-only debug profile without requiring a dev certificate.
var hasHttpsEndpoint = app.Urls.Any(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    || (builder.Configuration["ASPNETCORE_URLS"]?.Contains("https://", StringComparison.OrdinalIgnoreCase) ?? false);

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

// Fallback to serve index.html for Blazor client-side routing
app.MapFallbackToFile("index.html");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

// Add minimal endpoints to manage TFS config from client
app.MapGet("/api/tfsconfig", async (TfsConfigurationService svc) =>
{
    var cfg = await svc.GetConfigAsync();
    if (cfg == null) return Results.NoContent();
    return Results.Ok(new { cfg.Url, cfg.Project });
});

app.MapPost("/api/tfsconfig", async (TfsConfigurationService svc, TfsConfigRequest req) =>
{
    await svc.SaveConfigAsync(req.Url ?? string.Empty, req.Project ?? string.Empty, req.Pat ?? string.Empty);
    return Results.Ok();
});

app.MapGet("/api/tfsvalidate", async (ITfsClient client) =>
{
    var ok = await client.ValidateConnectionAsync();
    return ok ? Results.Ok() : Results.StatusCode(500);
});

app.MapPost("/api/workitems/sync", async (IMediator mediator, CancellationToken ct) =>
{
    await mediator.Send(new PoTool.Core.WorkItems.Commands.SyncWorkItemsCommand("DefaultAreaPath"), ct);
    return Results.Ok();
});

app.Run();

public record TfsConfigRequest(string? Url, string? Project, string? Pat);
