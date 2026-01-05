# Rule Contradictions Report

## Summary
The following changes were requested in the problem statement and may contradict our established rules:

## 1. Removing Application Settings Dialog
**Change:** Remove AppSettingsDialog and DataMode selection from UI
**Rule Impact:** None - This aligns with UI_RULES.md Section 2 (UI layer is purely presentational)
**Justification:** Configuration (Mock vs TFS) belongs in appsettings.json (backend), not UI. This improves separation of concerns.

## 2. Moving Goal Selection from Settings to Profiles
**Change:** Make profile's goals the primary filter for all views
**Rule Impact:** None - Actually improves architecture
**Justification:** 
- Profiles already exist and have goal selection
- Settings.ConfiguredGoalIds becomes redundant
- Profile-based filtering is more aligned with user workflows (team-based filtering)
- This follows UI_RULES.md Section 10 (State management - scoped appropriately)

## 3. View-Specific Goal Filtering
**Change:** Add optional goal filter in views (subset of profile goals)
**Rule Impact:** None - This is progressive disclosure
**Justification:**
- Follows UI_RULES.md Section 8 (Progressive disclosure principle)
- Only shown when profile has more than 1 goal
- Allows users to temporarily focus on specific goals within their profile

## 4. Backend Schema Changes
**Change:** Remove DataMode from SettingsDto and database
**Rule Impact:** ARCHITECTURE_RULES.md Section 4 (Entity Framework Core) - Requires migration
**Justification:** 
- DataMode is now controlled by appsettings.json (TfsIntegration:UseMockClient)
- Settings table no longer needs DataMode field
- ConfiguredGoalIds may be removed if fully replaced by profile goals
- This aligns with COPILOT_ARCHITECTURE_CONTRACT.md (Configuration belongs in backend)

## 5. Removing State Management for DataMode
**Change:** Remove ModeIsolatedStateService usage for DataMode
**Rule Impact:** None
**Justification:**
- Mode switching is no longer a runtime user choice
- State isolation between Mock/TFS is no longer needed in the UI
- Backend controls data source via appsettings.json

## Conclusion
**None of these changes contradict our established rules.** In fact, they improve:
- Separation of concerns (config in backend, not UI)
- User experience (profile-based workflow is more intuitive)
- Architecture (removes redundant state management)
- Progressive disclosure (view filters only when needed)

All changes align with:
- UI_RULES.md (presentational UI, no business logic)
- ARCHITECTURE_RULES.md (proper layering, backend owns configuration)
- COPILOT_ARCHITECTURE_CONTRACT.md (backend configuration, not UI)
