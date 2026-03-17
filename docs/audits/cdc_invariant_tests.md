# CDC Invariant Tests

Status: corrected audit for previously generated invariant assumptions

References:

- `docs/domain/cdc_reference.md`
- `docs/domain/cdc_domain_map.md`
- `docs/domain/sprint_commitment_domain_model.md`
- `docs/audits/effort_planning_cdc_extraction.md`

## Reason for correction

The previously generated invariant set carried two incorrect assumptions:

1. it treated `SpilloverSP` as additive to `RemainingSP`, even though spillover is a subset of Remaining
2. it coupled SprintFacts story-point semantics to EffortPlanning effort-hour rollups

Those assumptions created false failures because SprintFacts and EffortPlanning own different units and different slice responsibilities.

## Original incorrect assumptions

- `CommittedSP = DeliveredSP + RemainingSP + SpilloverSP`
- SprintFacts invariants may use effort totals
- EffortPlanning totals may be equated to SprintFacts totals
- cross-slice invariants may compare effort hours directly with story points

## Corrected Invariants

### SprintFacts

SprintFacts uses story points only.

- `RemainingSP = CommittedSP + AddedSP - RemovedSP - DeliveredSP`
- `CommittedSP >= DeliveredSP`
- `AddedSP >= DeliveredFromAddedSP`
- `SpilloverSP <= RemainingSP`
- `DeliveredFromAddedSP <= AddedSP`

Interpretation constraints:

- Spillover is a subset of Remaining and must not be added on top of remaining scope
- SprintFacts must not use effort hours in its invariants

### EffortPlanning

EffortPlanning distribution totals equal the sum of work-item effort hours in the selected snapshot scope.

Interpretation constraints:

- EffortPlanning totals are effort-hour rollups, not SprintFacts story-point totals
- invariants must not equate effort hours with story points

## Final invariant definitions

Use the corrected SprintFacts formulas above for sprint story-point tests.

Use effort-hour summation only inside EffortPlanning distribution tests.

Do not create cross-slice invariants that compare effort-hour totals to story-point totals.
