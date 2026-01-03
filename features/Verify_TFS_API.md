```md
# Feature: Verify TFS API (Functional Compatibility & Safety Check)

## Purpose

Provide an **in-context verification capability** within the TFS Configuration View that validates whether the configured TFS / Azure DevOps Server instance supports **all API functionality used by the tool**, without risking production data.

The feature must:
- detect real integration incompatibilities
- clearly show which functionality will not work
- provide enough detail to fix configuration, permission, or implementation issues
- verify write behavior safely and explicitly

Verification is a core part of configuration, not a separate tool.

---

## Placement

- The Verify TFS API feature is an **integrated action in the TFS Configuration View**.
- It complements the existing **Test Connection** action.
- It MUST use the current configuration values in the view, even if not yet saved.

---

## User interaction

### Controls

Located in the **TFS Configuration View**:

- Button: **Verify TFS API**
- Optional section (only visible if the tool performs write operations):
  - Toggle: **Include write checks**
  - Input: **Work Item ID** (optional, user-provided)

### User flow

1. User configures or adjusts TFS settings.
2. User clicks **Verify TFS API**.
3. Read-only checks run automatically.
4. If **Include write checks** is enabled:
   - user must explicitly confirm execution
   - checks operate only on the user-provided scope or temporary artefacts

---

## Execution behavior

- Verification runs against the real TFS server using real credentials.
- All checks use the same integration layer as runtime behavior.
- Failures in one check MUST NOT stop other checks.
- Checks are deterministic and isolated per capability.

---

## Verification coverage

### A. Read-only checks (always executed)

- Server reachability and authentication
- Project existence and accessibility
- Area Path and Iteration Path readability (if used)
- Process-template assumptions (required fields, work item types)
- WIQL queries used by the tool
- Required field readability
- Batch reads and pagination behavior
- Advanced reads (revisions, relations, attachments) if used

### B. Write checks (explicit opt-in only)

Write checks MUST be executed as separate, clearly labeled checks.

Supported modes:
- **User-scoped update check**  
  - operates on a user-provided Work Item ID
  - performs non-destructive, idempotent updates only
- **Temporary artefact check**  
  - creates tool-owned verification artefacts with prefix `VERIFY-*`
  - cleans up automatically when possible

Write checks MUST be skipped entirely if the tool does not perform write operations.

---

## Diagnostic report

The Verify TFS API MUST return a **human-readable, technically complete diagnostic report**.

### Per capability, the report includes:

- **CapabilityId**
- **ImpactedFunctionality**
- **ExpectedBehavior**
- **ObservedBehavior**
- **FailureCategory**
- **RawEvidence (sanitized)**
- **LikelyCauses**
- **ResolutionGuidance**

### Additional fields for write checks:

- **TargetScope**
- **MutationType**
- **CleanupStatus**

Reports MUST be available in Markdown or structured JSON and copyable without loss of information.

---

## Safety constraints

- No secrets, tokens, or headers may appear in output.
- No user-created artefacts may be deleted.
- No production data may be modified without explicit user consent.
- The tool MUST NOT auto-select work items for write checks.

---

## Architectural constraints

- Verification MUST reuse the runtime TFS integration layer.
- Verification MUST reuse the same auth and configuration pipeline as Test Connection.
- No mocks or simulated responses are allowed.

---

## Acceptance criteria

- Verification is accessible directly from the TFS Configuration View.
- The report clearly explains which functionality will not work and why.
- Write behavior is verified only with explicit user control.
- A developer can fix integration issues using the report alone.
- Adding new TFS-dependent features requires extending the verification suite.
```
