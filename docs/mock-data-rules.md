# Mock Data Generation Rules — PO Companion

> **Status**: MANDATORY  
> **Applies to**: All development, testing, and data generation work

This document defines the authoritative rules for generating mock data in PO Companion. All mock data generators, test fixtures, and development data MUST conform to these rules.

---

## Table of Contents

1. [Data Theme](#data-theme)
2. [Hierarchy Structure](#hierarchy-structure)
3. [Team Structure & Area Paths](#team-structure--area-paths)
4. [Status & State Rules](#status--state-rules)
5. [Iteration Paths](#iteration-paths)
6. [Effort Estimation](#effort-estimation)
7. [Dependencies & Links](#dependencies--links)
8. [Pull Requests](#pull-requests)
9. [Data Volume Requirements](#data-volume-requirements)
10. [Validation Rules](#validation-rules)

---

## 1. Data Theme

### 1.1 Theme Overview

All mock data MUST follow a consistent theme: **Battleship Incident Handling and Damage Control System**

This theme provides a realistic, cohesive context for all work items, making the data more relatable and easier to understand during testing and development.

### 1.2 Domain Context

The mock data represents a software solution for naval vessel incident management, including:

- **Incident Detection**: Fire detection, leakage monitoring, collision alerts
- **Incident Response**: Emergency protocols, crew notifications, response coordination
- **Personnel Management**: Crew safety tracking, injury reporting, medical response
- **Damage Assessment**: Hull integrity monitoring, compartment status, structural damage evaluation
- **Damage Control**: Repair coordination, resource allocation, priority management
- **Communication Systems**: Inter-department messaging, command center integration, emergency broadcasts
- **Reporting & Analytics**: Incident logs, damage reports, performance metrics

### 1.3 Naming Conventions

Work items should use terminology and language consistent with the battleship domain:

**Goals** - Strategic objectives (e.g., "Mission-Critical Incident Response Capability", "Comprehensive Damage Control System")

**Objectives** - Major capabilities (e.g., "Real-Time Fire Detection and Suppression", "Automated Hull Breach Response")

**Epics** - Large features (e.g., "Fire Detection System", "Crew Safety Management", "Damage Control Dashboard")

**Features** - Specific functionalities (e.g., "Smoke Sensor Integration", "Automated Evacuation Alerts", "Hull Pressure Monitoring")

**PBIs** - User stories (e.g., "As a damage control officer, I need to view compartment status in real-time")

**Bugs** - Issues (e.g., "Fire alarm not triggering in engine room", "Crew location tracker showing incorrect positions")

**Tasks** - Implementation work (e.g., "Configure sensor API endpoints", "Create alert notification component")

### 1.4 Example Work Item Titles

**Incident Types to Reference**:
- Fire incidents (engine room fire, electrical fire, fuel fire)
- Leakage incidents (hull breach, pipe rupture, coolant leak)
- Personnel incidents (crew injury, man overboard, medical emergency)
- Structural damage (collision damage, explosion damage, equipment failure)

**Example Goal**: "Mission-Ready Incident Response Platform"

**Example Objective**: "Rapid Fire Detection and Automated Suppression"

**Example Epic**: "Engine Room Fire Detection System"

**Example Feature**: "Multi-Sensor Fire Detection Network"

**Example PBI**: "As a damage control officer, I need to receive instant alerts when engine room temperature exceeds safe thresholds so I can initiate fire suppression protocols"

**Example Bug**: "Fire suppression system not activating when smoke density reaches critical level"

**Example Task**: "Implement temperature sensor data aggregation service"

---

## 2. Hierarchy Structure

### 2.1 Work Item Hierarchy

Mock data MUST follow this exact hierarchy:

```
Goals (10)
  └─→ Objectives (2-4 per goal, total ~30)
       └─→ Epics (2-5 per objective, total ~100)
            └─→ Features (3-7 per epic, total ~500)
                 └─→ Product Backlog Items (5-10 per feature, total ~3,000)
                 └─→ Bugs (1-3 per feature, total ~1,000)
                      └─→ Tasks (2-5 per PBI/Bug, total ~15,000)
```

### 2.2 Quantity Rules

| Work Item Type | Minimum | Target | Maximum |
|----------------|---------|--------|---------|
| Goals          | 10      | 10     | 10      |
| Objectives     | 25      | 30     | 35      |
| Epics          | 80      | 100    | 120     |
| Features       | 400     | 500    | 600     |
| PBIs           | 2,500   | 3,000  | 3,500   |
| Bugs           | 800     | 1,000  | 1,200   |
| Tasks          | 12,000  | 15,000 | 18,000  |

**Total Work Items**: Minimum 15,815 | Target 19,640 | Maximum 23,455

### 2.3 Parent-Child Relationships

- **Goals**: No parent (top-level)
- **Objectives**: Must have exactly one Goal parent
- **Epics**: Must have exactly one Objective parent
- **Features**: Must have exactly one Epic parent
- **PBIs**: Must have exactly one Feature parent
- **Bugs**: Must have exactly one Feature parent
- **Tasks**: Must have exactly one PBI or Bug parent

**Critical Rule**: Every work item (except Goals) MUST have a valid parent reference.

---

## 3. Team Structure & Area Paths

### 3.1 Team Hierarchy

Mock data MUST include 10-15 teams organized in the following hierarchy:

```
Portfolio Level (1 team)
  └─→ Program Level (2-3 teams)
       └─→ Feature Teams (6-9 teams)
       └─→ Shared Services (1-2 teams)
```

### 3.2 Example Team Structure

```
Battleship Systems (Portfolio)
  ├─→ Incident Detection (Program)
  │    ├─→ Fire Detection (Feature Team)
  │    ├─→ Leakage Monitoring (Feature Team)
  │    └─→ Collision Detection (Feature Team)
  ├─→ Incident Response (Program)
  │    ├─→ Emergency Protocols (Feature Team)
  │    ├─→ Crew Safety (Feature Team)
  │    └─→ Medical Response (Feature Team)
  ├─→ Damage Control (Program)
  │    ├─→ Hull Integrity (Feature Team)
  │    ├─→ Repair Coordination (Feature Team)
  │    └─→ Resource Management (Feature Team)
  └─→ Shared Services
       ├─→ Communication Systems (Shared Services Team)
       └─→ DevOps & Infrastructure (Shared Services Team)
```

### 3.3 Area Path Rules (CRITICAL)

**Rule 1: Epic Determines Ownership**
- Epics are assigned to feature teams
- The Epic's area path determines the owning team
- Example: `\Battleship Systems\Incident Detection\Fire Detection`

**Rule 2: Inheritance Below Epic (MANDATORY)**
- Features MUST inherit the area path from their parent Epic
- PBIs MUST inherit the area path from their parent Feature (and therefore Epic)
- Bugs MUST inherit the area path from their parent Feature (and therefore Epic)
- Tasks MUST inherit the area path from their parent PBI/Bug (and therefore Epic)

**Rule 3: NO Area Path Mixing Below Feature Level**
- Once an Epic is assigned to a team, ALL descendants MUST have the same area path
- Cross-team work is modeled through dependencies, NOT mixed area paths
- This rule is CRITICAL for clean team backlogs

**Rule 4: Area Path Format**
```
\<Project>\<Program>\<Team>
```

Example:
```
\Battleship Systems\Incident Detection\Fire Detection
\Battleship Systems\Incident Response\Crew Safety
\Battleship Systems\Shared Services\Communication Systems
```

### 3.4 Area Path Distribution

| Level           | Area Path Assignment              |
|-----------------|-----------------------------------|
| Goals           | Portfolio level (root)            |
| Objectives      | Program level                     |
| Epics           | Feature team level                |
| Features        | Inherit from Epic (MANDATORY)     |
| PBIs            | Inherit from Feature (MANDATORY)  |
| Bugs            | Inherit from Feature (MANDATORY)  |
| Tasks           | Inherit from parent (MANDATORY)   |

### 3.5 Validation Rules

Mock data generators MUST validate:
1. Every Epic has a valid feature team area path
2. Every Feature has the same area path as its parent Epic
3. Every PBI/Bug has the same area path as its parent Feature
4. Every Task has the same area path as its parent PBI/Bug
5. No area path mixing occurs below the Epic level

---

## 4. Status & State Rules

### 4.1 Valid States by Work Item Type

| Type      | Valid States                                          |
|-----------|-------------------------------------------------------|
| Goal      | Proposed, Active, Completed, Removed                  |
| Objective | Proposed, Active, Completed, Removed                  |
| Epic      | New, Active, Resolved, Closed, Removed                |
| Feature   | New, Active, Resolved, Closed, Removed                |
| PBI       | New, Approved, Committed, Done, Removed               |
| Bug       | New, Approved, Committed, Done, Removed               |
| Task      | To Do, In Progress, Done, Removed                     |

### 4.2 State Distribution (Realistic)

Target distribution for active work items:

| State Category | Percentage | Description                          |
|----------------|------------|--------------------------------------|
| Backlog/New    | 60-70%     | Not yet started                      |
| Active/In Progress | 15-25% | Currently being worked on            |
| Resolved/Done  | 10-15%     | Completed in current/recent sprints  |
| Closed         | 5-10%      | Completed and accepted               |
| Removed        | 1-2%       | Cancelled or removed                 |

### 4.3 Invalid States for Testing (10-15% of items)

Mock data MUST include intentionally invalid states to test detection features:

- **Invalid state transitions**: Items moved to states without following proper workflow
- **Orphaned completed work**: Tasks marked "Done" but parent PBI still "New"
- **Blocked items without reason**: Items in "Blocked" state without explanation
- **State-sprint mismatches**: Items marked "Done" but in future sprint

**Target**: 10-15% of work items should have intentionally invalid or suspicious states

### 4.4 State Consistency Rules

- **Closed Epics**: Should have all Features closed or removed
- **Closed Features**: Should have all PBIs/Bugs closed or removed
- **Done PBIs/Bugs**: Should have all Tasks done or removed
- **Removed Items**: Children should also be removed (or reassigned)

**For Testing**: Include 5-10% violations of these rules to test detection logic

---

## 5. Iteration Paths

### 5.1 Sprint Structure

Mock data MUST include:
- **1 Backlog** (no sprint)
- **6-10 Sprints** (time-boxed iterations)

Example:
```
\Battleship Systems
  ├─→ Backlog
  ├─→ Sprint 1 (2025-01-01 to 2025-01-14)
  ├─→ Sprint 2 (2025-01-15 to 2025-01-28)
  ├─→ Sprint 3 (2025-01-29 to 2025-02-11)
  ├─→ Sprint 4 (2025-02-12 to 2025-02-25)
  ├─→ Sprint 5 (2025-02-26 to 2025-03-11)
  ├─→ Sprint 6 (2025-03-12 to 2025-03-25)
  ├─→ Sprint 7 (2025-03-26 to 2025-04-08)
  ├─→ Sprint 8 (2025-04-09 to 2025-04-22)
  └─→ Sprint 9 (2025-04-23 to 2025-05-06)
```

### 5.2 Sprint Duration

- **Standard**: 2 weeks (10 business days)
- **Allowed**: 1-4 weeks
- **Recommended**: Consistent duration across all sprints

### 5.3 Iteration Path Assignment

| Work Item Type | Iteration Path Assignment                  |
|----------------|--------------------------------------------|
| Goal           | No iteration (strategic level)             |
| Objective      | No iteration (strategic level)             |
| Epic           | Backlog or spanning multiple sprints       |
| Feature        | Backlog or target sprint                   |
| PBI            | Backlog or specific sprint                 |
| Bug            | Backlog or specific sprint                 |
| Task           | Same as parent PBI/Bug                     |

### 5.4 Distribution Across Sprints

**Backlog**: 60% of PBIs and Bugs
- These items are not yet scheduled for any sprint
- Valid states: New, Approved

**Sprints 1-3**: 20% of PBIs and Bugs (past/current sprints)
- Mix of In Progress, Done, and Closed states
- Should include some carryover items

**Sprints 4-6**: 15% of PBIs and Bugs (near-term sprints)
- Mostly Approved or Committed states
- Some may already be In Progress

**Sprints 7+**: 5% of PBIs and Bugs (future sprints)
- Mostly New or Approved states
- Placeholder assignments

### 5.5 Sprint Capacity and Velocity

Each sprint should have:
- **Total Effort**: 80-150 story points (across all teams)
- **Per Team**: 20-40 story points per feature team
- **Velocity Variation**: ±20% between sprints (realistic variation)

---

## 6. Effort Estimation

### 6.1 Estimation Scale

Use **Fibonacci sequence** for story points:

```
1, 2, 3, 5, 8, 13, 21
```

### 6.2 Estimation by Work Item Type

| Type      | Estimation Method      | Typical Range |
|-----------|------------------------|---------------|
| Goal      | Not estimated          | N/A           |
| Objective | Not estimated          | N/A           |
| Epic      | Sum of Features        | 50-200        |
| Feature   | Sum of PBIs            | 20-100        |
| PBI       | Story points           | 1-13          |
| Bug       | Story points           | 1-8           |
| Task      | Hours (optional)       | 0-16 hours    |

### 6.3 Estimation Distribution

**PBIs**:
- 1 point: 20%
- 2 points: 15%
- 3 points: 25%
- 5 points: 20%
- 8 points: 12%
- 13 points: 5%
- 21+ points: 3% (should be broken down)

**Bugs**:
- 1 point: 30%
- 2 points: 25%
- 3 points: 25%
- 5 points: 15%
- 8 points: 5%

### 6.4 Unestimated Items

**Rule**: 20-30% of backlog items should be unestimated
- New items not yet groomed
- Low-priority items not yet analyzed
- Future work not yet defined

**Distribution**:
- Backlog items: 30-40% unestimated
- Sprint items: 0-5% unestimated (should be estimated before sprint)
- Active items: 0% unestimated (must be estimated)

### 6.5 Estimation Accuracy

For realism, include:
- **Under-estimated items**: 10-15% of completed items (actual effort > estimate)
- **Over-estimated items**: 5-10% of completed items (actual effort < estimate)
- **On-target items**: 75-85% of completed items

---

## 7. Dependencies & Links

### 7.1 Dependency Types

Mock data MUST include these link types:

| Link Type         | Description                           | % of Total |
|-------------------|---------------------------------------|------------|
| Predecessor       | Must complete before dependent starts | 40%        |
| Successor         | Dependent that starts after current   | 40%        |
| Related           | Related but no blocking relationship  | 15%        |
| Duplicate         | Duplicate work items                  | 3%         |
| Parent-Child      | Hierarchy (already covered)           | N/A        |
| Tested By         | Test cases linked to PBIs             | 2%         |

### 7.2 Dependency Volume

**Minimum Requirements**:
- **10-15% of work items** should have at least one dependency link
- **Total dependency links**: 15,000-20,000 links
- **Average links per item**: 1-2 links

**Distribution**:
- Epics: 30-40% have dependencies
- Features: 20-30% have dependencies
- PBIs: 10-15% have dependencies
- Bugs: 5-10% have dependencies
- Tasks: 1-5% have dependencies

### 7.3 Cross-Team Dependencies (CRITICAL)

**Rule**: 30-40% of all dependencies MUST cross team boundaries

This means:
- Predecessor and successor are in different area paths
- Different feature teams involved
- Requires coordination across teams

**Example**:
```
PBI-1234 (Crew Safety team)
  └─→ Predecessor: PBI-987 (Hull Integrity team)
  └─→ Successor: PBI-2456 (Communication Systems team)
```

**Purpose**: 
- Realistic modeling of inter-team coordination
- Tests dependency visualization across teams
- Highlights integration points

### 7.4 Dependency Patterns

**Vertical Dependencies** (30%):
- Within same team
- Within same Epic
- Example: Feature A depends on Feature B in same Epic

**Horizontal Dependencies** (40%):
- Across teams
- Across Epics
- Example: Fire Detection API must be ready before Damage Control Dashboard can display alerts

**External Dependencies** (10%):
- Dependencies on external systems
- Dependencies on third-party libraries
- Dependencies on naval equipment manufacturers or sensor suppliers

**Circular Dependencies** (5%, for testing):
- A depends on B, B depends on C, C depends on A
- Should be detected as invalid

**Orphaned Dependencies** (5%, for testing):
- Links to non-existent work items
- Links to removed work items
- Should be detected as invalid

### 7.5 Blocked Items

**Rule**: 3-5% of active work items should be marked as "Blocked"

**Blocked Reasons**:
- Waiting for dependency: 60%
- Technical issue: 20%
- Waiting for decision: 15%
- Resource unavailable: 5%

**Blocked States**:
- PBIs: "Committed" but blocked
- Tasks: "In Progress" but blocked
- Should have "Blocked" tag or reason field

### 7.6 Invalid Dependencies for Testing

Include these intentionally invalid scenarios (5-10% of dependencies):

- **Circular dependencies**: A → B → C → A
- **Orphaned links**: Links to non-existent work items
- **Self-dependencies**: Work item depends on itself
- **Invalid state dependencies**: Done item depends on Removed item
- **Temporal violations**: Item in Sprint 2 depends on item in Sprint 5

**Purpose**: Test detection and validation features

---

## 8. Pull Requests

### 8.1 Volume Requirements

Mock data MUST include **at least 100 pull requests** with full metadata.

**Target**: 100-200 PRs

### 8.2 PR Metadata (Full)

Each PR MUST include:

| Field                | Required | Example Values                        |
|----------------------|----------|---------------------------------------|
| PR ID                | Yes      | 1001, 1002, 1003                      |
| Title                | Yes      | "Add user authentication"             |
| Description          | Yes      | Detailed description with context     |
| Status               | Yes      | Active, Completed, Abandoned          |
| Created Date         | Yes      | 2025-01-15T10:30:00Z                  |
| Creator              | Yes      | john.doe@example.com                  |
| Source Branch        | Yes      | feature/user-auth                     |
| Target Branch        | Yes      | main, develop                         |
| Repository           | Yes      | PO-Companion-Backend                  |
| Work Item Links      | Optional | [PBI-1234, PBI-1235]                  |
| Reviewers            | Yes      | [jane.smith, bob.johnson]             |
| Reviews              | Yes      | List of review records                |
| Comments             | Optional | List of comment threads               |
| Labels               | Optional | [bug-fix, high-priority]              |
| Merge Status         | Yes      | Succeeded, Conflicts, Pending         |
| Merge Commit ID      | Optional | abc123def456 (if merged)              |

### 8.3 PR Status Distribution

| Status      | Percentage | Description                         |
|-------------|------------|-------------------------------------|
| Active      | 15-20%     | Currently open for review           |
| Completed   | 70-75%     | Merged and closed                   |
| Abandoned   | 5-10%      | Closed without merging              |

### 8.4 Review Metadata

Each PR should have 1-5 reviewers with:

| Field           | Required | Example Values                  |
|-----------------|----------|---------------------------------|
| Reviewer        | Yes      | jane.smith@example.com          |
| Vote            | Yes      | Approved, Rejected, Waiting     |
| Review Date     | Yes      | 2025-01-16T14:20:00Z            |
| Comments        | Optional | "LGTM", "Please fix tests"      |

**Vote Distribution**:
- Approved: 60-70%
- Approved with suggestions: 15-20%
- Waiting for author: 10-15%
- Rejected: 3-5%

### 8.5 Comment Threads

Each PR should have 0-20 comment threads:

| Field           | Required | Example Values                  |
|-----------------|----------|---------------------------------|
| Thread ID       | Yes      | 1, 2, 3                         |
| File Path       | Optional | src/auth/login.cs               |
| Line Number     | Optional | 42                              |
| Status          | Yes      | Active, Resolved, Closed        |
| Comments        | Yes      | List of comments in thread      |

**Comment Thread Distribution**:
- No comments: 20%
- 1-5 comments: 50%
- 6-10 comments: 20%
- 11-20 comments: 10%

### 8.6 Code Changes

Each PR should include:

| Metric              | Typical Range |
|---------------------|---------------|
| Files Changed       | 1-50          |
| Lines Added         | 10-500        |
| Lines Deleted       | 5-300         |
| Net Lines Changed   | 5-700         |

**Distribution**:
- Small PRs (1-50 lines): 40%
- Medium PRs (51-200 lines): 35%
- Large PRs (201-500 lines): 20%
- Very Large PRs (500+ lines): 5%

### 8.7 Work Item Links

**Rule**: 70-80% of PRs should be linked to at least one work item

**Distribution**:
- No links: 20-30%
- 1 link: 40-50%
- 2-3 links: 20-25%
- 4+ links: 5-10%

**Link Types**:
- PBIs: 60%
- Bugs: 30%
- Features: 8%
- Tasks: 2%

### 8.8 Labels

Common labels to include:
- `bug-fix` (30% of PRs)
- `feature` (40% of PRs)
- `refactoring` (10% of PRs)
- `documentation` (5% of PRs)
- `high-priority` (15% of PRs)
- `breaking-change` (5% of PRs)
- `needs-testing` (10% of PRs)

---

## 9. Data Volume Requirements

### 9.1 Minimum Viable Dataset

| Entity               | Minimum  | Target   | Maximum  |
|----------------------|----------|----------|----------|
| Work Items (Total)   | 15,815   | 19,640   | 23,455   |
| Teams                | 10       | 12       | 15       |
| Area Paths           | 10       | 12       | 15       |
| Iteration Paths      | 7        | 10       | 12       |
| Dependencies         | 1,500    | 2,000    | 3,000    |
| Dependency Links     | 15,000   | 20,000   | 30,000   |
| Pull Requests        | 100      | 150      | 200      |
| PR Reviews           | 200      | 400      | 600      |
| PR Comments          | 300      | 800      | 1,500    |

### 9.2 Performance Testing Dataset

For performance and scale testing:

| Entity               | Performance Test Target |
|----------------------|-------------------------|
| Work Items (Total)   | 50,000+                 |
| Dependencies         | 5,000+                  |
| Dependency Links     | 50,000+                 |
| Pull Requests        | 500+                    |

### 9.3 Data Generation Time

Targets for mock data generation:

- **Initial generation**: < 30 seconds for minimum dataset
- **Full generation**: < 2 minutes for target dataset
- **Performance dataset**: < 5 minutes

---

## 10. Validation Rules

### 10.1 Mandatory Validations

Mock data generators MUST validate:

1. **Hierarchy Integrity**:
   - Every child has a valid parent (except Goals)
   - No circular references
   - No orphaned work items

2. **Area Path Consistency**:
   - Every Epic has a feature team area path
   - Features, PBIs, Bugs, Tasks inherit area path from Epic
   - No area path mixing below Epic level

3. **Iteration Path Validity**:
   - All iteration paths exist in project
   - Dates are valid and non-overlapping
   - Sprint assignments are reasonable

4. **State Validity**:
   - States match work item type
   - State transitions are valid (with intentional exceptions)
   - State-sprint combinations are reasonable

5. **Estimation Validity**:
   - Fibonacci values used
   - Unestimated percentage within bounds
   - No negative values

6. **Dependency Integrity**:
   - Links reference valid work items
   - No self-dependencies (except for testing)
   - Cross-team percentage within bounds

7. **Pull Request Integrity**:
   - All required fields populated
   - Valid status values
   - Work item links reference valid items

### 10.2 Data Quality Metrics

Track these quality metrics:

| Metric                        | Target    |
|-------------------------------|-----------|
| Work items with valid parent  | 100%      |
| Area path consistency         | 85-90%*   |
| Estimated backlog items       | 70-80%    |
| PRs with work item links      | 70-80%    |
| Dependencies within teams     | 60-70%    |
| Dependencies cross-team       | 30-40%    |
| Invalid states (intentional)  | 10-15%    |

*Note: 10-15% intentional violations for testing

### 10.3 Validation Reports

Mock data generators MUST produce validation reports showing:

- Total work items by type
- Area path distribution
- Iteration path distribution
- Estimation coverage
- Dependency statistics
- Cross-team dependency percentage
- Invalid data percentage (for testing)
- PR statistics

---

## 11. Implementation Guidelines

### 11.1 Data Generation Strategy

**Recommended Approach**:

1. **Generate top-down**:
   - Start with Goals
   - Generate Objectives for each Goal
   - Generate Epics for each Objective
   - Assign Epics to teams (area paths)
   - Generate Features, PBIs, Bugs, Tasks (inheriting area paths)

2. **Apply distributions**:
   - Assign states according to distribution rules
   - Assign iteration paths according to distribution rules
   - Apply effort estimates

3. **Generate dependencies**:
   - Create intra-team dependencies first
   - Add cross-team dependencies (30-40%)
   - Add intentional invalid dependencies (5-10%)

4. **Generate PRs**:
   - Create PRs with full metadata
   - Link to work items
   - Add reviews and comments

5. **Validate**:
   - Run all validation rules
   - Generate validation report
   - Fix critical issues

### 11.2 Randomization Guidelines

Use controlled randomization:

- **Seed-based**: Use a consistent seed for reproducible data
- **Distribution-aware**: Follow percentage distributions
- **Realistic**: Use realistic names, dates, and descriptions
- **Varied**: Ensure good variety in data

### 11.3 Performance Considerations

- **Lazy generation**: Generate data on-demand when possible
- **Caching**: Cache generated data for reuse
- **Batch operations**: Generate in batches for efficiency
- **Indexing**: Ensure proper indexing for fast lookups

---

## 12. Examples

### 12.1 Example Epic with Children

```
Epic-101: "Engine Room Fire Detection System"
  Area Path: \Battleship Systems\Incident Detection\Fire Detection
  State: Active
  Iteration: Backlog
  
  └─→ Feature-201: "Multi-Sensor Temperature Monitoring"
      Area Path: \Battleship Systems\Incident Detection\Fire Detection (inherited)
      State: Active
      Iteration: Sprint 3
      
      └─→ PBI-301: "Implement real-time temperature sensor data aggregation"
          Area Path: \Battleship Systems\Incident Detection\Fire Detection (inherited)
          State: Committed
          Iteration: Sprint 3
          Effort: 5
          Dependencies:
            - Predecessor: PBI-205 "Configure sensor network infrastructure" (Communication Systems team) [Cross-team]
          
          └─→ Task-401: "Create temperature sensor API endpoints"
              Area Path: \Battleship Systems\Incident Detection\Fire Detection (inherited)
              State: Done
              Iteration: Sprint 3
              Remaining Work: 0 hours
```

### 12.2 Example Cross-Team Dependency

```
PBI-1234: "Display crew evacuation routes on damage control dashboard"
  Area Path: \Battleship Systems\Incident Response\Crew Safety
  Team: Crew Safety
  Dependencies:
    - Predecessor: PBI-987 "Create compartment status API for real-time hull integrity data"
      Area Path: \Battleship Systems\Damage Control\Hull Integrity
      Team: Hull Integrity [Cross-team dependency]
```

### 12.3 Example Pull Request

```
PR-1001: "Add fire suppression system activation middleware"
  Status: Completed
  Created: 2025-01-15T10:30:00Z
  Creator: john.doe@example.com
  Source Branch: feature/fire-suppression-activation
  Target Branch: main
  Repository: Battleship-Incident-Backend
  
  Work Items:
    - PBI-301 (Implement real-time temperature sensor data aggregation)
    - PBI-302 (Add automated fire suppression trigger logic)
  
  Reviewers:
    - jane.smith@example.com (Approved, 2025-01-16T14:20:00Z)
    - bob.johnson@example.com (Approved with suggestions, 2025-01-16T16:45:00Z)
  
  Code Changes:
    Files Changed: 12
    Lines Added: 245
    Lines Deleted: 78
    Net Change: +167
  
  Comments:
    - Thread 1 (Resolved): "Please add unit tests for suppression activation logic"
    - Thread 2 (Resolved): "Consider extracting sensor threshold validation to a helper method"
    - Thread 3 (Active): "Documentation needed for emergency override API"
  
  Labels:
    - feature
    - safety-critical
    - needs-testing
  
  Merge Status: Succeeded
  Merge Commit: abc123def456
  Merge Date: 2025-01-17T09:15:00Z
```

---

## 13. Compliance and Enforcement

### 13.1 Mandatory Compliance

These rules are **MANDATORY** for:
- All mock data generators
- All test fixtures
- All development data scripts
- All data generation tools

### 13.2 Review Requirements

Mock data generation code MUST be reviewed for:
- Compliance with all rules in this document
- Proper distribution percentages
- Valid hierarchy and relationships
- Correct area path inheritance
- Adequate cross-team dependencies
- Sufficient data volume

### 13.3 Updates and Maintenance

This document MUST be updated when:
- New work item types are added
- Team structure changes
- New dependency types are introduced
- PR metadata requirements change
- Data volume requirements change

---

## 14. Summary of Critical Rules

1. **Hierarchy**: 10 Goals → 30 Objectives → 100 Epics → 500 Features → 3,000 PBIs + 1,000 Bugs → 15,000 Tasks
2. **Area Paths**: Epic determines team ownership; all descendants MUST inherit the same area path
3. **No Area Path Mixing**: Below Epic level, area paths CANNOT be mixed or changed
4. **Cross-Team Dependencies**: 30-40% of dependencies MUST cross team boundaries
5. **Invalid Data for Testing**: 10-15% of data should have intentional issues
6. **Pull Requests**: Minimum 100 PRs with full metadata
7. **Dependency Links**: 15,000-20,000 total dependency links
8. **Data Volume**: Minimum 15,815 work items, target 19,640

---

## Document History

| Version | Date       | Changes                          | Author    |
|---------|------------|----------------------------------|-----------|
| 1.0     | 2026-01-01 | Initial version                  | System    |

---

**END OF DOCUMENT**
