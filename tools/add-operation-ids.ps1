#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Adds operation IDs to OpenAPI specification based on controller names and action names.

.DESCRIPTION
    NSwag.AspNetCore doesn't automatically set operation IDs from controller action names.
    This script generates operation IDs like "WorkItems_GetAll" from paths
    so that NSwag client generator can create proper client interfaces.
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
    
    # If already starts with uppercase, assume it's already PascalCase
    if ($text -cmatch '^[A-Z]') {
        return $text
    }
    
    # Split on hyphens and capitalize each word
    $words = $text -split '-'
    $result = ($words | ForEach-Object { 
        if ($_.Length -gt 0) {
            $_.Substring(0,1).ToUpper() + $_.Substring(1)
        }
    }) -join ''
    
    return $result
}

# Map of special endpoint names to controller names
$specialMappings = @{
    '/health' = @{ Controller = 'Health'; Action = 'GetHealth' }
    '/api/tfsconfig' = @{ Controller = 'TfsConfig'; ActionBase = 'TfsConfig' }
    '/api/tfsvalidate' = @{ Controller = 'TfsConfig'; ActionBase = 'Validate' }
    '/api/tfsverify' = @{ Controller = 'TfsConfig'; ActionBase = 'Verify' }
}

# Process each path
foreach ($pathKey in $json.paths.PSObject.Properties.Name) {
    $pathItem = $json.paths.$pathKey
    
    # Process each HTTP method
    foreach ($method in @('get', 'post', 'put', 'delete', 'patch')) {
        if ($pathItem.PSObject.Properties[$method]) {
            $operation = $pathItem.$method
            
            # Determine the verb based on HTTP method
            $verb = switch ($method) {
                'get' { 'Get' }
                'post' { 'Create' }
                'put' { 'Update' }
                'delete' { 'Delete' }
                'patch' { 'Patch' }
                default { '' }
            }
            
            # Check if this is a special mapped endpoint
            $controllerName = $null
            $actionName = $null
            
            if ($specialMappings.ContainsKey($pathKey)) {
                $mapping = $specialMappings[$pathKey]
                $controllerName = $mapping.Controller
                if ($mapping.Action) {
                    $operationId = "${controllerName}_$($mapping.Action)"
                } else {
                    $actionName = "$verb$($mapping.ActionBase)"
                    $operationId = "${controllerName}_${actionName}"
                }
            }
            else {
                # Extract meaningful parts from the path
                # e.g., "/api/Metrics/velocity" -> "Metrics", "velocity"
                $parts = @($pathKey -split '/' | Where-Object { 
                    $_ -and $_ -ne 'api' -and $_ -notmatch '^\{.*\}$' 
                })
                
                if ($parts.Count -gt 1) {
                    # Multi-segment path: first part is controller, last part is action
                    $controllerName = ConvertTo-PascalCase $parts[0]
                    $actionPart = $parts[-1]
                    $actionName = "$verb$(ConvertTo-PascalCase $actionPart)"
                    $operationId = "${controllerName}_${actionName}"
                }
                elseif ($parts.Count -eq 1) {
                    # Single segment: this is the controller root
                    $controllerName = ConvertTo-PascalCase $parts[0]
                    $actionName = "$verb$(ConvertTo-PascalCase $parts[0])"
                    $operationId = "${controllerName}_${actionName}"
                }
                else {
                    # Fallback
                    $cleanPath = $pathKey -replace '^/', '' -replace '/$', '' -replace '[^a-zA-Z0-9]', '_'
                    $operationId = "$verb$cleanPath"
                }
            }
            
            # Add the operation ID
            if ($operation.PSObject.Properties['operationId']) {
                $operation.operationId = $operationId
            } else {
                $operation | Add-Member -NotePropertyName "operationId" -NotePropertyValue $operationId
            }
            $script:addedCount++
            Write-Host "  Added: $operationId for $method $pathKey" -ForegroundColor Green
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
