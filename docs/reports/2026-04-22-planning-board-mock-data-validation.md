# Planning board mock data validation

## Summary of dataset

- Products in seeded planning dataset: 2
  - Crew Safety Operations
  - Incident Response Control
- Canonical Battleship sprint range used for planning intent: Sprint 3 through Sprint 14
- Deterministic timeline variation confirmed:
  - multiple products in the same seeded dataset
  - parallel work in Crew Safety Operations
  - stable gap at Sprint 7 for Crew Safety Operations
  - mixed epic lengths: 1 sprint, 2 sprints, 3 sprints, and 4 sprints

## Sprint heat variation

The first six rendered planning-board columns for Crew Safety Operations deterministically resolve to:

| Rendered column | Battleship sprint | Planning state |
| --- | --- | --- |
| 1 | Sprint 3 | healthy |
| 2 | Sprint 4 | near-limit |
| 3 | Sprint 5 | overcommitted |
| 4 | Sprint 6 | near-limit |
| 5 | Sprint 7 | provisional |
| 6 | Sprint 8 | healthy |

## Epic placement table

| Product | Epic ID | Start sprint | End sprint | Duration | Track |
| --- | ---: | --- | --- | ---: | ---: |
| Crew Safety Operations | 8042 | Sprint 3 | Sprint 3 | 1 | 0 |
| Crew Safety Operations | 7032 | Sprint 4 | Sprint 6 | 3 | 0 |
| Crew Safety Operations | 8774 | Sprint 4 | Sprint 5 | 2 | 1 |
| Crew Safety Operations | 7275 | Sprint 5 | Sprint 6 | 2 | 2 |
| Crew Safety Operations | 8504 | Sprint 8 | Sprint 11 | 4 | 0 |
| Crew Safety Operations | 7804 | Sprint 9 | Sprint 10 | 2 | 1 |
| Incident Response Control | 2949 | Sprint 3 | Sprint 3 | 1 | 0 |
| Incident Response Control | 16534 | Sprint 3 | Sprint 3 | 1 | 1 |
| Incident Response Control | 16100 | Sprint 4 | Sprint 4 | 1 | 0 |
| Incident Response Control | 1951 | Sprint 5 | Sprint 5 | 1 | 0 |
| Incident Response Control | 1831 | Sprint 6 | Sprint 6 | 1 | 0 |
| Incident Response Control | 15957 | Sprint 7 | Sprint 7 | 1 | 0 |
| Incident Response Control | 15095 | Sprint 8 | Sprint 8 | 1 | 0 |
| Incident Response Control | 1238 | Sprint 9 | Sprint 9 | 1 | 0 |
| Incident Response Control | 2274 | Sprint 10 | Sprint 10 | 1 | 0 |
| Incident Response Control | 3084 | Sprint 11 | Sprint 11 | 1 | 0 |
| Incident Response Control | 15304 | Sprint 12 | Sprint 12 | 1 | 0 |
| Incident Response Control | 1002 | Sprint 13 | Sprint 13 | 1 | 0 |
| Incident Response Control | 2423 | Sprint 14 | Sprint 14 | 1 | 0 |
| Incident Response Control | 16665 | Sprint 14 | Sprint 14 | 1 | 1 |

## Overlap cases

### Crew Safety Operations

- Sprint 4:
  - Epic 7032 on track 0 overlaps with epic 8774 on track 1
- Sprint 5:
  - Epic 7032 on track 0 overlaps with epic 8774 on track 1
  - Epic 7275 on track 2 creates a third concurrent lane
- Sprint 6:
  - Epic 7032 on track 0 overlaps with epic 7275 on track 2
- Sprint 9 and Sprint 10:
  - Epic 8504 on track 0 overlaps with epic 7804 on track 1

These cases require at least 3 tracks at peak concurrency and validate deterministic minimal-track assignment.

### Incident Response Control

- Sprint 3:
  - Epics 2949 and 16534 run in parallel on tracks 0 and 1
- Sprint 14:
  - Epics 2423 and 16665 run in parallel on tracks 0 and 1

This second product remains visible in the shared dataset while keeping a simpler single-sprint distribution.

## Integrity checks

- No overlaps remain within the same track after deterministic track assignment.
- All seeded epics fall within the valid Sprint 3 to Sprint 14 range.
- Both target products are present in the same mock-backed planning dataset.
- The previously failing overlap assertion in `BattleshipMockScenario_SeedsDeterministicPlanningBoards_ForTwoProducts` was corrected to validate the intended ordering condition.

## Screenshot reference

- Screenshot reference: not generated in this follow-up change.
- Visibility confirmation in this report is based on deterministic seeded data and the validated planning-board read model, not on a captured UI artifact.

## Assumptions and limitations

- Sprint names in this report use the canonical Battleship sprint numbers persisted by the mock seed.
- Track assignments listed here are derived from the seeded planning-board read model after deterministic bootstrap.
- Screenshot generation remains a separate follow-up task if a UI artifact is still required for review.
