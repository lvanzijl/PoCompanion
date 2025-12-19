using PoTool.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add all PoTool API services
builder.Services.AddPoToolApiServices(builder.Configuration, builder.Environment.IsDevelopment());

var app = builder.Build();

// Configure the API middleware pipeline
app.ConfigurePoToolApi(app.Environment.IsDevelopment());

app.Run();

// Partial class for testing
public partial class Program { }

