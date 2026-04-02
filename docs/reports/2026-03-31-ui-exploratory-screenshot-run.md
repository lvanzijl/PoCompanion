# UI exploratory screenshot run

- Date/time of run: 2026-03-31T16:14Z to 2026-03-31T16:31Z
- Repository: `/home/runner/work/PoCompanion/PoCompanion`
- How the app was started:
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --configuration Debug --nologo`
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Client/PoTool.Client.csproj --configuration Debug --nologo`
  - `dotnet run --project /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --launch-profile http --no-build`
  - `dotnet run --project /home/runner/work/PoCompanion/PoCompanion/PoTool.Client/PoTool.Client.csproj --launch-profile http --no-build`
- Base URL used: `http://localhost:5292`
- Mock/demo data mode:
  - API development configuration had `TfsIntegration:UseMockClient=true`
  - The run used seeded mock data and auto-selected mock profile `Commander Elena Marquez` after skipping onboarding

## Summary

- Pages attempted: 20
- Pages successfully captured: 18
- Pages failed: 2
- Screenshots produced: 20
- Issues found: 12

## Page coverage

| Order | Page/Area | Route or Navigation Path | Screenshot | Status | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | Onboarding overlay | `/` -> `/onboarding` | `01-onboarding-overlay.png` | Partial | First-run wizard rendered over the full shell; workspace navigation was visible behind the modal. |
| 2 | Sync gate | Skip wizard -> `/sync-gate?returnUrl=%2Fhome` | `02-sync-gate.png` | Success | Mock profile `Commander Elena Marquez` synced successfully after a long staged load. |
| 3 | Home dashboard | `/home` | `03-home-dashboard.png` | Success | Home hub loaded with product chips, mock metrics, workspace tiles, and quick actions. |
| 4 | Health hub | Home tile -> `/home/health` | `04-health-hub.png` | Success | Production-shaped hub with overview, validation triage, and backlog health entry tiles. |
| 5 | Health overview | Health hub -> Overview -> `/home/health/overview` | `05-health-overview-error.png` | Failed | Page shell loaded, but core Build Quality content failed with a client deserialization error despite HTTP 200. |
| 6 | Validation triage | Health hub -> Validation Triage -> `/home/validation-triage` | `06-validation-triage.png` | Success | Strong category-card page with counts and clear next actions. |
| 7 | Validation queue | Validation Triage -> Open queue -> `/home/validation-queue?category=SI` | `07-validation-queue.png` | Success | Queue page loaded with grouped rule cards and actionable fix-session links. |
| 8 | Validation fix session | Validation Queue -> Start fix session -> `/home/validation-fix?category=SI&ruleId=SI-3` | `08-validation-fix-session.png` | Success | Detailed record view loaded with breadcrumbs, state chips, long issue explanation, and actions. |
| 9 | Delivery hub | Top nav -> `/home/delivery` | `09-delivery-hub.png` | Success | Production-shaped delivery hub with three focused entry tiles. |
| 10 | Sprint delivery | Delivery hub -> Sprint Delivery -> `/home/delivery/sprint` | `10-sprint-delivery-error.png` | Failed | Page shell loaded, but Build Quality section failed with a client deserialization error despite HTTP 200. |
| 11 | Sprint execution | Direct route `/home/delivery/execution` | `11-sprint-execution-empty.png` | Partial | Reached page successfully, but it showed `No sprints found` while team/product context controls were present. |
| 12 | Trends workspace | Direct route `/home/trends` | `12-trends-workspace.png` | Success | Loaded with team filter, trend signal tiles, and a bug trend visualization after async data settled. |
| 13 | Pipeline insights | Trends -> Pipeline Insights -> `/home/pipeline-insights` | `13-pipeline-insights-empty.png` | Partial | Filter panel rendered, but page stayed in a `select team and sprint` empty state; trend tile had already logged pipeline signal load problems. |
| 14 | Planning hub | Direct route `/home/planning` | `14-planning-hub.png` | Success | Structurally consistent planning hub with roadmaps and plan board entry tiles. |
| 15 | Product roadmaps overview | Planning -> Product Roadmaps -> `/planning/product-roadmaps` | `15-product-roadmaps-empty.png` | Partial | Page loaded but both products had `0 roadmap epics`; warnings were logged while edit/reporting affordances stayed visible. |
| 16 | Roadmap editor | Product Roadmaps -> Edit roadmap -> `/planning/product-roadmaps/1` | `16-roadmap-editor.png` | Success | Loaded dense planning editor with search, available-epics list, and an empty roadmap drop area. |
| 17 | Backlog health | Direct route `/home/backlog-overview` | `17-backlog-health-context-gate.png` | Partial | Reachable page required explicit product selection and did not inherit existing context. |
| 18 | Portfolio delivery | Direct route `/home/delivery/portfolio` | `18-portfolio-delivery-context-gate.png` | Partial | Reachable page required team selection before any sprint data would load. |
| 19 | PR overview | Direct route `/home/pull-requests` | `19-pr-overview.png` | Success | Dense PR insights page loaded successfully; accessibility snapshot was much larger than hub pages, indicating a richer detail surface. |
| 20 | Plan board | Direct route `/planning/plan-board` | `20-plan-board-context-gate.png` | Partial | Reachable planning page gated on product selection; `Refresh from TFS` was disabled in mock/local mode. |

## Findings

### Critical

None observed in this run.

### Major

1. **Health Overview broken by deserialization failure**  
   - Route: `/home/health/overview`  
   - Observed behavior: page frame loads, but the main Build Quality content is replaced by a deserialization error while the response status is still `200`.  
   - Impact: primary content for the page is unavailable.

2. **Sprint Delivery broken by deserialization failure**  
   - Route: `/home/delivery/sprint`  
   - Observed behavior: the Build Quality section fails with a similar deserialization error while the page otherwise loads.  
   - Impact: a major delivery view is partially unusable.

3. **Several pages require manual context reselection instead of inheriting known context**  
   - Routes: `/home/backlog-overview`, `/home/delivery/portfolio`, `/planning/plan-board`, `/home/pipeline-insights`, `/home/delivery/execution`  
   - Observed behavior: these pages render successfully but stop at product/team/sprint selection prompts or related context-dependent empty states even after the session already has an active profile and previously selected context.  
   - Impact: inconsistent navigation flow and reduced exploratory testability.

### Minor

1. **Onboarding modal overlays a fully rendered app shell**  
   Workspace navigation and global actions are visible behind the first-run wizard, which makes the initial experience look structurally inconsistent.

2. **Skipping onboarding appears to auto-select an existing mock profile**  
   After clicking `Skip Wizard`, the app went straight into sync for `Commander Elena Marquez` instead of clearly returning to `/profiles` for an explicit choice.

3. **Sprint Execution empty state may be misleading**  
   The page shows a team/product context control but still says `No sprints found`, which feels disconnected from the visible selectors.

4. **Product Roadmaps overview exposes edit/reporting affordances even with zero roadmap epics**  
   The page is usable, but the mock-data state makes it feel incomplete and warning-heavy.

5. **Persistent console noise during normal navigation**  
   The run consistently logged an invalid preload warning and a blocked Google Fonts request.

6. **Home dashboard triggered 404s for backlog-health metric requests**  
   Requests to `/api/Metrics/backlog-health?...` returned 404 while the page still rendered top-level tiles and metrics.

### Observations

1. The workspace hubs themselves are visually cohesive: Home, Health, Delivery, Trends, and Planning share consistent breadcrumbing, tile structure, and top navigation.
2. Validation flows were the strongest end-to-end experience in the run; triage, queue, and fix-session pages all loaded with meaningful mock content.
3. Trends workspace gave the best chart/visualization coverage in this run, especially after async signal loading completed.
4. Planning detail pages rely heavily on mock-data richness; the product-level roadmap editor felt substantially more complete than the roadmap overview page.
5. Direct route navigation is viable, but every direct load briefly returns to the generic WASM loading shell before the page settles.

## Unreachable or ambiguous pages

- No visited target page was completely unreachable.
- Some likely major pages were not included in the final screenshot set because this run prioritized broad representative coverage over every trends subpage:
  - `/home/pr-delivery-insights`
  - `/home/portfolio-progress`
  - `/home/trends/delivery`
  - `/home/bugs`
- These routes remain good follow-up candidates for a second-pass screenshot run.

## Gaps in mock data or testability

- Several delivery/planning/health pages require explicit product, team, or sprint selection even when the session already has an active mock profile and prior context.
- Product roadmap overview had no roadmap epics seeded for the visited products, limiting meaningful verification of ordering/reordering behavior.
- Pipeline Insights and Portfolio Delivery appear especially sensitive to missing or unselected team/sprint context.
- Some feature pages return client-side errors caused by shape mismatches or empty/unsupported payloads rather than presenting a graceful empty state.

## Recommended next actions

1. Fix the client/API contract mismatch causing the Build Quality deserialization failures on Health Overview and Sprint Delivery.
2. Decide which pages should inherit active profile/product/team context automatically and make that behavior consistent.
3. Improve empty-state guidance where selectors are required so the next action is explicit and clearly connected to the available controls.
4. Review the onboarding skip flow to ensure it does not silently choose a mock profile unless that is intentional.
5. Add a second exploratory pass for the remaining trends subpages and bug pages once the major data-contract issues are fixed.

## Final inventory of screenshot files saved in `docs/testing/screenshots/`

- `01-onboarding-overlay.png`
- `02-sync-gate.png`
- `03-home-dashboard.png`
- `04-health-hub.png`
- `05-health-overview-error.png`
- `06-validation-triage.png`
- `07-validation-queue.png`
- `08-validation-fix-session.png`
- `09-delivery-hub.png`
- `10-sprint-delivery-error.png`
- `11-sprint-execution-empty.png`
- `12-trends-workspace.png`
- `13-pipeline-insights-empty.png`
- `14-planning-hub.png`
- `15-product-roadmaps-empty.png`
- `16-roadmap-editor.png`
- `17-backlog-health-context-gate.png`
- `18-portfolio-delivery-context-gate.png`
- `19-pr-overview.png`
- `20-plan-board-context-gate.png`
