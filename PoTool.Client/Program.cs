using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PoTool.Client.Services;
using PoTool.Shared.Interfaces;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Get API base URL from configuration or environment
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;

// Ensure the API base URL ends with a slash
if (!apiBaseUrl.EndsWith("/"))
{
    apiBaseUrl += "/";
}

// Configure HttpClient with API base address (no handler - WASM compatible)
builder.Services.AddScoped(sp =>
{
    var client = new HttpClient
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
    return client;
});

// Register services
builder.Services.AddScoped<ICharacterClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new CharacterClient(httpClient);
});

builder.Services.AddScoped<IItemClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new ItemClient(httpClient);
});

builder.Services.AddScoped<ISkillTreeClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new SkillTreeClient(httpClient);
});

builder.Services.AddScoped<IPassiveTreeClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new PassiveTreeClient(httpClient);
});

builder.Services.AddScoped<IBuildClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new BuildClient(httpClient);
});

builder.Services.AddScoped<IEquipmentClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new EquipmentClient(httpClient);
});

builder.Services.AddScoped<IGemClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new GemClient(httpClient);
});

builder.Services.AddScoped<IFlaskClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new FlaskClient(httpClient);
});

builder.Services.AddScoped<IDefenseClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new DefenseClient(httpClient);
});

builder.Services.AddScoped<IHealthCalculationClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new HealthCalculationClient(httpClient);
});

builder.Services.AddScoped<IMetricsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new MetricsClient(httpClient);
});

await builder.Build().RunAsync();
