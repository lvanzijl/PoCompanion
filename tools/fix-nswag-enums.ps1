#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Post-processes the NSwag-generated API client to replace int enum properties with proper enum types.

.DESCRIPTION
    NSwag generates enum properties as 'int' because the OpenAPI spec defines them as integers.
    This script replaces those int properties with the correct enum types from PoTool.Shared.
    
.EXAMPLE
    .\tools\fix-nswag-enums.ps1
#>

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$GeneratedClientPath = Join-Path $RepoRoot "PoTool.Client/ApiClient/ApiClient.g.cs"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "NSwag Enum Type Fixer" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

if (!(Test-Path $GeneratedClientPath)) {
    Write-Host "ERROR: Generated client not found at: $GeneratedClientPath" -ForegroundColor Red
    exit 1
}

Write-Host "Reading generated client..." -ForegroundColor Yellow
$content = Get-Content $GeneratedClientPath -Raw

Write-Host "Applying enum type fixes..." -ForegroundColor Yellow

# Define the mappings from property names to enum types
$enumMappings = @{
    # Metrics enums
    "EffortTrend" = "TrendDirection"
    "ValidationTrend" = "TrendDirection"
    "BlockerTrend" = "TrendDirection"
    "Trend" = "TrendDirection"
    "Direction" = "TrendDirection"
    
    "Severity" = "BottleneckSeverity"
    "BottleneckSeverity" = "BottleneckSeverity"
    
    "Status" = "CapacityStatus"
    "CapacityStatus" = "CapacityStatus"
    
    "RiskLevel" = "ConcentrationRiskLevel"
    "ConcentrationRiskLevel" = "ConcentrationRiskLevel"
    "OverallRisk" = "ConcentrationRiskLevel"
    "Priority" = "ConcentrationRiskLevel"
    
    "TrendDirection" = "EffortTrendDirection"
    "EffortTrendDirection" = "EffortTrendDirection"
    
    "Confidence" = "ForecastConfidence"
    "ForecastConfidence" = "ForecastConfidence"
    
    "ImbalanceRiskLevel" = "ImbalanceRiskLevel"
    "FeatureImbalanceRisk" = "ImbalanceRiskLevel"
    "AreaImbalanceRisk" = "ImbalanceRiskLevel"
    
    "Strategy" = "MitigationStrategy"
    "MitigationStrategy" = "MitigationStrategy"
    
    "Type" = "RecommendationType"
    "RecommendationType" = "RecommendationType"
    
    "WarningLevel" = "WarningLevel"
    
    # Pipeline enums
    "Result" = "PipelineRunResult"
    "PipelineRunResult" = "PipelineRunResult"
    "LastRunResult" = "PipelineRunResult"
    
    "Trigger" = "PipelineRunTrigger"
    "PipelineRunTrigger" = "PipelineRunTrigger"
    
    "PipelineType" = "PipelineType"
    
    # Pull Request enums
    "ReviewerStatus" = "ReviewerStatus"
    
    # Settings enums
    "DataMode" = "DataMode"
    "PictureType" = "ProfilePictureType"
    "ProfilePictureType" = "ProfilePictureType"
    
    # Work Items enums
    "ChainRisk" = "DependencyChainRisk"
    "DependencyChainRisk" = "DependencyChainRisk"
    
    "LinkType" = "DependencyLinkType"
    "DependencyLinkType" = "DependencyLinkType"
}

$replacementCount = 0

foreach ($propertyName in $enumMappings.Keys) {
    $enumType = $enumMappings[$propertyName]
    
    # Pattern: public int PropertyName { get; set; }
    $pattern = "public int $propertyName \{ get; set; \}"
    $replacement = "public $enumType $propertyName { get; set; }"
    
    if ($content -match $pattern) {
        $content = $content -replace $pattern, $replacement
        $replacementCount++
        Write-Host "  ✓ Replaced: $propertyName (int → $enumType)" -ForegroundColor Green
    }
    
    # Pattern: public int? PropertyName { get; set; }
    $patternNullable = "public int\? $propertyName \{ get; set; \}"
    $replacementNullable = "public $enumType? $propertyName { get; set; }"
    
    if ($content -match $patternNullable) {
        $content = $content -replace $patternNullable, $replacementNullable
        $replacementCount++
        Write-Host "  ✓ Replaced: $propertyName (int? → $enumType?)" -ForegroundColor Green
    }
}

if ($replacementCount -eq 0) {
    Write-Host "  ⚠ No replacements made - this might indicate an issue" -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "Writing fixed client back to file..." -ForegroundColor Yellow
    $content | Set-Content $GeneratedClientPath -NoNewline
    
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "✓ Successfully fixed $replacementCount enum properties!" -ForegroundColor Green
    Write-Host "================================================" -ForegroundColor Cyan
}
