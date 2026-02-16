## What changed
- Added offline recorded payload JSON fixtures under `PoTool.Tests.Unit/RecordedPayloads/`.
- Added unit tests for continuation token normalization, nextLink token extraction (absolute/relative), and reporting URL token/startDateTime mutual exclusivity.
- Added test-side helpers for loading recorded payloads and request capture.

## Why
- The issue requires fully offline validation with deterministic payloads and no live Azure DevOps/TFS dependency.
- These tests establish a stable baseline for paging and token parsing behavior before and after production fixes.

## How tests validate correctness
- Tests read recorded payload files and run through `RealRevisionTfsClient` via mocked `HttpMessageHandler`.
- URL construction tests assert `continuationToken` and `startDateTime` are never emitted together.
