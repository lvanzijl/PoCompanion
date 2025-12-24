#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts the PoCompanion application for exploratory testing with mock data.

.DESCRIPTION
    This script:
    1. Verifies .NET is installed
    2. Ensures mock data mode is enabled
    3. Starts the API server in the background
    4. Waits for API health check
    5. Provides instructions for manual testing
    6. Optionally opens browser to client URL

.PARAMETER SkipBrowser
    If specified, does not automatically open the browser

.EXAMPLE
    .\start-exploratory-testing.ps1
    .\start-exploratory-testing.ps1 -SkipBrowser
#>

param(
    [switch]$SkipBrowser
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "PoCompanion Exploratory Testing Starter" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check .NET installation
Write-Host "[1/6] Checking .NET installation..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ .NET version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ .NET is not installed or not in PATH" -ForegroundColor Red
    Write-Host "Please install .NET 10.0 SDK from https://dot.net" -ForegroundColor Red
    exit 1
}

# Check appsettings.Development.json for mock mode
Write-Host ""
Write-Host "[2/6] Verifying mock data configuration..." -ForegroundColor Yellow
$appsettingsPath = "PoTool.Api/appsettings.Development.json"
if (Test-Path $appsettingsPath) {
    $appsettings = Get-Content $appsettingsPath | ConvertFrom-Json
    if ($appsettings.TfsIntegration.UseMockClient -eq $true) {
        Write-Host "✓ Mock client is enabled in appsettings.Development.json" -ForegroundColor Green
    } else {
        Write-Host "✗ Mock client is not enabled!" -ForegroundColor Red
        Write-Host "Please set TfsIntegration.UseMockClient to true in $appsettingsPath" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "✗ appsettings.Development.json not found at $appsettingsPath" -ForegroundColor Red
    exit 1
}

# Build the solution
Write-Host ""
Write-Host "[3/6] Building solution..." -ForegroundColor Yellow
try {
    dotnet build PoTool.sln --configuration Release --no-restore --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Build successful" -ForegroundColor Green
    } else {
        Write-Host "✗ Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "✗ Build failed: $_" -ForegroundColor Red
    exit 1
}

# Start API in background
Write-Host ""
Write-Host "[4/6] Starting API server..." -ForegroundColor Yellow
$apiUrl = "http://localhost:5000"

# Kill any existing process on port 5000
$existingProcess = Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue
if ($existingProcess) {
    Write-Host "⚠ Port 5000 is already in use. Attempting to stop existing process..." -ForegroundColor Yellow
    Stop-Process -Id $existingProcess.OwningProcess -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

# Start the API
$apiJob = Start-Job -ScriptBlock {
    param($apiPath)
    Set-Location $apiPath
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    dotnet run --no-build --configuration Release --urls "http://localhost:5000"
} -ArgumentList (Get-Location).Path

Write-Host "✓ API started (Job ID: $($apiJob.Id))" -ForegroundColor Green
Write-Host "  API will be available at: $apiUrl" -ForegroundColor Gray

# Wait for API health check
Write-Host ""
Write-Host "[5/6] Waiting for API health check..." -ForegroundColor Yellow
$maxAttempts = 30
$attempt = 0
$apiHealthy = $false

while ($attempt -lt $maxAttempts) {
    $attempt++
    try {
        $response = Invoke-WebRequest -Uri "$apiUrl/health" -TimeoutSec 2 -UseBasicParsing -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            $apiHealthy = $true
            Write-Host "✓ API is healthy and responding" -ForegroundColor Green
            break
        }
    } catch {
        # Ignore and retry
    }
    Write-Host "  Attempt $attempt/$maxAttempts..." -ForegroundColor Gray
    Start-Sleep -Seconds 2
}

if (-not $apiHealthy) {
    Write-Host "✗ API health check failed after $maxAttempts attempts" -ForegroundColor Red
    Write-Host "Checking API job status..." -ForegroundColor Yellow
    Receive-Job -Job $apiJob
    Stop-Job -Job $apiJob
    Remove-Job -Job $apiJob
    exit 1
}

# Instructions for testing
Write-Host ""
Write-Host "[6/6] Ready for exploratory testing!" -ForegroundColor Yellow
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "EXPLORATORY TESTING INSTRUCTIONS" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "API Server:" -ForegroundColor Cyan
Write-Host "  URL: $apiUrl" -ForegroundColor White
Write-Host "  Status: Running (Job ID: $($apiJob.Id))" -ForegroundColor White
Write-Host ""
Write-Host "Client Application:" -ForegroundColor Cyan
Write-Host "  To start the client, open a new terminal and run:" -ForegroundColor White
Write-Host "  cd PoTool.Client" -ForegroundColor Yellow
Write-Host "  dotnet run --no-build --configuration Release" -ForegroundColor Yellow
Write-Host ""
Write-Host "  The client will be available at: http://localhost:5001" -ForegroundColor White
Write-Host ""
Write-Host "Testing Guide:" -ForegroundColor Cyan
Write-Host "  1. Navigate to http://localhost:5001 in your browser" -ForegroundColor White
Write-Host "  2. Follow the test plan in docs/EXPLORATORY_TEST_PLAN.md" -ForegroundColor White
Write-Host "  3. Capture screenshots as you test each feature" -ForegroundColor White
Write-Host "  4. Document results in docs/TEST_RESULTS.md" -ForegroundColor White
Write-Host ""
Write-Host "Features to Test:" -ForegroundColor Cyan
Write-Host "  • Home Page (landing)" -ForegroundColor White
Write-Host "  • TFS Configuration (/tfsconfig)" -ForegroundColor White
Write-Host "  • Work Items (not yet implemented - skip)" -ForegroundColor White
Write-Host "  • Backlog Health (/backlog-health)" -ForegroundColor White
Write-Host "  • Effort Distribution (/effort-distribution)" -ForegroundColor White
Write-Host "  • PR Insights (/pr-insights)" -ForegroundColor White
Write-Host "  • State Timeline (/state-timeline)" -ForegroundColor White
Write-Host "  • Epic Forecast (/epic-forecast)" -ForegroundColor White
Write-Host "  • Dependency Graph (/dependency-graph)" -ForegroundColor White
Write-Host "  • Velocity Dashboard (/velocity-dashboard)" -ForegroundColor White
Write-Host ""
Write-Host "To Stop Testing:" -ForegroundColor Cyan
Write-Host "  Press Ctrl+C to stop this script, then run:" -ForegroundColor White
Write-Host "  Stop-Job -Id $($apiJob.Id)" -ForegroundColor Yellow
Write-Host "  Remove-Job -Id $($apiJob.Id)" -ForegroundColor Yellow
Write-Host ""
Write-Host "========================================" -ForegroundColor Green

# Optionally open browser
if (-not $SkipBrowser) {
    Write-Host ""
    Write-Host "Note: Automatic browser opening requires manual client start" -ForegroundColor Yellow
    Write-Host "Please start the client as shown above and navigate to http://localhost:5001" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Press Ctrl+C to stop the API server and exit" -ForegroundColor Cyan
Write-Host ""

# Keep script running and show API logs
try {
    while ($true) {
        $jobOutput = Receive-Job -Job $apiJob -ErrorAction SilentlyContinue
        if ($jobOutput) {
            Write-Host $jobOutput
        }
        Start-Sleep -Seconds 1
    }
} finally {
    Write-Host ""
    Write-Host "Cleaning up..." -ForegroundColor Yellow
    Stop-Job -Job $apiJob -ErrorAction SilentlyContinue
    Remove-Job -Job $apiJob -ErrorAction SilentlyContinue
    Write-Host "✓ API server stopped" -ForegroundColor Green
}
