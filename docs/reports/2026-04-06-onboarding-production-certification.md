# Production Certification — Onboarding

## 1. Scenario Results

| Scenario | Result (Pass/Fail) |
| --- | --- |
| happy-binding-chain | Pass |
| missing-root | Pass |
| team-assignment | Pass |
| pipeline-assignment | Fail |
| permission-denied | Pass |
| stale-project | Pass |
| tfs-unavailable | Pass |
| lookup-empty | Pass |

## 2. System Validation

Executed evidence:

- browser-verified `happy-binding-chain`
  - project link succeeded
  - root creation succeeded
  - binding creation succeeded
  - final summary reached `Complete` with `Projects 1/1`, `Roots 1/1`, `Bindings 1/1`
- browser-verified `missing-root`
  - root action stayed in context
  - explicit error surfaced: `Work item '1001' was not found.`
  - counts remained unchanged
- browser-verified `team-assignment`
  - invalid binding blocker visible
  - `Go to fix location` routed to `replace-binding-source`
  - replacement selector rendered with `Crew Safety Team`
  - replacement mutation succeeded
  - binding updated to `team-crew-safety`
  - binding blocker disappeared
- browser-verified `pipeline-assignment`
  - invalid pipeline binding blocker visible
  - blocker card still advertised `Next: Create binding for product root`
  - `Go to fix location` routed to `onboardingIntentType=create-binding`
  - binding action zone rendered create-binding UI instead of replacement selector UI
  - replacement candidate selection was not available from the blocker flow
- deterministic scenario test-verified `pipeline-assignment`
  - backend/service path still passes in isolation
  - final browser certification failure is specifically on the blocker-entry UX path, which supersedes the earlier targeted-fix slice reports for production certification
- browser-verified `permission-denied`
  - explicit error surfaced: `TFS denied the requested lookup.`
  - no false success
  - no count drift
- browser-verified `tfs-unavailable`
  - explicit error surfaced: `TFS is currently unavailable.`
  - no false success
  - no count drift
- deterministic test-verified `stale-project`
  - `StaleProjectScenario_IsReproducible` passed
  - stale project validation still returns the intended `NotFound` outcome
- API-verified `lookup-empty`
  - `/api/onboarding/lookups/projects/battleship-systems-project/teams?limit=10&offset=0` returned `0`
  - `/api/onboarding/lookups/projects/battleship-systems-project/pipelines?limit=10&offset=0` returned `0`

Certification validation also found one failing deterministic support test during the broader onboarding suite:

- `MockConfigurationSeedHostedServiceTests.StartAsync_WhenTeamAssignmentScenarioSelected_SeedsReachableAssignmentBlocker`
  - expected one seeded team source
  - actual deterministic fixture now seeds two team sources for reachable replacement

## 3. Invariants

- no stale data observed in passing browser mutation flows: Pass
- no ghost entities observed in exercised flows: Pass
- graph == problem-first == backend in passing scenarios: Pass
- filters preserved through passing mutation flows: Pass
- navigation context preserved through passing mutation flows: Pass
- assignment correction continuity across all required scenarios: Fail (`pipeline-assignment`)

## 4. Intent Enforcement

- all exercised mutations still went through `ExecutionIntentViewModel`: Pass
- no direct UI CRUD path was observed: Pass
- no bypass path around `OnboardingExecutionService` was observed: Pass
- required replacement intent enforcement across blocker entry points: Fail for `pipeline-assignment`
  - blocker/root-cause flow still downgraded to `create-binding`

## 5. UX Validation

- `happy-binding-chain`: continuous problem → fix flow, no dead end: Pass
- `missing-root`, `permission-denied`, `tfs-unavailable`: explicit failure feedback, no contradictory success signal: Pass
- `team-assignment`: continuous replacement flow with visible selector and successful mutation: Pass
- `pipeline-assignment`: dead end from blocker path because the UI still directs the user into create-binding behavior instead of replacement behavior: Fail

## 6. Performance

- no UI freeze observed during exercised browser runs
- no excessive reload loop observed
- responsiveness remained acceptable in passing scenarios

Performance result: Pass

## 7. Final Verdict

- Production Ready: No

Exact defect:

1. `pipeline-assignment` still fails the certification gate because the blocker/root-cause entry path routes to `create-binding` instead of replacement intent, so the binding action zone opens create-binding UI and does not expose a replacement pipeline selector end-to-end.
2. The deterministic certification support suite is not fully green because `MockConfigurationSeedHostedServiceTests.StartAsync_WhenTeamAssignmentScenarioSelected_SeedsReachableAssignmentBlocker` still asserts the pre-replacement single-team seed shape.
