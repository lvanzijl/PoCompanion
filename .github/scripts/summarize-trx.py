#!/usr/bin/env python3
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def main() -> int:
    if len(sys.argv) != 6:
        print("Usage: summarize-trx.py <trx-directory> <trx-prefix> <gate-name> <summary-md-path> <summary-json-path>", file=sys.stderr)
        return 2

    trx_directory = Path(sys.argv[1])
    trx_prefix = sys.argv[2]
    gate_name = sys.argv[3]
    summary_md_path = Path(sys.argv[4])
    summary_json_path = Path(sys.argv[5])

    summary_md_path.parent.mkdir(parents=True, exist_ok=True)
    summary_json_path.parent.mkdir(parents=True, exist_ok=True)

    trx_paths = sorted(trx_directory.glob(f"{trx_prefix}*.trx"))

    if not trx_paths:
        expected_pattern = trx_directory / f"{trx_prefix}*.trx"
        message = (
            f"{gate_name}: expected TRX files matching '{expected_pattern}' in output directory "
            f"'{trx_directory}', but none were produced."
        )
        print(message, file=sys.stderr)
        payload = {
            "gateName": gate_name,
            "trxFiles": [],
            "failedTests": [],
            "total": 0,
            "passed": 0,
            "failed": 0,
            "notExecuted": 0,
            "warning": message
        }
        summary_json_path.write_text(json.dumps(payload, indent=2) + "\n")
        summary_md_path.write_text(
            f"# {gate_name} failing test summary\n\n"
            f"- TRX directory: `{trx_directory}`\n"
            f"- Expected pattern: `{expected_pattern}`\n"
            f"- Warning: {message}\n")
        return 1

    total = 0
    passed = 0
    failed = 0
    not_executed = 0
    failed_tests = []

    for trx_path in trx_paths:
        tree = ET.parse(trx_path)
        root = tree.getroot()
        namespace = {"trx": root.tag[root.tag.find("{") + 1:root.tag.find("}")]} if root.tag.startswith("{") else {}
        prefix = "trx:" if namespace else ""

        counters = root.find(f".//{prefix}Counters", namespace)
        total += int(counters.attrib.get("total", "0")) if counters is not None else 0
        passed += int(counters.attrib.get("passed", "0")) if counters is not None else 0
        failed += int(counters.attrib.get("failed", "0")) if counters is not None else 0
        not_executed += int(counters.attrib.get("notExecuted", "0")) if counters is not None else 0

        for result in root.findall(f".//{prefix}UnitTestResult", namespace):
            outcome = result.attrib.get("outcome", "")
            if outcome not in {"Failed", "Error", "Timeout", "Aborted"}:
                continue

            test_name = result.attrib.get("testName", "<unknown>")
            error_info = result.find(f"{prefix}Output/{prefix}ErrorInfo", namespace)
            message = ""
            if error_info is not None:
                message = (error_info.findtext(f"{prefix}Message", default="", namespaces=namespace) or "").strip()

            failed_tests.append({
                "name": test_name,
                "message": message
            })

    failed_tests.sort(key=lambda item: item["name"])

    payload = {
        "gateName": gate_name,
        "trxFiles": [str(path) for path in trx_paths],
        "total": total,
        "passed": passed,
        "failed": failed,
        "notExecuted": not_executed,
        "failedTests": failed_tests
    }
    summary_json_path.write_text(json.dumps(payload, indent=2) + "\n")

    lines = [
        f"# {gate_name} failing test summary",
        "",
        f"- Total: {total}",
        f"- Passed: {passed}",
        f"- Failed: {failed}",
        f"- Not executed: {not_executed}",
        f"- TRX files: {len(trx_paths)}",
        ""
    ]

    if not failed_tests:
        lines.append("No failing tests.")
    else:
        for test in failed_tests:
            lines.append(f"- `{test['name']}`")
            if test["message"]:
                lines.append(f"  - {test['message']}")

    summary_md_path.write_text("\n".join(lines) + "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
