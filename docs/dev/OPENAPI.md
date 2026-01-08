# OpenAPI Specification Generation

This document describes how to generate the OpenAPI specification for the PoTool API.

## Overview

The PoTool API uses NSwag to generate OpenAPI/Swagger documentation. In development mode, the API automatically exposes:
- OpenAPI JSON: `http://localhost:5291/openapi/v1.json`
- Swagger UI: `http://localhost:5291/swagger`

## Prerequisites

- .NET 10 SDK installed
- PoTool.Api project built and ready to run

## Manual Generation (Step-by-Step)

### Step 1: Start the API

From the repository root:

```powershell
cd PoTool.Api
dotnet run --urls "http://localhost:5291"
```

Wait for the API to start. You should see:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5291
```

### Step 2: Download the OpenAPI specification

In a separate terminal/PowerShell window:

```powershell
curl http://localhost:5291/openapi/v1.json -o PoTool.Client/openapi.json
```

Or use the automated script (see below).

### Step 3: Stop the API

Press `Ctrl+C` in the terminal running the API.

## Automated Generation (Recommended)

Use the provided PowerShell script from the repository root:

```powershell
.\tools\generate-openapi.ps1
```

This script will:
1. Build the PoTool.Api project
2. Start the API on port 5291
3. Wait for it to be ready
4. Download the OpenAPI spec to `PoTool.Client/openapi.json`
5. Stop the API

## Output

The OpenAPI specification is saved to:
```
PoTool.Client/openapi.json
```

This file is used by NSwag to generate the C# API client. See [NSWAG.md](./NSWAG.md) for client generation instructions.

## Troubleshooting

### Port already in use

If port 5291 is already in use, stop any running instances of PoTool.Api:

```powershell
# Windows
Get-Process -Name "PoTool.Api" | Stop-Process

# Linux/Mac
pkill -f PoTool.Api
```

### API doesn't start

Check that you can build the project:

```powershell
dotnet build PoTool.Api/PoTool.Api.csproj
```

Fix any build errors before attempting to generate OpenAPI.

### OpenAPI endpoint returns 404

Ensure you're running in Development mode. The OpenAPI endpoints are only available in development. Check your `ASPNETCORE_ENVIRONMENT` variable:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project PoTool.Api
```
