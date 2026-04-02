# Branch Protection Handoff

## Protected branches

Configure branch protection for:

- `main`
- `release/**` when that branch pattern is part of repository policy

## Required status checks to configure now

Use these exact GitHub Actions job names:

- `Core Gate`

## Status checks recommended after short stabilization

Use these exact GitHub Actions job names:

- `API Contract Gate`

## Status checks that should remain visible but initially optional

Use these exact GitHub Actions job names:

- `Governance Gate`

## Critical warning

- Use the exact job names above as branch-protection required checks.
- Do **not** configure only the workflow name `Build and Test Gates`.
- Branch protection must target the individual job checks, because those are the operational contract names.

## Recommended GitHub branch-protection settings

For each protected branch or branch pattern:

1. Enable **Require a pull request before merging** if repository policy expects PR-only changes.
2. Enable **Require status checks to pass before merging**.
3. Add `Core Gate` as a required status check immediately.
4. Add `API Contract Gate` as a required status check after the short stabilization period.
5. Leave `Governance Gate` visible but optional during phase-in.
6. Enable **Require branches to be up to date before merging**.
7. Enable **Dismiss stale pull request approvals when new commits are pushed**.
8. Restrict direct pushes if repository policy allows it.

## Manual action required

A repository admin must apply these settings in GitHub branch protection for the operational enforcement model to become fully active.
