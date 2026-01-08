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
            
            # Force regenerate operation IDs to include controller prefix
            if ($true) {
                
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
                # Include controller name as prefix for proper client separation
                # e.g., "WorkItems_GetAll", "Metrics_GetVelocity"
                if ($parts.Count -gt 1) {
                    $controllerName = $parts[0]
                    $lastPart = $parts[-1]
                    $pascalCase = ConvertTo-PascalCase $lastPart
                    # Keep controller name as-is if it's already PascalCase, otherwise convert it
                    if ($controllerName -cmatch '^[A-Z]') {
                        $operationId = "${controllerName}_${verb}${pascalCase}"
                    } else {
                        $controllerPascal = ConvertTo-PascalCase $controllerName
                        $operationId = "${controllerPascal}_${verb}${pascalCase}"
                    }
                }
                elseif ($parts.Count -eq 1) {
                    $controllerName = $parts[0]
                    # If it's just the controller name, use Controller_Verb + controller name
                    # e.g., "GET /api/Profiles" -> "Profiles_GetProfiles"
                    # e.g., "GET /health" -> "Health_GetHealth"
                    $pascalCase = ConvertTo-PascalCase $controllerName
                    # Keep controller name as-is if it's already PascalCase, otherwise convert it
                    if ($controllerName -cmatch '^[A-Z]') {
                        $operationId = "${controllerName}_${verb}${pascalCase}"
                    } else {
                        $operationId = "${pascalCase}_${verb}${pascalCase}"
                    }
                }
                else {
                    # Fallback - try to extract from path
                    $cleanPath = $pathKey -replace '^/', '' -replace '/$', ''
                    $operationId = "$verb$cleanPath" -replace '[^a-zA-Z0-9]', ''
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
