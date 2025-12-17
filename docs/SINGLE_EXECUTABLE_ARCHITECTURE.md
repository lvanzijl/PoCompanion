# Single Executable Architecture

## Overzicht

PoCompanion is geconfigureerd als een **single executable application** waarbij zowel het ASP.NET Core Web API backend als de Blazor WebAssembly frontend vanuit dezelfde executable draaien.

## Architectuur

```
┌─────────────────────────────────────────────┐
│         PoTool.Api (Executable)             │
│                                             │
│  ┌──────────────────────────────────────┐  │
│  │  ASP.NET Core Web API                │  │
│  │  - Controllers                        │  │
│  │  - SignalR Hubs                       │  │
│  │  - Mediator Handlers                  │  │
│  │  - Entity Framework                   │  │
│  │  - Background Services                │  │
│  └──────────────────────────────────────┘  │
│                                             │
│  ┌──────────────────────────────────────┐  │
│  │  Static File Server                   │  │
│  │  - Serves Blazor WebAssembly          │  │
│  │  - wwwroot from PoTool.Client         │  │
│  │  - .NET runtime (WebAssembly)         │  │
│  │  - Framework files                    │  │
│  └──────────────────────────────────────┘  │
│                                             │
│         Runs on: http://localhost:5291      │
└─────────────────────────────────────────────┘

        Browser connects to: http://localhost:5291
                    ↓
        ┌───────────────────────────┐
        │  Blazor WebAssembly UI    │
        │  (Runs in Browser)        │
        │                           │
        │  ↓ API Calls              │
        │  → /api/workitems         │
        │  → /hubs/workitems        │
        │  ← JSON responses         │
        └───────────────────────────┘
```

## Voordelen

### 1. **Eenvoudige Deployment**
- Slechts 1 executable om te deployen
- Geen aparte hosting nodig voor frontend
- Geen CORS complexiteit in productie

### 2. **Gecentraliseerd Beheer**
- 1 configuratie bestand
- 1 logging endpoint
- 1 health check endpoint

### 3. **Ontwikkeling**
- 1 commando om hele applicatie te starten
- Automatische rebuild van frontend bij wijzigingen
- Blazor debugging werkt out-of-the-box

### 4. **Communicatie**
- Backend en frontend op zelfde origin
- SignalR werkt zonder extra configuratie
- Geen CORS problemen tijdens development

## Implementatie Details

### PoTool.Api.csproj

```xml
<ItemGroup>
  <!-- Blazor WebAssembly Server hosting package -->
  <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Server" Version="10.0.1" />
</ItemGroup>

<ItemGroup>
  <!-- Reference to client project to serve its output -->
  <ProjectReference Include="..\PoTool.Client\PoTool.Client.csproj" />
</ItemGroup>
```

### Program.cs Configuratie

```csharp
// Development: Enable Blazor WebAssembly debugging
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}

// Serve Blazor WebAssembly static files
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// API endpoints (before fallback!)
app.MapControllers();
app.MapHub<WorkItemHub>("/hubs/workitems");

// Fallback to serve index.html for client-side routing
app.MapFallbackToFile("index.html");
```

### PoTool.Client Program.cs

```csharp
// When hosted by API server, use the host's base address
// When running standalone (dev), use configured API address
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] 
    ?? builder.HostEnvironment.BaseAddress;
    
builder.Services.AddScoped(sp => 
    new HttpClient { BaseAddress = new Uri(apiBaseAddress) });
```

## Development Workflow

### Applicatie Starten

```bash
# Start de hele applicatie (API + Frontend)
cd PoTool.Api
dotnet run

# Of met watch voor hot reload
dotnet watch run
```

De applicatie is beschikbaar op: **http://localhost:5291**

### URL Routes

| Route | Beschrijving |
|-------|-------------|
| `/` | Blazor WebAssembly UI (index.html) |
| `/api/*` | REST API endpoints |
| `/hubs/workitems` | SignalR Hub |
| `/swagger` | Swagger UI (development only) |
| `/health` | Health check endpoint |

### Client-Side Routing

Blazor's client-side router handelt alle routes af:
- `/` → Home page
- `/workitems` → Work Items explorer
- `/tfsconfig` → TFS Configuration

De `MapFallbackToFile("index.html")` zorgt ervoor dat elke route die niet een API endpoint is, de Blazor app laadt.

## Build Process

### Bij Build van PoTool.Api:

1. **PoTool.Client wordt automatisch gebuild**
   - Blazor WebAssembly output → `bin/Debug/net10.0/wwwroot`
   - Bevat: index.html, _framework/, CSS, JS

2. **Client output wordt gekopieerd naar Api**
   - Via `UseBlazorFrameworkFiles()` middleware
   - Static files worden geserveerd vanaf `/`

3. **Api executable bevat alles**
   - Backend code
   - Frontend static files
   - .NET WebAssembly runtime

## Deployment

### Development

```bash
dotnet run --project PoTool.Api
```

### Production

```bash
# Publish als self-contained executable
dotnet publish PoTool.Api -c Release -o ./publish

# Run de executable
./publish/PoTool.Api
```

Of als single-file executable:

```bash
dotnet publish PoTool.Api -c Release -o ./publish \
  /p:PublishSingleFile=true \
  /p:SelfContained=true \
  /p:RuntimeIdentifier=win-x64

# Resulteert in: publish/PoTool.Api.exe (1 bestand!)
```

## Debugging

### Backend Debugging
- Normale .NET debugging in VS Code / Visual Studio
- Breakpoints in controllers, services, handlers

### Frontend Debugging
- Chrome/Edge DevTools voor Blazor debugging
- In development mode: `app.UseWebAssemblyDebugging()`
- Source maps beschikbaar voor C# debugging in browser

### SignalR Debugging
- Console logging in `WorkItemSyncHubService`
- Browser network tab → WS (WebSocket) traffic
- Server-side logging in `WorkItemHub`

## Configuratie

### appsettings.json (PoTool.Api)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=potool.db"
  }
}
```

### wwwroot/appsettings.json (PoTool.Client)

```json
{
  "ApiBaseAddress": null
}
```

**Note:** `ApiBaseAddress` is `null` omdat de client door de API wordt gehost. De `BaseAddress` wordt automatisch de API's address.

## Alternatieve Configuraties

### Standalone Frontend (Development)

Als je de frontend apart wilt draaien (bijvoorbeeld voor snellere rebuilds):

1. **Start API:**
   ```bash
   cd PoTool.Api
   dotnet run
   ```

2. **Start Client (in nieuwe terminal):**
   ```bash
   cd PoTool.Client
   dotnet run
   ```

3. **Update CORS in Program.cs** om Client origin toe te staan

**Nadeel:** Complexere setup, CORS configuratie nodig

### Microservices Architectuur

Voor grotere applicaties kan je overwegen:
- Aparte API service
- Aparte Frontend hosting (CDN, Static Web Apps)
- API Gateway
- Service discovery

**Nadeel:** Veel complexer, alleen nodig voor zeer grote schaal

## Best Practices

### ✅ DO

- Gebruik deze single executable architectuur voor MVP en kleine tot middelgrote applicaties
- Test de API endpoints via Swagger UI op `/swagger`
- Gebruik één `BaseAddress` voor alle HTTP calls
- Implementeer health checks voor monitoring

### ❌ DON'T

- Vermijd hardcoded URLs in client code
- Gebruik geen `AllowAnyOrigin()` in CORS (security risk)
- Host geen grote binaire bestanden in wwwroot (database limits)
- Maak geen tight coupling tussen API en Client code

## Monitoring & Health Checks

### Health Check Endpoint

```
GET /health

Response:
{
  "Status": "Healthy",
  "Timestamp": "2025-12-17T20:00:00Z"
}
```

### Performance Monitoring

Monitor deze metrics:
- API response times (via logging)
- SignalR connection count
- Database query performance
- Static file serving performance
- Blazor WebAssembly load time

## Troubleshooting

### Issue: 404 op client-side routes

**Probleem:** Directe navigatie naar `/workitems` geeft 404

**Oplossing:** Zorg dat `MapFallbackToFile("index.html")` **na** alle API endpoints staat

### Issue: SignalR verbinding faalt

**Probleem:** SignalR kan geen verbinding maken

**Oplossing:** 
1. Check CORS policy: `AllowCredentials()` moet aanstaan
2. Verify hub URL: `_apiBase + "/hubs/workitems"`
3. Check browser console voor errors

### Issue: Static files niet gevonden

**Probleem:** CSS/JS bestanden laden niet

**Oplossing:**
1. Verify `UseBlazorFrameworkFiles()` is geconfigureerd
2. Check `UseStaticFiles()` staat na framework files
3. Rebuild Client project

### Issue: API calls falen vanuit Blazor

**Probleem:** CORS errors of connection refused

**Oplossing:**
1. Check `HttpClient.BaseAddress` configuratie
2. Verify API is running
3. Check browser network tab voor exacte URL

## Conclusie

De single executable architectuur van PoCompanion biedt:
- ✅ Eenvoudige development
- ✅ Eenvoudige deployment
- ✅ Goede performance
- ✅ Geschikt voor MVP en mid-size applicaties
- ✅ Duidelijke scheiding frontend/backend via API contract
- ✅ Modern tech stack (Blazor WebAssembly + ASP.NET Core)

Voor grotere applicaties kan later worden opgeschaald naar microservices als dat nodig is.
