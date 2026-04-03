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

# Pattern 1: .Result on task-like sources
# Uses a lightweight source scan so DTO or view-model properties named Result do not trigger false positives.
echo "Checking for .Result patterns..."
RESULT_CHECK_OUTPUT=$(python3 <<'PY'
import pathlib
import re

root = pathlib.Path("PoTool.Client")
file_extensions = {".cs", ".razor"}

assignment_pattern = re.compile(
    r'\b(?:var|[A-Za-z_][A-Za-z0-9_<>,?.\[\]]*)\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*([^;]+);')
task_type_pattern = re.compile(
    r'\b(?:Task|ValueTask)(?:<[^;]+?>)?\s+([A-Za-z_][A-Za-z0-9_]*)\b')
member_result_pattern = re.compile(r'([A-Za-z_][A-Za-z0-9_]*)\.Result\b')
direct_async_result_pattern = re.compile(r'(?:\b[A-Za-z_][A-Za-z0-9_.]*Async\s*\([^;\n]*\)|\b(?:Task|ValueTask)\.[A-Za-z_][A-Za-z0-9_]*\([^;\n]*\))\.Result\b')

violations: list[str] = []

def is_async_source(expression: str) -> bool:
    return (
        "Async(" in expression
        or ".AsTask(" in expression
        or expression.strip().startswith("Task.")
        or expression.strip().startswith("ValueTask.")
        or ".ContinueWith(" in expression
    )

for path in sorted(root.rglob("*")):
    if path.suffix not in file_extensions or not path.is_file():
        continue

    async_like_names: set[str] = set()
    lines = path.read_text(encoding="utf-8").splitlines()

    for line in lines:
        type_match = task_type_pattern.search(line)
        if type_match:
            async_like_names.add(type_match.group(1))

        assignment_match = assignment_pattern.search(line)
        if assignment_match and is_async_source(assignment_match.group(2)):
            async_like_names.add(assignment_match.group(1))

    for line_number, line in enumerate(lines, start=1):
        if ".Result" not in line or "// allowed:" in line:
            continue

        result_index = line.find(".Result")
        if result_index >= 0 and "await" in line[:result_index]:
            continue

        is_violation = False

        if direct_async_result_pattern.search(line):
            is_violation = True
        else:
            for match in member_result_pattern.finditer(line):
                name = match.group(1)
                if name in async_like_names or name.lower().endswith("task"):
                    is_violation = True
                    break

        if is_violation:
            violations.append(f"{path}:{line_number}:{line}")

if violations:
    print("\n".join(violations))
PY
)

if [ -n "$RESULT_CHECK_OUTPUT" ]; then
    echo "$RESULT_CHECK_OUTPUT"
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
