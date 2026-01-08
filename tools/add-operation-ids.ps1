#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Adds operation IDs to OpenAPI specification based on HTTP verbs and path segments.

.DESCRIPTION
    NSwag.AspNetCore doesn't automatically set operation IDs from controller action names.
    This script generates operation IDs like "GetVelocityTrend" from paths like "GET /api/Metrics/velocity"
    so that NSwag client generator can create methods named "GetVelocityTrendAsync".

.EXAMPLE
    .\tools/add-operation-ids.ps1
#>

$ErrorActionPreference = "Stop"

$openApiPath = "$PSScriptRoot/../PoTool.Client/openapi.json"

Write-Host "Adding operation IDs to OpenAPI spec..." -ForegroundColor Yellow

# Read the OpenAPI JSON
$json = Get-Content $openApiPath -Raw | ConvertFrom-Json -Depth 100

$addedCount = 0

# Function to convert path segment to PascalCase
function ConvertTo-PascalCase {
    param([string]$text)
    
    # Split on hyphens and capitalize each word
    $words = $text -split '-'
    $result = ($words | ForEach-Object { 
        if ($_.Length -gt 0) {
            $_.Substring(0,1).ToUpper() + $_.Substring(1).ToLower()
        }
    }) -join ''
    
    return $result
}

# Process each path
foreach ($pathKey in $json.paths.PSObject.Properties.Name) {
    $pathItem = $json.paths.$pathKey
    
    # Process each HTTP method
    foreach ($method in @('get', 'post', 'put', 'delete', 'patch')) {
        if ($pathItem.PSObject.Properties[$method]) {
            $operation = $pathItem.$method
            
            # Only add operation ID if not already set
            if (-not $operation.PSObject.Properties['operationId'] -or [string]::IsNullOrWhiteSpace($operation.operationId)) {
                
                # Extract meaningful parts from the path
                # e.g., "/api/Metrics/velocity" -> "Metrics", "velocity"
                # e.g., "/api/Metrics/epic-forecast/{epicId}" -> "Metrics", "epic-forecast"
                $parts = $pathKey -split '/' | Where-Object { 
                    $_ -and $_ -ne 'api' -and $_ -notmatch '^\{.*\}$' 
                }
                
                # Determine the verb based on HTTP method
                $verb = switch ($method) {
                    'get' { 'Get' }
                    'post' { 'Create' }
                    'put' { 'Update' }
                    'delete' { 'Delete' }
                    'patch' { 'Patch' }
                    default { '' }
                }
                
                # Build operation ID
                # For GET requests, use "Get" + last path segment
                # e.g., "GetVelocity", "GetEpicForecast"
                if ($parts.Count -gt 1) {
                    $lastPart = $parts[-1]
                    $pascalCase = ConvertTo-PascalCase $lastPart
                    $operationId = "$verb$pascalCase"
                }
                elseif ($parts.Count -eq 1) {
                    $pascalCase = ConvertTo-PascalCase $parts[0]
                    # If it's just the controller name, use verb + controller name
                    # e.g., "GET /api/Profiles" -> "GetProfiles"
                    $operationId = "$verb$pascalCase"
                }
                else {
                    # Fallback
                    $operationId = "$method$pathKey" -replace '[^a-zA-Z0-9]', ''
                }
                
                # Add the operation ID
                $operation | Add-Member -NotePropertyName "operationId" -NotePropertyValue $operationId -Force
                $script:addedCount++
                Write-Host "  Added: $operationId for $method $pathKey" -ForegroundColor Green
            }
        }
    }
}

if ($addedCount -gt 0) {
    # Write back the modified JSON
    $json | ConvertTo-Json -Depth 100 | Set-Content $openApiPath -Encoding UTF8
    Write-Host "✓ Added $addedCount operation IDs to openapi.json" -ForegroundColor Green
}
else {
    Write-Host "✓ All operation IDs already set" -ForegroundColor Green
}
