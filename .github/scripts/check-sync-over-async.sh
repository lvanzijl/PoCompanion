#!/bin/bash
# Script to check for sync-over-async patterns in PoTool.Client
# Required by PROCESS_RULES.md Section 13.1
#
# This script scans only the PoTool.Client directory for forbidden patterns
# that could cause deadlocks in Blazor WebAssembly.
#
# Forbidden patterns (blocking calls):
# - .Result (blocking access to Task.Result)
# - .Wait( (synchronous wait)
# - GetAwaiter().GetResult (synchronous wait)
# - AsTask().Result (synchronous wait on ValueTask)
# - AsTask().Wait (synchronous wait on ValueTask)
#
# Allowed patterns (async access):
# - await x.Result - This is async access to a property named Result (e.g., DialogResult)
# - Lines with "// allowed:" comment - Explicitly marked exceptions
#
# Note: MudBlazor dialogs use .Result as a property on DialogReference that returns 
# the dialog's result. Using "await dialog.Result" is correct async usage.

set -e

echo "=== Sync-over-Async Pattern Check ==="
echo "Checking PoTool.Client for forbidden patterns..."
echo ""

# Check if directory exists
if [ ! -d "PoTool.Client" ]; then
    echo "Error: PoTool.Client directory not found."
    echo "Run this script from the repository root."
    exit 1
fi

FOUND_VIOLATIONS=0

# Pattern 1: .Result (not followed by a letter, to exclude property names like "Results")
# Excludes lines containing "await" before ".Result" on the same line (legitimate async pattern)
echo "Checking for .Result patterns..."
if grep -rn --include="*.cs" --include="*.razor" -E '\.Result[^a-zA-Z]' PoTool.Client/ 2>/dev/null | grep -v '// allowed:' | grep -vE 'await[^;]*\.Result'; then
    echo "❌ Found forbidden .Result pattern"
    FOUND_VIOLATIONS=1
else
    echo "✓ No .Result violations found"
fi

# Pattern 2: .Wait(
echo ""
echo "Checking for .Wait( patterns..."
if grep -rn --include="*.cs" --include="*.razor" -E '\.Wait\(' PoTool.Client/ 2>/dev/null | grep -v '// allowed:'; then
    echo "❌ Found forbidden .Wait( pattern"
    FOUND_VIOLATIONS=1
else
    echo "✓ No .Wait( violations found"
fi

# Pattern 3: GetAwaiter().GetResult
echo ""
echo "Checking for GetAwaiter().GetResult patterns..."
if grep -rn --include="*.cs" --include="*.razor" -E 'GetAwaiter\(\)\.GetResult' PoTool.Client/ 2>/dev/null | grep -v '// allowed:'; then
    echo "❌ Found forbidden GetAwaiter().GetResult pattern"
    FOUND_VIOLATIONS=1
else
    echo "✓ No GetAwaiter().GetResult violations found"
fi

# Pattern 4: AsTask().Result
echo ""
echo "Checking for AsTask().Result patterns..."
if grep -rn --include="*.cs" --include="*.razor" -E 'AsTask\(\)\.Result' PoTool.Client/ 2>/dev/null | grep -v '// allowed:'; then
    echo "❌ Found forbidden AsTask().Result pattern"
    FOUND_VIOLATIONS=1
else
    echo "✓ No AsTask().Result violations found"
fi

# Pattern 5: AsTask().Wait
echo ""
echo "Checking for AsTask().Wait patterns..."
if grep -rn --include="*.cs" --include="*.razor" -E 'AsTask\(\)\.Wait' PoTool.Client/ 2>/dev/null | grep -v '// allowed:'; then
    echo "❌ Found forbidden AsTask().Wait pattern"
    FOUND_VIOLATIONS=1
else
    echo "✓ No AsTask().Wait violations found"
fi

echo ""
echo "=== Summary ==="

if [ $FOUND_VIOLATIONS -eq 1 ]; then
    echo "❌ FAILED: Sync-over-async patterns detected in PoTool.Client"
    echo ""
    echo "These patterns can cause deadlocks in Blazor WebAssembly."
    echo "See PROCESS_RULES.md Section 13 for details."
    echo ""
    echo "To fix:"
    echo "  - Replace .Result with await"
    echo "  - Replace .Wait() with await"
    echo "  - Replace GetAwaiter().GetResult() with await"
    echo "  - Mark the containing method as async"
    exit 1
else
    echo "✅ PASSED: No sync-over-async patterns found"
    exit 0
fi
