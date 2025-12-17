# Adding Features to PO Companion — Step-by-Step Guide

This document provides a comprehensive, mandatory process for adding features to the PO Companion repository.
All contributors—human or AI—MUST follow these steps.

---

## Prerequisites

Before starting ANY feature work, you MUST:

1. **Read and understand ALL governing documents:**
   - `docs/UX_PRINCIPLES.md` — UI/UX rules for Blazor WebAssembly
   - `docs/ARCHITECTURE_RULES.md` — Layer boundaries and architectural constraints
   - `docs/PROCESS_RULES.md` — Development workflow and review standards
   - `docs/COPILOT_ARCHITECTURE_CONTRACT.md` — Quick reference for AI agents

2. **Understand the repository structure:**
   ```
   PoTool.Core/        — Business logic, domain models, interfaces (infrastructure-free)
   PoTool.Api/         — ASP.NET Core Web API, SignalR, EF Core, TFS integration
   PoTool.Client/      — Blazor WebAssembly frontend
   PoTool.App/         — Shell application (MAUI)
   PoTool.Tests.Unit/  — MSTest unit tests
   ```

3. **Verify you have the required tools:**
   - .NET 10 SDK
   - Git
   - A text editor or IDE

---

## Step 1: Understand the Feature Request

### 1.1 Clarify Requirements
- Read the feature request thoroughly
- Identify the core goal (one feature = one goal)
- Note any ambiguities or missing information
- **If unclear, STOP and ask for clarification** (do not make assumptions)

### 1.2 Determine Scope
Ask yourself:
- What is the **single, clear purpose** of this feature?
- Does it introduce new UI components?
- Does it require backend logic?
- Does it need TFS integration?
- Does it require database changes?
- What layers will be affected? (Core / Api / Frontend / Shell)

### 1.3 Check for Rule Conflicts
Before proceeding, verify:
- [ ] The feature does not violate layer boundaries
- [ ] The feature uses approved Blazor UI components only
- [ ] The feature does not require direct TFS access from frontend
- [ ] The feature maintains dark-theme-only UI
- [ ] No new dependencies are needed (or if needed, document for approval)

**If any rule would be violated, STOP and discuss alternatives.**

---

## Step 2: Explore the Existing Codebase

### 2.1 Identify Similar Features
- Search for similar functionality already in the codebase
- Review how existing features are structured
- Identify patterns to follow or components to reuse

### 2.2 Understand Build and Test Infrastructure
Run these commands to understand the project:

```bash
# Navigate to repository root
cd /home/runner/work/PoCompanion/PoCompanion

# Build the solution
dotnet build

# Run unit tests
dotnet test

# Check for linting/formatting tools (if any)
# This project uses standard .NET conventions
```

### 2.3 Review Related Components
Examine:
- Existing Core contracts and interfaces
- Similar API controllers or services
- Existing Blazor components in the frontend
- Test examples for the areas you'll modify

**Store any important conventions you discover** using memory tools for future reference.

---

## Step 3: Design Your Implementation Plan

### 3.1 Define Changes by Layer

Create a checklist breaking down the work by architectural layer:

**Core Layer** (if needed):
- [ ] New interfaces or contracts
- [ ] Domain models or DTOs
- [ ] Business logic services
- [ ] Validation rules (FluentValidation)

**Api Layer** (if needed):
- [ ] New controllers or endpoints
- [ ] Service implementations
- [ ] SignalR hub methods
- [ ] Database entities and migrations
- [ ] Repository implementations
- [ ] TFS client integration

**Frontend Layer** (if needed):
- [ ] New Blazor components
- [ ] API client service calls
- [ ] UI state management
- [ ] CSS isolation for styling

**Tests**:
- [ ] Unit tests for Core business logic
- [ ] Integration tests for API (if applicable)
- [ ] No real TFS calls (use mocks/fakes)

### 3.2 Identify Duplication Risks
Review your plan and ask:
- Will any UI structures be repeated? → Extract to reusable component
- Will any backend logic be repeated? → Extract to Core service/helper
- Are you copying existing code? → Refactor instead

**Duplication is forbidden. Plan to eliminate it upfront.**

### 3.3 Create an Initial Report
Use the `report_progress` tool to share your plan as a checklist:

```markdown
- [ ] Phase 1: Core layer changes
  - [ ] Define interfaces
  - [ ] Create DTOs
- [ ] Phase 2: API layer changes
  - [ ] Implement controller
  - [ ] Add service
- [ ] Phase 3: Frontend changes
  - [ ] Create Blazor component
  - [ ] Wire up API calls
- [ ] Phase 4: Tests
  - [ ] Unit tests for business logic
  - [ ] Integration tests (if needed)
- [ ] Phase 5: Validation
  - [ ] Build and test
  - [ ] Code review
  - [ ] Security scan
```

---

## Step 4: Implement Changes (Minimal and Surgical)

### 4.1 Work in Small Increments
- Implement one layer at a time
- Start with Core (interfaces, models), then Api, then Frontend
- Test each layer as you complete it
- Use `report_progress` after completing each meaningful unit

### 4.2 Follow Coding Standards

**Core Layer Rules:**
- MUST NOT reference ASP.NET Core, EF Core, SignalR, HTTP, or TFS APIs
- MUST be fully unit-testable without infrastructure
- All business logic belongs here

**Api Layer Rules:**
- MAY reference Core
- MUST be the only layer that accesses TFS (via `ITfsClient`)
- MUST expose all functionality via Web API and SignalR
- MUST use source-generated Mediator library (not MediatR) if using mediator pattern
  - See ARCHITECTURE_RULES.md section 11 for approved Mediator library details
- Handlers and implementations live here

**Frontend Layer Rules:**
- MUST use Blazor WebAssembly
- MUST use approved open-source Blazor component libraries (MudBlazor, Radzen, Fluent UI)
- MUST NOT use custom JavaScript/TypeScript UI widgets
- MUST communicate with backend ONLY via HTTP Web API or SignalR
- MUST use CSS isolation per component
- MUST maintain dark-theme-only styling
- MUST use FluentValidation for forms

### 4.3 Avoid Common Mistakes
❌ **Do NOT:**
- Copy-paste code (extract shared logic instead)
- Add dependencies without approval
- Make cross-cutting changes without discussion
- Change behavior outside your stated goal
- Mix multiple features in one PR
- Use MediatR (use source-generated Mediator library instead—see ARCHITECTURE_RULES.md)
- Access TFS from frontend
- Add JavaScript UI widgets

✅ **Do:**
- Extract reusable components and services
- Minimize changes (surgical edits only)
- Follow existing patterns
- Test as you go
- Document your decisions

---

## Step 5: Write Tests

### 5.1 Unit Tests (Mandatory for Business Logic)
- Use **MSTest** framework
- Test all business logic in Core layer
- Use file-based mocks/fakes for TFS data
- MUST NOT call real TFS in tests

Example test structure:
```csharp
[TestClass]
public class MyFeatureServiceTests
{
    [TestMethod]
    public void MyMethod_WhenCondition_ExpectedResult()
    {
        // Arrange
        var mockDependency = new MockDependency();
        var service = new MyFeatureService(mockDependency);
        
        // Act
        var result = service.MyMethod(input);
        
        // Assert
        Assert.AreEqual(expected, result);
    }
}
```

### 5.2 Run Tests Frequently
```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter FullyQualifiedName~MyFeatureServiceTests

# Run tests with verbose output
dotnet test --verbosity normal
```

### 5.3 Update Existing Tests
If your changes affect existing behavior:
- Update related tests to reflect new behavior
- Do NOT delete tests without justification
- Ensure all tests pass before proceeding

---

## Step 6: Validate Your Changes

### 6.1 Build the Solution
```bash
cd /home/runner/work/PoCompanion/PoCompanion
dotnet build
```
Fix any build errors before proceeding.

### 6.2 Run All Tests
```bash
dotnet test
```
Ensure all tests pass. Fix any failures related to your changes.

### 6.3 Manual Verification
If your feature includes UI changes:

```bash
# Run the API
cd PoTool.Api
dotnet run

# In another terminal, run the Client
cd PoTool.Client
dotnet run
```

- Navigate to the feature in the browser
- Test the happy path
- Test error conditions
- Verify dark theme is preserved
- **Take screenshots of UI changes** for review

### 6.4 Review Your Changes
```bash
# Check git status
git --no-pager status

# Review diff
git --no-pager diff
```

Verify:
- Only intended files are changed
- No debug code or console logs remain
- No commented-out code
- No unnecessary whitespace changes

---

## Step 7: Request Code Review

### 7.1 Use the Code Review Tool
Before finalizing, request an automated code review:

```
Use code_review tool with:
- prTitle: "[Feature] Brief description"
- prDescription: "Description of changes and high-level implementation details"
```

### 7.2 Address Review Comments
- Read all feedback carefully
- Decide which comments are correct (the tool is imperfect)
- Fix legitimate issues
- If you make significant changes, request another review

### 7.3 Iterate Until Clean
Continue fixing issues and re-reviewing until:
- No blockers remain
- Major issues are resolved
- Minor issues are addressed or documented as follow-up

---

## Step 8: Run Security Scan

### 8.1 Use CodeQL Checker
After code review is complete, run the security scanner:

```
Use codeql_checker tool
```

### 8.2 Investigate All Alerts
For each alert discovered:
- Investigate the root cause
- Fix if it's a real vulnerability and requires only localized changes
- Document if it's a false positive
- Document if it cannot be fixed easily

### 8.3 Re-run After Fixes
After fixing vulnerabilities, re-run `codeql_checker` to verify fixes.

### 8.4 Create Security Summary
Include in your final PR description:
- List of vulnerabilities discovered (if any)
- Status of each (fixed / false positive / deferred with justification)

---

## Step 9: Final PR Submission

### 9.1 Update .gitignore if Needed
Ensure build artifacts and dependencies are excluded:
```
bin/
obj/
*.db
*.db-shm
*.db-wal
node_modules/
.vs/
```

### 9.2 Final Progress Report
Use `report_progress` one last time with:
- Commit message: Clear, single-line description
- PR description: Complete checklist showing all work completed

### 9.3 Verify PR Template Compliance
Your PR MUST address all items in `docs/pr_template.md`:

**Mandatory Checklist Items:**
- [ ] Single, clear purpose stated
- [ ] No scope creep
- [ ] UX principles followed
- [ ] UI rules followed (Blazor components, dark theme, CSS isolation)
- [ ] Architecture rules followed (layer boundaries, no TFS in frontend)
- [ ] Process rules followed (no implicit decisions, no unapproved dependencies)
- [ ] No code duplication
- [ ] Repeated UI extracted to components
- [ ] Repeated logic extracted to services
- [ ] Business logic unit tested
- [ ] No real TFS in tests
- [ ] Existing tests updated

### 9.4 Include Reviewer Notes
In your PR description, explain:
- What was intentionally NOT changed (and why)
- Known limitations or follow-up items needed
- Any trade-offs or design decisions made

---

## Step 10: Post-Merge Cleanup

### 10.1 Store Important Learnings
After completing the feature, use memory tools to store:
- Important conventions discovered
- Commands that worked well
- Patterns to follow for future features

**Examples:**
- "Use `ITfsClient` interface for all TFS operations"
- "Extract repeated Blazor table structures into `DataGridComponent`"
- "Build with `dotnet build` and test with `dotnet test`"

### 10.2 Update Documentation
If your feature changes public APIs or user-facing behavior:
- Update README.md
- Update relevant documentation in `docs/`
- Add examples if helpful

---

## Common Scenarios

### Adding a New API Endpoint

1. **Core Layer:**
   - Define request/response DTOs
   - Create interface for service (if complex logic)

2. **Api Layer:**
   - Implement service (if needed)
   - Add controller method
   - Wire up dependency injection

3. **Frontend Layer:**
   - Add API client method call
   - Update UI to use new endpoint

4. **Tests:**
   - Unit test service logic
   - Integration test API endpoint (optional)

### Adding a New UI Component

1. **Verify:** Check approved component libraries (MudBlazor, Radzen, Fluent UI) for existing solution
2. **Frontend Layer:**
   - Create Blazor component in `/Components`
   - Use CSS isolation (`.razor.css` file)
   - Ensure dark theme compatibility
   - Make component reusable (no hardcoded values)

3. **Use Component:**
   - Import in page or parent component
   - Pass data via parameters

### Adding TFS Integration

1. **Core Layer:**
   - Define interface extending `ITfsClient`
   - Create DTOs for TFS data

2. **Api Layer (ONLY):**
   - Implement TFS client interface
   - Add PAT authentication (encrypted)
   - Log all TFS mutations
   - Map TFS results to DTOs

3. **Frontend Layer:**
   - Call API endpoints (never TFS directly)

---

## Emergency Stops: When to STOP and Ask

You MUST stop and ask for clarification if:

❌ Requirements are incomplete or ambiguous  
❌ A rule would be violated  
❌ A new dependency seems necessary  
❌ A cross-layer or cross-cutting change is implied  
❌ Scope creep is detected  
❌ You're uncertain about architectural decisions  
❌ You're tempted to "work around" a limitation  

**Stopping is correct behavior. It prevents architectural debt.**

---

## Summary: Quick Reference Checklist

Use this checklist for every feature:

- [ ] Read all governing documents
- [ ] Understand and clarify requirements
- [ ] Explore existing codebase
- [ ] Design implementation plan (report initial progress)
- [ ] Implement changes (minimal, surgical, layer-by-layer)
- [ ] Extract any duplication into reusable components/services
- [ ] Write unit tests for business logic
- [ ] Build and run tests
- [ ] Manual verification (with screenshots for UI)
- [ ] Request code review (iterate on feedback)
- [ ] Run security scan (fix issues)
- [ ] Final progress report
- [ ] Verify PR template compliance
- [ ] Store learnings for future work

---

## Resources

- **Architecture:** `docs/ARCHITECTURE_RULES.md`
- **UX/UI:** `docs/UX_PRINCIPLES.md`
- **Process:** `docs/PROCESS_RULES.md`
- **PR Template:** `docs/pr_template.md`
- **Copilot Contract:** `docs/COPILOT_ARCHITECTURE_CONTRACT.md`

---

**Remember:** This repository prioritizes **correctness, clarity, and long-term maintainability** over speed.
Taking time to do it right is always better than introducing technical debt.
