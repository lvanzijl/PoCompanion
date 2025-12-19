using PoTool.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Determine if we're in a testing environment
var isTesting = builder.Environment.IsEnvironment("Testing");

// Add all PoTool API services with optional test database configuration
if (isTesting)
{
    // In testing, don't configure the database here - let the test framework handle it
    builder.Services.AddPoToolApiServices(builder.Configuration, builder.Environment.IsDevelopment(), 
        configureDatabase: (services, config) => 
        {
            // Database configuration will be provided by test framework
            // Do nothing here to allow test to configure it
        });
}
else
{
    // Normal configuration for production/development
    builder.Services.AddPoToolApiServices(builder.Configuration, builder.Environment.IsDevelopment());
}

var app = builder.Build();

// Configure the API middleware pipeline
app.ConfigurePoToolApi(app.Environment.IsDevelopment());

app.Run();

// Partial class for testing
public partial class Program { }

