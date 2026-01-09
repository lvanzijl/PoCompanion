#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates the OpenAPI specification from the running PoTool API.

.DESCRIPTION
    This script automates the OpenAPI generation process by:
    1. Building the PoTool.Api project
    2. Starting the API server on port 5291
    3. Waiting for the API to be ready
    4. Downloading the OpenAPI spec to PoTool.Client/openapi.json
    5. Stopping the API server

.EXAMPLE
    .\tools\generate-openapi.ps1
#>

param(
    [int]$Port = 5291,
    [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"

# Get the repository root (parent of tools directory)
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ApiProject = Join-Path $RepoRoot "PoTool.Api"
$OutputPath = Join-Path $RepoRoot "PoTool.Client/openapi.json"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "OpenAPI Specification Generator" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build the API project
Write-Host "[1/5] Building PoTool.Api..." -ForegroundColor Yellow
Push-Location $ApiProject
try {
    dotnet build --configuration Release --no-restore 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Build failed. Run 'dotnet build PoTool.Api' to see errors." -ForegroundColor Red
        exit 1
    }
    Write-Host "      ✓ Build successful" -ForegroundColor Green
}
finally {
    Pop-Location
}

# Step 2: Start the API
Write-Host "[2/5] Starting API server..." -ForegroundColor Yellow
$env:ASPNETCORE_ENVIRONMENT = "Development"
$ApiProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList "run", "--no-build", "--configuration", "Release", "--urls", "http://localhost:$Port" `
    -WorkingDirectory $ApiProject `
    -PassThru `
    -NoNewWindow

Write-Host "      ✓ API process started (PID: $($ApiProcess.Id))" -ForegroundColor Green

# Step 3: Wait for API to be ready
Write-Host "[3/5] Waiting for API to be ready..." -ForegroundColor Yellow
$ApiUrl = "http://localhost:$Port/health"
$OpenApiUrl = "http://localhost:$Port/openapi/v1.json"
$StartTime = Get-Date
$Ready = $false

while (((Get-Date) - $StartTime).TotalSeconds -lt $TimeoutSeconds) {
    try {
        $response = Invoke-WebRequest -Uri $ApiUrl -Method Get -TimeoutSec 2 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            $Ready = $true
            break
        }
    }
    catch {
        # Still waiting...
    }
    Start-Sleep -Milliseconds 500
}

if (-not $Ready) {
    Write-Host "      ✗ API did not become ready within $TimeoutSeconds seconds" -ForegroundColor Red
    Stop-Process -Id $ApiProcess.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

Write-Host "      ✓ API is ready" -ForegroundColor Green

# Step 4: Download OpenAPI spec
Write-Host "[4/6] Downloading OpenAPI specification..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri $OpenApiUrl -OutFile $OutputPath -ErrorAction Stop
    $FileSize = (Get-Item $OutputPath).Length
    Write-Host "      ✓ Downloaded to: $OutputPath ($FileSize bytes)" -ForegroundColor Green
}
catch {
    Write-Host "      ✗ Failed to download OpenAPI spec: $_" -ForegroundColor Red
    Stop-Process -Id $ApiProcess.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

# Step 5: Fix integer type issues
Write-Host "[5/6] Fixing integer type issues..." -ForegroundColor Yellow
& "$PSScriptRoot/fix-openapi-types.ps1"
if ($LASTEXITCODE -ne 0) {
    Write-Host "      ✗ Failed to fix OpenAPI types" -ForegroundColor Red
    Stop-Process -Id $ApiProcess.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

# Step 6: Stop the API
Write-Host "[6/6] Stopping API server..." -ForegroundColor Yellow
Stop-Process -Id $ApiProcess.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Write-Host "      ✓ API stopped" -ForegroundColor Green

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "✓ OpenAPI specification generated successfully!" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Review the spec: $OutputPath"
Write-Host "  2. Regenerate the API client: nswag run PoTool.Client/nswag.json"
Write-Host "     (See docs/dev/NSWAG.md for details)"
Write-Host ""
