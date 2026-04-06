# Slice 5 — Audit

## 1. Reconstructed Architecture

Reconstructed pipeline:
- legacy read: `OnboardingLegacyMigrationReader` reads `TfsConfigEntity`, `TeamEntity`, `PipelineDefinitionEntity`, `ProductBacklogRootEntity`, and `ProductTeamLinkEntity`, then derives project references and pipeline bindings in memory
- mapping: `OnboardingMigrationMapper` maps legacy records into Slice 1 onboarding entities plus `OnboardingMigrationMappingContext`
- validation: `OnboardingMigrationExecutionService` calls Slice 2 validation before each write via `IOnboardingValidationService`
- persistence: live mode upserts Slice 1 entities directly through `PoToolDbContext`
- ledger: Slice 4 `IOnboardingMigrationLedgerService` creates runs, units, issues, and final summaries

Slice dependencies:
- Slice 1: onboarding persistence entities/configuration and FK/index contracts
- Slice 2: validation and live lookup services
- Slice 3: not modified, but post-migration correctness depends on its status derivation consuming valid migrated state
- Slice 4: run/unit/issue ledger and observability lifecycle

Execution ordering guarantee:
1. connection
2. projects
3. teams
4. pipelines
5. product roots
6. bindings

What must be true for correctness:
- legacy input ordering must be stable
- project, pipeline, and work-item discovery must resolve to the same authoritative identities on every run
- external IDs used for upserts must be globally safe for the query shape actually used
- every unresolved dependency must surface as a recorded issue
- dry-run and live execution must differ only in onboarding-entity persistence
- run/unit counts must match actual attempted entities

Invariants relied upon but not proven:
- `PipelineExternalId` is globally unique enough to use outside project scope
- `ProjectExternalId`, `TeamExternalId`, and `WorkItemExternalId` can be queried globally even though DB uniqueness is scoped by parent for several entities
- `Dictionary` insertion order is stable enough for action ordering
- live TFS reads are stable enough to support deterministic migration claims
- no concurrent migration runs target the same dataset

## 2. Determinism Audit

Findings:
- legacy DB reads are explicitly ordered in the reader for teams, pipelines, product roots, team bindings, pipeline bindings, and project references
- execution itself is sequential; units are awaited in order and no `Task.WhenAll` is used
- the determinism claim is weakened by hidden time and live-system dependencies

Concrete determinism breaks:
1. `OnboardingMigrationMapper.CreateMetadata()` injects `DateTime.UtcNow` into every mapped snapshot, so the same legacy input maps to different entity state on every run (`PoTool.Api/Services/Onboarding/OnboardingMigrationMapper.cs:172-183`)
2. validation snapshots and validation states also embed current time and fresh live lookup results, so identical legacy input can produce different persisted values across runs (`PoTool.Api/Services/Onboarding/OnboardingValidationService.cs:41-56`, `104-108`, `141-146`, `179-184`, `210-214`)
3. project discovery depends on current TFS project, pipeline, and work-item visibility, not just legacy input; the same database can resolve differently if TFS changes or is temporarily unavailable (`PoTool.Api/Services/Onboarding/OnboardingMigrationExecutionService.cs:153-176`, `611-649`)
4. `BuildBindingActions` relies on dictionary-backed state without explicit sorting, so final binding processing order is implementation-dependent rather than contractually fixed (`PoTool.Api/Services/Onboarding/OnboardingMigrationExecutionService.cs:1009-1065`)

Determinism verdict:
- execution order is sequential
- input ordering is mostly explicit
- full deterministic behavior is **not proven**
- the “deterministic execution” claim is overstated because mapping and validation both depend on wall-clock time and live external state

## 3. Idempotency Audit

Findings:
- live mode uses upsert helpers instead of blind inserts
- binding uniqueness is implemented on `(ProductRootId, SourceType, SourceExternalId)`
- the stored fingerprint is compared, but `HasMatchingFingerprint` is not used to alter behavior (`PoTool.Api/Services/Onboarding/OnboardingMigrationExecutionService.cs:63-80`, `1068-1081`)

Concrete duplicate/drift failure modes:
1. upsert queries ignore the same scoping used by the database uniqueness rules:
   - `ProjectSource` is uniquely indexed by `(TfsConnectionId, ProjectExternalId)` but queried only by `ProjectExternalId`
   - `PipelineSource` is uniquely indexed by `(ProjectSourceId, PipelineExternalId)` but queried only by `PipelineExternalId`
   - `ProductRoot` is uniquely indexed by `(ProjectSourceId, WorkItemExternalId)` but queried only by `WorkItemExternalId`
   This can overwrite the wrong row or throw if multiple scoped rows exist (`ProjectSourceConfiguration.cs:28-29`, `PipelineSourceConfiguration.cs:28-29`, `ProductRootConfiguration.cs:28-29`, `OnboardingMigrationExecutionService.cs:776-907`)
2. concurrent runs can race: both runs may read “missing”, both attempt insert, and one will fail on a uniqueness constraint after partial ledger/entity progress because there is no transaction or run-level lock (`OnboardingMigrationExecutionService.cs:746-907`)
3. pipeline identity is treated as globally resolvable during discovery and persistence, but pipeline IDs are stored independently from project scope; a duplicate pipeline ID across projects would drift to whichever project wins discovery last (`OnboardingMigrationExecutionService.cs:629-649`, `824-845`)
4. partial reruns can silently drift because dropped binding prerequisites are filtered out before issue creation, so the rerun summary can show fewer processed bindings without proving equivalence to legacy state (`OnboardingMigrationExecutionService.cs:1027-1062`)

Idempotency verdict:
- “no duplicate entities on rerun” is only partially demonstrated by tests
- practical idempotency is **not production-safe under scoped-identity conflicts or concurrent runs**

## 4. Validation Integrity

Findings:
- within `OnboardingMigrationExecutionService`, validation is called before every live onboarding write
- invalid validation results block the direct write path in that service

Integrity gaps:
1. validation consistency is not stable across runs because it is based on live TFS reads and current timestamps, not a captured validation baseline (`OnboardingValidationService.cs:42-56`, `90-108`, `113-146`, `151-184`, `189-214`)
2. transient discovery failures are swallowed before validation/issue handling:
   - failed work-item lookups in project discovery are ignored
   - failed pipeline lookups in project discovery are ignored
   This means `TfsUnavailable` can change candidate sets without a corresponding issue (`OnboardingMigrationExecutionService.cs:611-649`)
3. invalid data can leak into migration decisions even when it does not leak into onboarding tables, because discovery logic can omit entities instead of validating and failing them explicitly

Validation verdict:
- direct persistence paths are validation-first
- system-level validation integrity is incomplete because pre-validation discovery contains silent fallback behavior

## 5. Dependency Handling

Ordering strengths:
- unit order is fixed and sequential
- team, pipeline, root, and binding units check for prerequisite state before processing

Dependency failures detected too late or incompletely:
1. unresolved project references can collapse to `candidateProjects.Count == 0` and cause the project unit to skip with no issue, pushing failure detection into downstream units or hiding it entirely (`OnboardingMigrationExecutionService.cs:172-182`)
2. missing team/root/pipeline prerequisites for bindings are silently filtered out in `BuildBindingActions` instead of recorded as `DependencyViolation` issues (`OnboardingMigrationExecutionService.cs:1027-1062`)
3. work-item/pipeline discovery failures are handled during candidate resolution instead of explicit dependency enforcement, so root causes can disappear before unit processing begins (`OnboardingMigrationExecutionService.cs:611-649`)

Invalid graph risk:
- writes are sequential, but each entity is committed immediately with `SaveChangesAsync`, so later unit failure leaves a partial migrated graph by design (`OnboardingMigrationExecutionService.cs:746-907`)
- that may be acceptable for a rerunnable backfill, but it is not equivalent to a strongly consistent migration transaction

## 6. Issue Recording

What is correct:
- explicit validation failures record blocking issues
- explicit missing team ID and explicit dependency failures during unit execution record issues

Silent or incomplete paths:
1. `ResolveProjectCandidatesAsync` drops failed work-item discovery without recording any issue (`OnboardingMigrationExecutionService.cs:611-617`)
2. `ResolveProjectCandidatesAsync` drops failed pipeline discovery without recording any issue (`OnboardingMigrationExecutionService.cs:631-635`)
3. `BuildBindingActions` drops missing roots, teams, or pipelines without recording any issue (`OnboardingMigrationExecutionService.cs:1027-1062`)
4. the implementation never emits `Warning` or `Info`; all execution issues are blocking, so the severity model claimed in the report is not actually exercised (`OnboardingMigrationExecutionService.cs:702-743`)

Execution-after-issue concern:
- unit failure does not stop later units; the service continues into downstream units and records generic dependency failures, which can obscure the original fault chain and inflate secondary noise (`OnboardingMigrationExecutionService.cs:82-87`, `654-673`)

## 7. Dry-Run Analysis

What matches live mode:
- same reader, discovery, mapper, validator, unit ordering, and ledger flow are used
- onboarding entity persistence is skipped in dry-run mode

Dry-run divergence risks:
1. dry-run never exercises EF uniqueness constraints, FK enforcement, or persistence contract validation, so it can pass even when live mode would fail on database write semantics (`OnboardingMigrationExecutionService.cs:577-585`, `746-907`)
2. dry-run uses transient entities with default IDs, so binding validation runs against in-memory `Id = 0` references rather than real persisted identifiers; this is not identical to live execution behavior (`OnboardingMigrationExecutionService.cs:557-565`)
3. dry-run still writes ledger rows and observability output, so it is not zero-side-effect in the strict sense

Dry-run verdict:
- useful for orchestration rehearsal
- not a faithful proof of live-write correctness

## 8. Ledger Consistency

Strengths:
- Slice 4 enforces valid unit transitions
- final run status is derived from terminal unit states
- run finalization blocks if units remain pending/running

Misleading or inconsistent cases:
1. counts can diverge from actual legacy workload:
   - project discovery failure records outcome `(1 processed, 1 failed)` regardless of actual candidate count
   - dependency unit failure records `(0 processed, 1 failed)` even though no entity was processed
   - silently dropped bindings are omitted from counts entirely (`OnboardingMigrationExecutionService.cs:156-169`, `661-673`, `1027-1062`)
2. run/unit transitions and entity writes are not atomic together; a crash after entity persistence but before unit completion leaves ledger status behind actual data (`OnboardingMigrationExecutionService.cs:746-907`, `OnboardingMigrationLedgerService.cs:266-337`)
3. run status can look better than actual migration fidelity because skipped or uncounted legacy records do not necessarily create issues or failed counts

Ledger verdict:
- lifecycle mechanics are solid
- outcome accounting is not fully trustworthy as an audit-grade truth source

## 9. Governance Compliance

Observed strengths:
- no UI paths involved
- no onboarding CRUD endpoints added
- migration stays in backend services
- onboarding writes target Slice 1 entities and Slice 4 ledger entities only

Subtle violations or risk areas:
1. the report claims deterministic/idempotent safety more strongly than the code proves; that is a governance-quality problem because auditability is overstated by the authoritative Slice 5 report
2. the service is registered in DI but has no explicit operator-only invocation boundary in this slice, so “not exposed to normal user flows” is true only by absence of a controller today, not by a hardened boundary
3. `HasMatchingFingerprint` is computed but unused, which means the stated fingerprint comparison strategy is not actually enforced in execution logic (`OnboardingMigrationExecutionService.cs:63-80`, `1068-1081`)

## 10. Verdict

**FAIL**

Top 5 blocking risks:
1. non-deterministic mapping and validation due to `DateTime.UtcNow` and live lookup dependence
2. upsert queries do not match the scoped uniqueness model of the database for several entity types
3. silent failure paths in project/pipeline/work-item discovery and binding construction mean the ledger is not complete
4. dry-run cannot prove live correctness because it bypasses all DB write semantics
5. no transaction/locking strategy exists for concurrent or crash-interrupted runs, so rerun safety is not production-grade

Top 5 non-blocking improvements:
1. explicitly sort all action lists and binding actions before processing
2. convert silent discovery drops into explicit `MigrationIssue` records
3. either use fingerprint state to skip/reconcile runs or remove the misleading unused comparison
4. align processed/succeeded/failed counts with actual attempted legacy entities, not synthetic counters
5. add adversarial tests for duplicate scoped identities, concurrent runs, silent discovery failures, and binding omission cases
