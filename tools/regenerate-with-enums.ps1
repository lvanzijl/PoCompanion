#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Regenerates OpenAPI spec and API client with proper enum handling
.DESCRIPTION
    This script temporarily removes the Client reference from API, generates OpenAPI,
    then regenerates the client with proper enum types.
#>

$ErrorActionPreference = "Stop"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "OpenAPI + Client Regeneration with Enum Support" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$repoRoot = $PSScriptRoot + "/.."
$apiCsproj = "$repoRoot/PoTool.Api/PoTool.Api.csproj"
$clientCsproj = "$repoRoot/PoTool.Client/PoTool.Client.csproj"

# Step 1: Backup and modify API csproj to remove Client reference
Write-Host "[1/6] Backing up API project file..." -ForegroundColor Yellow
Copy-Item $apiCsproj "$apiCsproj.bak"

Write-Host "      Removing Client reference from API..." -ForegroundColor Yellow
$apiContent = Get-Content $apiCsproj -Raw
$apiContent = $apiContent -replace '<ProjectReference Include="\.\.\\PoTool\.Client\\PoTool\.Client\.csproj" />', '<!-- Temporarily removed for OpenAPI generation -->'
Set-Content -Path $apiCsproj -Value $apiContent

try {
    # Step 2: Restore and build API
    Write-Host "[2/6] Restoring API dependencies..." -ForegroundColor Yellow
    dotnet restore "$repoRoot/PoTool.Api/PoTool.Api.csproj" 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Restore failed"
    }

    Write-Host "[3/6] Building API..." -ForegroundColor Yellow
    dotnet build "$repoRoot/PoTool.Api/PoTool.Api.csproj" --configuration Release --no-restore 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: API build failed" -ForegroundColor Red
        throw "Build failed"
    }
    Write-Host "      ✓ API built successfully" -ForegroundColor Green

    # Step 3: Generate OpenAPI using the existing script
    Write-Host "[4/6] Generating OpenAPI spec..." -ForegroundColor Yellow
    & "$PSScriptRoot/generate-openapi.ps1"
    if ($LASTEXITCODE -ne 0) {
        throw "OpenAPI generation failed"
    }

} finally {
    # Step 4: Restore original API csproj
    Write-Host "[5/6] Restoring API project file..." -ForegroundColor Yellow
    Move-Item "$apiCsproj.bak" $apiCsproj -Force
}

# Step 5: Regenerate client
Write-Host "[6/6] Regenerating API client..." -ForegroundColor Yellow
Push-Location "$repoRoot/PoTool.Client"
try {
    dotnet nswag run nswag.json 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "NSwag generation failed"
    }
    Write-Host "      ✓ Client regenerated successfully" -ForegroundColor Green
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "✓ Regeneration complete!" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan
