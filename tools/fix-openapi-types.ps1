#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fixes OpenAPI schema type issues where integers are incorrectly marked as union types.

.DESCRIPTION
    This script fixes a known issue where NSwag generates OpenAPI schemas with union types
    like ["integer", "string"] for integer properties that have regex patterns.
    The script also fixes nullable properties that are incorrectly marked as required.
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
$nullabilityFixCount = 0

# List of properties that should be nullable (based on C# DTOs with nullable types)
# IMPORTANT: Keep this list in sync with actual DTO definitions in PoTool.Core
# This list is needed because NSwag's OpenAPI generation sometimes incorrectly marks
# nullable properties as required. This causes compilation errors in the generated client.
# 
# To find nullable properties in DTOs:
#   grep -r "int?" PoTool.Core/ --include="*.cs"
#   grep -r "string?" PoTool.Core/ --include="*.cs" 
#
# Common nullable properties across DTOs:
$nullableProperties = @(
    'parentTfsId',  # WorkItemDto, WorkItemWithValidationDto (parent is optional for root items)
    'effort',       # WorkItemDto, EffortDistribution DTOs (effort may not be estimated yet)
    'epicId',       # Various DTOs (epic relationship is optional)
    'capacity'      # IterationEffortDistribution (capacity may not be set for iterations)
)

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
            
            # Fix: change to just "integer"
            $obj.type = "integer"
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
    
    # Fix nullability: Remove nullable properties from required arrays and mark them as nullable
    foreach ($schemaName in $json.components.schemas.PSObject.Properties.Name) {
        $schema = $json.components.schemas.$schemaName
        
        if ($schema.PSObject.Properties['required'] -and $schema.PSObject.Properties['properties']) {
            $requiredList = [System.Collections.ArrayList]@($schema.required)
            
            foreach ($propName in $schema.properties.PSObject.Properties.Name) {
                if ($nullableProperties -contains $propName) {
                    # Remove from required list if present
                    if ($requiredList.Contains($propName)) {
                        $requiredList.Remove($propName) | Out-Null
                        $script:nullabilityFixCount++
                        Write-Host "  Removed '$propName' from required list in '$schemaName'" -ForegroundColor Green
                    }
                    
                    # Add nullable flag to property
                    $property = $schema.properties.$propName
                    if (-not $property.PSObject.Properties['nullable']) {
                        $property | Add-Member -NotePropertyName 'nullable' -NotePropertyValue $true -Force
                    }
                    elseif (-not $property.nullable) {
                        $property.nullable = $true
                    }
                }
            }
            
            # Update the required array
            if ($requiredList.Count -eq 0) {
                $schema.PSObject.Properties.Remove('required')
            }
            else {
                $schema.required = $requiredList.ToArray()
            }
        }
    }
}

# Fix schemas in paths
if ($json.PSObject.Properties['paths']) {
    Fix-Schema $json.paths
}

$totalFixes = $fixCount + $nullabilityFixCount
if ($totalFixes -gt 0) {
    # Write back the fixed JSON
    $json | ConvertTo-Json -Depth 100 | Set-Content $openApiPath -Encoding UTF8
    Write-Host "✓ Fixed $fixCount type issues and $nullabilityFixCount nullability issues in openapi.json" -ForegroundColor Green
}
else {
    Write-Host "✓ No type issues found in openapi.json" -ForegroundColor Green
}
