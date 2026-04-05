# Context model enforcement — 2026-04-05

## Canonical context graph

### Canonical relationships
- **Product → Teams**
  - Canonical source: `ProductTeamLinkEntity`
  - A team is valid for a product only when a persisted product-team link exists.
- **Team → Sprints**
  - Canonical source: `SprintEntity.TeamId`
  - A sprint is valid for a team only when that sprint is owned by that team.
- **Product → Sprints**
  - Derived only through linked teams.
  - A sprint is valid for a product only when its owning team is linked to that product.

### Allowed combinations
- Product only
- Product + Team where team belongs to product
- Team only where the endpoint semantics allow team-driven scope
- Team + Sprint where sprint belongs to team
- Product + Sprint where sprint belongs to a team linked to product
- Product + Team + Sprint where both relationships hold

### Disallowed combinations
- Team outside selected product
- Sprint outside selected team
- Sprint outside selected product
- Product-scoped requests that require a concrete product but only have owner-derived/all-products scope

## ContextResolver design

### Centralized backend resolver
- Added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/ContextResolver.cs`
- Input:
  - normalized product selection
  - normalized team selection
  - normalized sprint selection
  - resolver flags for explicit product requirement and team-driven product derivation
- Output:
  - validated effective product scope
  - valid team universe for the selected products
  - validated sprint IDs
  - canonical validation result

### Enforcement points
- `SprintFilterResolutionService`
- `DeliveryFilterResolutionService`
- `PipelineFilterResolutionService`
- `PullRequestFilterResolutionService`

These services now delegate product/team/sprint graph validation to the same resolver instead of keeping their own scattered combination logic.

### Upstream rejection
- Controllers now reject invalid resolved context before handlers run:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/MetricsController.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/BuildQualityController.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/PipelinesController.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/PullRequestsController.cs`

## Replaced guardrails

### Replaced
- Distributed product/team/sprint consistency checks inside `SprintFilterResolutionService`
- Team-derived product guessing inside `PullRequestFilterResolutionService`
- Repeated controller-specific validation-message construction
- Client-side ad hoc product/team option logic spread between controls and defaults code

### Centralized replacements
- `ContextResolver` for API-side structural validation
- `GlobalFilterContextResolver` for client-side valid team-option filtering and state normalization

## Invalid states eliminated

### API
- Sprint-scoped requests can no longer reach handlers with:
  - sprint outside selected product
  - sprint outside selected team
  - team outside selected product
  - missing explicit product where the endpoint requires one

### Frontend
- Global filter team options are now constrained to the selected product’s linked teams
- Product changes now normalize team/sprint state so the UI cannot keep an invalid team under the new product
- Sprint options remain constrained by the selected team only

## Remaining edge cases
- Route/query strings can still describe invalid combinations, but they now fail structurally at API resolution instead of being interpreted downstream.
- Frontend option enforcement is currently centered in shared global filters; other future selectors must reuse the same resolver to stay canonical.
- Portfolio-style endpoints that do not use the Product/Team/Sprint graph were intentionally left outside this resolver.
