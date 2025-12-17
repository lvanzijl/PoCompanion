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

// Configure database: prefer SqlServer if connection string present, otherwise SQLite
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
            // during development allow any origin to simplify local testing
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
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

// Ensure database is created/migrated. If migrations are not present, fall back to EnsureCreated.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
    try
    {
        // Try apply migrations if any
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        // If migrations are not available or migration application fails, fallback to EnsureCreated
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        logger.LogWarning(ex, "EF migrations could not be applied; falling back to EnsureCreated. Create migrations locally using 'dotnet ef migrations add <Name> --project PoTool.Api --startup-project PoTool.Api'");
        await db.Database.EnsureCreatedAsync();
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Only enable HTTPS redirection when an HTTPS URL is configured.
// This allows running an HTTP-only debug profile without requiring a dev certificate.
var hasHttpsEndpoint = app.Urls.Any(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    || (builder.Configuration["ASPNETCORE_URLS"]?.Contains("https://", StringComparison.OrdinalIgnoreCase) ?? false);

if (hasHttpsEndpoint)
{
    app.UseHttpsRedirection();
}

// Add routing so middleware such as CORS apply to endpoints including SignalR
app.UseRouting();

app.UseCors("AllowBlazorClient");

app.MapControllers();
app.MapHub<WorkItemHub>("/hubs/workitems");

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

app.MapPost("/api/workitems/sync", async (WorkItemSyncService svc, CancellationToken ct) =>
{
    await svc.TriggerSyncAsync("DefaultAreaPath", ct);
    return Results.Ok();
});

app.Run();

public record TfsConfigRequest(string? Url, string? Project, string? Pat);
