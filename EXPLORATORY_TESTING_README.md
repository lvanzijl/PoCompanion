# Exploratory Testing Quick Start

This repository now includes complete infrastructure for exploratory testing with mock data.

## Quick Start (5 minutes)

### 1. Start the Application

**Windows:**
```powershell
.\start-exploratory-testing.ps1
```

**Linux/macOS:**
```bash
./start-exploratory-testing.sh
```

### 2. Start the Client (New Terminal)

```bash
cd PoTool.Client
dotnet run --no-build --configuration Release
```

### 3. Open Browser

Navigate to: `http://localhost:5001`

### 4. Follow Test Plan

Open `docs/EXPLORATORY_TEST_PLAN.md` and test each feature systematically.

## What's Included

📝 **Complete Test Plan** - `docs/EXPLORATORY_TEST_PLAN.md`  
📋 **Test Results Template** - `docs/TEST_RESULTS.md`  
📸 **Screenshot Index** - `docs/screenshots/README.md`  
🚀 **Startup Scripts** - `start-exploratory-testing.ps1` and `.sh`  
✅ **Setup Verification** - `docs/EXPLORATORY_TEST_INFRASTRUCTURE_COMPLETE.md`

## Features to Test

- ✅ Home Page
- ✅ TFS Configuration
- ✅ Backlog Health
- ✅ Effort Distribution
- ✅ PR Insights
- ✅ State Timeline
- ✅ Epic Forecast
- ✅ Dependency Graph
- ✅ Velocity Dashboard
- ✅ Settings Modal

## Mock Data Configured

All testing uses **mock data** (no real TFS connection required):
- Complete work item hierarchy (Goal → Task)
- 12 sprints with varying velocity
- Pull request data across all sprints
- Revision history for work items

## Need Help?

- **Full Test Plan:** `docs/EXPLORATORY_TEST_PLAN.md`
- **Infrastructure Status:** `docs/EXPLORATORY_TEST_INFRASTRUCTURE_COMPLETE.md`
- **Troubleshooting:** See test plan section "Troubleshooting"

## Expected Duration

**Complete exploratory testing:** 4-6 hours
- Setup: 10 minutes
- Feature testing: 3-4 hours
- Screenshot capture: 1 hour
- Documentation: 1 hour

## Success Criteria

✅ All features load and render  
✅ Mock data displays correctly  
✅ No critical errors  
✅ Screenshots captured  
✅ Test results documented

---

**Ready to start?** Run the startup script and follow the test plan! 🚀
