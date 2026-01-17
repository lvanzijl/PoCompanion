# EF Core Concurrency Rules (Non-Negotiable)

## Purpose
Prevent DbContext concurrency errors by construction.
EF Core DbContext instances are **not thread-safe**. Any overlap of EF operations on the same instance is a bug.

These rules apply to **all production code**.

---

## Core Rule (Invariant)

> **No EF Core operation may run concurrently on the same DbContext instance.**

If this invariant is violated, the implementation is incorrect, regardless of whether it “usually works”.

---

## Allowed Parallelism

Parallelism is allowed **only** for:
- HTTP / network calls
- CPU-bound computation
- In-memory DTO processing

Parallelism is **not** allowed for:
- DbContext usage
- Repository calls
- LINQ-to-Entities queries
- SaveChanges / SaveChangesAsync

---

## Mandatory Two-Phase Pattern

All sync / aggregation / ingestion flows **must** follow this structure:

### Phase 1 — Collect (Parallel Allowed)
- Network calls only
- CPU-only processing
- Results stored as DTOs or value objects
- **No EF Core access**

### Phase 2 — Persist (Sequential Only)
- Single DbContext instance
- Fully awaited EF operations
- No `Task.WhenAll`
- No parallel loops
- Prefer one atomic `SaveChangesAsync`

Violations of this pattern are architectural defects.

---

## Forbidden Patterns

The following are **explicitly forbidden**:

- Calling EF Core inside:
  - `Task.WhenAll`
  - `Parallel.ForEach`
  - throttlers / rate limiters
  - async `Select(...)`
  - background loops with concurrency
- Returning `IQueryable` from services
- Using `await foreach` over EF queries when another EF call may occur
- Injecting DbContext into:
  - singleton services
  - hosted/background services
- Caching DbContext or repositories in fields

---

## Required Materialization

- EF queries must be **fully materialized inside the data layer**
- Services return:
  - `List<T>`
  - `T`
  - DTOs
- Never return `IQueryable` across layer boundaries

---

## Configuration & Reference Data

- Configuration **must be loaded once** before any parallel execution
- Configuration must be passed as:
  - DTO
  - snapshot object
- Configuration services may not hit EF inside parallel flows

If a configuration service touches EF:
- It is **sequential-only**
- It must be called before parallel work starts

---

## DbContext Lifetime Rules

- DbContext lifetime: **Scoped only**
- Any service injecting DbContext must also be scoped
- Hosted services / singletons must use:
  - `IDbContextFactory<T>`
  - or `IServiceScopeFactory`

---

## When DB Parallelism Is Truly Required (Rare)

If DB parallelism is unavoidable:
- Use `IDbContextFactory`
- One DbContext per task
- No entity instances may cross DbContext boundaries
- IDs/DTOs only

This requires explicit justification in code review.

---

## Enforcement Expectation

Any PR that violates these rules:
- Will be rejected
- Must be refactored before merge

These rules exist because DbContext concurrency bugs are **non-local, non-deterministic, and expensive**.
