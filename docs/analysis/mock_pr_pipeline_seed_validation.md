# Mock PR/Pipeline Seed Validation

Date: 2026-03-17

## Scope

This audit validates the mock-backed pull request and pipeline seed changes introduced for realistic PR/pipeline analytics coverage.

Validation source:

- `MockConfigurationSeedHostedService` seeded against an in-memory `PoToolDbContext`
- `BattleshipMockDataFacade` generated pull requests, work item links, and pipeline runs
- `MockTfsClient` resolved repository-scoped pipeline definitions and runs

## Counts

- Repositories seeded: 6
- Pull requests generated: 150
- Pull requests linked to at least one work item: 84.7%
- Pipeline definitions discovered from seeded repositories: 22
- Pipeline runs sampled from discovered definitions: 66

Validation checks:

- `pullRequestCount > 0`: pass
- `pipelineCount > 0`: pass
- `>= 80%` of PRs linked to `>= 1` work item: pass
- Broken PR → work item references: none detected
- Broken repository → pipeline definition references: none detected in the sampled dataset

## Linkage Distribution

Pull request → work item linkage distribution:

- Single work item: 90 PRs
- 2–3 work items: 27 PRs
- 4–8 work items: 10 PRs
- No linked work items: 23 PRs

Notes:

- PR links now resolve against existing seeded work item IDs instead of synthetic numeric ranges.
- Eligible linked work items come from active or completed implementation-level items already present in the mock hierarchy.

## Pull Request Duration Distribution

Completed PR status mix:

- Completed: 90.7%
- Abandoned: 3.3%
- Active: 6.0%

Completed PR duration buckets:

- 2–8 hours: 47
- 0.5–2 days: 71
- 2–5 days: 18

## Pipeline Duration and Result Distribution

Pipeline trigger mix in sampled runs:

- Pull request triggered: 8
- Continuous integration: 23
- Manual: 18
- Other trigger types present in remaining sampled runs

Pipeline duration buckets:

- 2–5 minutes: 7
- 5–15 minutes: 17
- 15–40 minutes: 34

Pipeline run results:

- Succeeded: 39
- Failed: 23
- Canceled: 1

## Sample Mappings

Representative repository-scoped examples from the seeded mock dataset:

1. Work item `1447` → PR `1001` in `Battleship-Coordination-UI` → pipeline `Battleship.DamageControl.CI` runs `1217`, `1218`
2. Work item `23925` → PR `1002` in `Battleship-Maintenance-Analytics` → pipeline `Battleship.IncidentDetection.CI` runs `1161`, `1162`
3. Work item `4624` → PR `1003` in `Battleship-CrewSafety-UI` → pipeline `Battleship.API.Gateway.CI` runs `1053`, `1055`

## Implementation Notes

- Mock startup now seeds repository configuration for each seeded product, which unblocks PR sync scope and pipeline-definition discovery.
- Mock pipeline definitions are now derived from the generated mock pipeline catalog, so discovered definition IDs line up with actual generated run IDs.
- The current cached pipeline model remains repository-scoped and trigger-based; it does not persist a direct PR foreign key. This audit therefore validates repository-consistent PR-triggered pipeline coverage rather than a stored PR → pipeline FK.
