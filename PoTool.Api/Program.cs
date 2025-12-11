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

// Configure database
builder.Services.AddDbContext<PoToolDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Data Source=potool.db"));

// Register repositories
builder.Services.AddScoped<IWorkItemRepository, WorkItemRepository>();

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
        policy.WithOrigins("https://localhost:5001", "http://localhost:5000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowBlazorClient");

app.MapControllers();
app.MapHub<WorkItemHub>("/hubs/workitems");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

app.Run();
