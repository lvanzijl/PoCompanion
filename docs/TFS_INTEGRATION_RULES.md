# TFS Integration Rules

## 1. Scope and Purpose

This document defines binding rules for integrating with Azure DevOps Server (TFS, on-prem) from the PO Companion application.

These rules:

- Are time-independent
- Remain valid regardless of implementation progress
- Apply to all current and future TFS-related code
- Override examples, roadmaps, summaries, and status documents

Any deviation requires an explicit, documented exception.

---

## 2. Integration Boundaries

1. Integration with TFS is allowed exclusively via supported APIs.
2. Direct database access to TFS is forbidden.
3. The integration layer is a strict boundary:
   - UI code MUST NOT call TFS APIs directly
   - Domain/Core logic MUST NOT depend on TFS-specific models
4. All TFS-specific concerns MUST be isolated in a dedicated integration layer.

---

## 3. Authentication and Authorization

1. Authentication to TFS MUST use one of the supported mechanisms:
   - NTLM authentication (on-prem environments)

2. The chosen authentication mechanism:
   - MUST be configurable
   - MUST NOT be hardcoded
   - MUST NOT leak outside the integration layer

   Rules:
   - PATs MUST be stored client-side only
   - PATs MUST NEVER be persisted server-side (database, cache, files, logs)
   - The API may receive a PAT only for immediate use or validation
   - Storage mechanism, encryption, lifecycle, and XSS mitigations are defined outside this document

4. NTLM-specific rules:
   - Credentials MUST NEVER be logged
   - Credential handling MUST rely on underlying platform security
   - Interactive login MUST NOT be assumed

5. The integration layer MUST assume:
   - Credentials can expire or be revoked
   - Authentication can fail at any time
   - Failures MUST be detected and surfaced explicitly

6. All TFS calls MUST handle authentication and authorization failures predictably and consistently.

---

## 4. API Usage Rules

1. All communication with TFS MUST use documented REST APIs.
2. Undocumented or internal endpoints are forbidden.
3. API calls MUST:
   - Be version-tolerant
   - Avoid reliance on undocumented response fields
4. WIQL queries MUST:
   - Be parameterized
   - Be compatible with on-prem Azure DevOps Server
5. API contracts MUST be treated as unstable inputs, never as domain truth.

---

## 5. API Versioning

1. All Azure DevOps Server REST API calls MUST explicitly specify api-version=7.0.
2. Reliance on server-default API versions is forbidden.
3. Mixing API versions within the same integration is forbidden.
4. Any API version change requires:
   - Validation against the target server version
   - Documentation update
   - Regression verification

---

## 6. Data Ownership and Mapping

1. TFS is an external system of record, not the domain model.
2. All TFS data MUST be mapped into internal canonical models.
3. Internal models:
   - MUST NOT expose raw TFS field names
   - MUST NOT leak TFS concepts outside the integration layer
4. Field mappings MUST be explicit, centralized, and versioned.
5. Missing or unknown fields MUST be handled gracefully.

---

## 7. Read Operations

1. Read operations MUST be idempotent and safe to retry.
2. Pagination MUST always be handled explicitly.
3. Large result sets MUST NEVER be loaded unbounded.
4. Query performance MUST NOT rely on dataset size, team count, or work item count.
5. Filtering MUST be applied as early as possible.

---

## 8. Write Operations

1. Write operations MUST be explicitly scoped and minimally invasive.
2. The integration MUST NEVER:
   - Perform bulk writes without safeguards
   - Implicitly mutate unrelated fields
3. Partial updates MUST:
   - Specify exact fields being changed
   - Avoid overwriting concurrent changes where possible
4. Write failures MUST be detectable and recoverable.

---

## 9. Verify TFS API – diagnostic and write-verification rules

### 9.1 Purpose

The Verify TFS API MUST provide a **clear, exhaustive diagnostic report** that:

- is readable and understandable by humans
- contains sufficient technical detail to fix TFS integration issues
- explicitly shows **which tool functionality will not work** as a result of each failure

The report is a first-class artefact and part of the product.

---

### 9.2 Configuration-view integration (mandatory)

The Verify TFS API is an **integral part of the TFS Configuration View**.

Rules:
- Verify TFS API MUST NOT exist as a standalone feature.
- It MUST be accessible from the same view used to configure:
  - TFS base URL
  - project / collection
  - authentication
- Verification MUST use the current (even unsaved) configuration values, identical to “Test Connection”.

---

### 9.3 Diagnostic content per failed capability

For every failed verification check, the report MUST include:

- **CapabilityId**  
  Stable identifier mapping 1:1 to a concrete TFS capability.

- **ImpactedFunctionality**  
  Explicit description of which user-visible functionality will not work or will be degraded.

- **ExpectedBehavior**  
  What the tool assumes the TFS server can do.

- **ObservedBehavior**  
  What actually happened during verification.

- **FailureCategory** (enum)  
  One of:
  - Authentication  
  - Authorization  
  - EndpointUnavailable  
  - UnsupportedApiVersion  
  - MissingField  
  - InvalidProcessTemplate  
  - QueryRestriction  
  - PayloadShapeMismatch  
  - RateLimit  
  - Unknown

- **RawEvidence (sanitized)**  
  - HTTP status code  
  - TFS error code if present  
  - truncated response or error message  
  - request intent (no URLs, headers, or secrets)

- **LikelyCauses**  
  Ordered list of plausible causes.

- **ResolutionGuidance**  
  Concrete steps to restore compatibility.

---

### 9.4 Write-operation verification (safeguards)

Write operations MUST be verified without risking production data.

Rules:

- Write-related verification MUST be implemented as **separate, explicitly labeled checks**.
- Write checks MUST NOT run as part of the default verification.
- Write checks MUST require explicit user opt-in.

User-controlled scope:
- The user MUST be able to provide a specific **Work Item ID** to be used for non-destructive update checks, **or**
- explicitly allow creation of temporary verification artefacts.

Safety constraints:
- The tool MUST NOT auto-select or guess production work items.
- Temporary artefacts MUST use a deterministic prefix (e.g. `VERIFY-*`).
- User-created artefacts MUST NEVER be deleted.
- Cleanup MUST be attempted and reported.

---

### 9.5 Additional diagnostics for write checks

For each write verification check, the report MUST additionally include:

- **TargetScope**  
  Description of which work item(s) were affected.

- **MutationType**  
  Create / Update / Link / Close / Other.

- **CleanupStatus**  
  CleanedUp / NotRequired / Failed / Skipped.

This information MUST be visible before raw technical evidence.

---

### 9.6 Determinism, safety, and enforcement

- Output MUST be deterministic across runs given the same server state.
- Reports MUST NOT include secrets or credentials.
- If a feature introduces or changes TFS write behavior and:
  - no corresponding write verification exists, or
  - write verification is unsafe or implicit

then the feature is incomplete and MUST NOT be merged.

---

## 10. Error Handling and Resilience

1. All TFS calls MUST include explicit error handling and categorization.
2. Errors MUST distinguish between:
   - Authentication errors
   - Authorization errors
   - Validation errors
   - Connectivity errors
   - Server-side failures
3. Transient failures MUST be retryable.
4. Non-transient failures MUST surface actionable diagnostics.

---

## 11. Caching and Consistency

1. Caching is allowed but never authoritative.
2. Cached TFS data MUST have clear invalidation rules.
3. The system MUST tolerate stale reads and partial refreshes.
4. No logic may assume cached data reflects real-time TFS state.

---

## 12. Performance Constraints

1. Integration performance MUST degrade predictably.
2. No single TFS call may block the UI thread or critical workflows.
3. Long-running operations MUST be cancellable and provide feedback.
4. Performance optimizations MUST NOT violate correctness or isolation rules.

---

## 13. Observability and Diagnostics

1. All TFS interactions MUST be observable.
2. Logs MUST include:
   - Correlation identifiers
   - Operation type
   - Failure category
3. Sensitive data MUST NEVER be logged.
4. Diagnostics MUST support post-mortem analysis without TFS access.

---

## 14. Testing Rules

1. TFS integration code MUST be testable without a live TFS instance.
2. Tests MUST include contract, error-path, and boundary tests.
3. Mock data MUST reflect realistic and malformed scenarios.
4. Tests MUST NOT depend on network or TFS availability.

---

## 15. Versioning and Evolution

1. API behavior changes between server versions MUST be assumed.
2. Version-specific logic MUST be isolated, explicit, and documented.
3. Backward compatibility MUST be preserved unless explicitly broken.

---

## 16. Prohibited Practices

The following are forbidden:

- Hardcoding organization, project, or collection names
- Assuming single-team or single-project setups
- Using TFS-specific IDs outside the integration layer
- Treating TFS as synchronous or always available
- Encoding business logic in TFS field configuration

---

## 17. Relationship to Other Documents

- This document is authoritative for TFS integration rules
- Roadmaps, plans, and summaries are non-authoritative
- Architectural constraints: see ARCHITECTURE_RULES.md

If a document conflicts with this one, this document prevails.
