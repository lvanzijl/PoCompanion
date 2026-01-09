#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fixes OpenAPI schema type issues where integers are incorrectly marked as union types.

.DESCRIPTION
    This script fixes a known issue where NSwag generates OpenAPI schemas with union types
    like ["integer", "string"] for integer properties that have regex patterns.
    The script modifies the openapi.json to use only "integer" type for these properties.

.EXAMPLE
    .\tools\fix-openapi-types.ps1
#>

$ErrorActionPreference = "Stop"

$openApiPath = "$PSScriptRoot/../PoTool.Client/openapi.json"

Write-Host "Fixing OpenAPI type issues..." -ForegroundColor Yellow

# Read the OpenAPI JSON
$json = Get-Content $openApiPath -Raw | ConvertFrom-Json -Depth 100

$fixCount = 0

# Function to recursively fix schemas
function Fix-Schema {
    param($obj)
    
    if ($null -eq $obj) { return }
    
    # Check if this is a property object
    if ($obj.PSObject.Properties['type'] -and $obj.PSObject.Properties['format']) {
        $typeValue = $obj.type
        
        # If type is an array containing both "integer" and "string", and format is "int32" or "int64"
        if ($typeValue -is [Array] -and 
            $typeValue.Contains("integer") -and 
            $typeValue.Contains("string") -and
            ($obj.format -eq "int32" -or $obj.format -eq "int64")) {
            
            # Fix: change to just "integer" and remove the pattern
            $obj.type = "integer"
            if ($obj.PSObject.Properties['pattern']) {
                $obj.PSObject.Properties.Remove('pattern')
            }
            $script:fixCount++
            Write-Host "  Fixed union type to integer (format: $($obj.format))" -ForegroundColor Green
        }
    }
    
    # Recursively process all properties
    foreach ($prop in $obj.PSObject.Properties) {
        if ($prop.Value -is [PSCustomObject]) {
            Fix-Schema $prop.Value
        }
        elseif ($prop.Value -is [Array]) {
            foreach ($item in $prop.Value) {
                if ($item -is [PSCustomObject]) {
                    Fix-Schema $item
                }
            }
        }
    }
}

# Fix schemas in components
if ($json.PSObject.Properties['components'] -and $json.components.PSObject.Properties['schemas']) {
    Fix-Schema $json.components.schemas
}

# Fix schemas in paths
if ($json.PSObject.Properties['paths']) {
    Fix-Schema $json.paths
}

if ($fixCount -gt 0) {
    # Write back the fixed JSON
    $json | ConvertTo-Json -Depth 100 | Set-Content $openApiPath -Encoding UTF8
    Write-Host "✓ Fixed $fixCount type issues in openapi.json" -ForegroundColor Green
}
else {
    Write-Host "✓ No type issues found in openapi.json" -ForegroundColor Green
}
