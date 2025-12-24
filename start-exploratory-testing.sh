#!/usr/bin/env bash

# PoCompanion Exploratory Testing Starter (Bash)
# This script starts the PoCompanion application for exploratory testing with mock data.

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

echo -e "${CYAN}========================================"
echo -e "PoCompanion Exploratory Testing Starter"
echo -e "========================================${NC}"
echo ""

# Check .NET installation
echo -e "${YELLOW}[1/6] Checking .NET installation...${NC}"
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    echo -e "${GREEN}✓ .NET version: ${DOTNET_VERSION}${NC}"
else
    echo -e "${RED}✗ .NET is not installed or not in PATH${NC}"
    echo -e "${RED}Please install .NET 10.0 SDK from https://dot.net${NC}"
    exit 1
fi

# Check appsettings.Development.json for mock mode
echo ""
echo -e "${YELLOW}[2/6] Verifying mock data configuration...${NC}"
APPSETTINGS_PATH="PoTool.Api/appsettings.Development.json"
if [ -f "$APPSETTINGS_PATH" ]; then
    USE_MOCK=$(grep -A 2 "TfsIntegration" "$APPSETTINGS_PATH" | grep "UseMockClient" | grep -o "true\|false")
    if [ "$USE_MOCK" == "true" ]; then
        echo -e "${GREEN}✓ Mock client is enabled in appsettings.Development.json${NC}"
    else
        echo -e "${RED}✗ Mock client is not enabled!${NC}"
        echo -e "${RED}Please set TfsIntegration.UseMockClient to true in $APPSETTINGS_PATH${NC}"
        exit 1
    fi
else
    echo -e "${RED}✗ appsettings.Development.json not found at $APPSETTINGS_PATH${NC}"
    exit 1
fi

# Build the solution
echo ""
echo -e "${YELLOW}[3/6] Building solution...${NC}"
if dotnet build PoTool.sln --configuration Release --no-restore --verbosity quiet; then
    echo -e "${GREEN}✓ Build successful${NC}"
else
    echo -e "${RED}✗ Build failed${NC}"
    exit 1
fi

# Start API in background
echo ""
echo -e "${YELLOW}[4/6] Starting API server...${NC}"
API_URL="http://localhost:5000"

# Kill any existing process on port 5000
if lsof -Pi :5000 -sTCP:LISTEN -t &>/dev/null; then
    echo -e "${YELLOW}⚠ Port 5000 is already in use. Attempting to stop existing process...${NC}"
    lsof -ti:5000 | xargs kill -9 2>/dev/null || true
    sleep 2
fi

# Start the API
export ASPNETCORE_ENVIRONMENT=Development
cd PoTool.Api
dotnet run --no-build --configuration Release --urls "$API_URL" > ../api.log 2>&1 &
API_PID=$!
cd ..

echo -e "${GREEN}✓ API started (PID: $API_PID)${NC}"
echo -e "${GRAY}  API will be available at: $API_URL${NC}"
echo -e "${GRAY}  Logs: api.log${NC}"

# Wait for API health check
echo ""
echo -e "${YELLOW}[5/6] Waiting for API health check...${NC}"
MAX_ATTEMPTS=30
ATTEMPT=0
API_HEALTHY=false

while [ $ATTEMPT -lt $MAX_ATTEMPTS ]; do
    ATTEMPT=$((ATTEMPT + 1))
    
    if curl -s -o /dev/null -w "%{http_code}" "$API_URL/health" 2>/dev/null | grep -q "200"; then
        API_HEALTHY=true
        echo -e "${GREEN}✓ API is healthy and responding${NC}"
        break
    fi
    
    echo -e "${GRAY}  Attempt $ATTEMPT/$MAX_ATTEMPTS...${NC}"
    sleep 2
done

if [ "$API_HEALTHY" != "true" ]; then
    echo -e "${RED}✗ API health check failed after $MAX_ATTEMPTS attempts${NC}"
    echo -e "${YELLOW}Checking API logs:${NC}"
    tail -n 20 api.log
    kill $API_PID 2>/dev/null || true
    exit 1
fi

# Instructions for testing
echo ""
echo -e "${YELLOW}[6/6] Ready for exploratory testing!${NC}"
echo ""
echo -e "${GREEN}========================================"
echo -e "EXPLORATORY TESTING INSTRUCTIONS"
echo -e "========================================${NC}"
echo ""
echo -e "${CYAN}API Server:${NC}"
echo -e "${WHITE}  URL: $API_URL${NC}"
echo -e "${WHITE}  Status: Running (PID: $API_PID)${NC}"
echo -e "${WHITE}  Logs: tail -f api.log${NC}"
echo ""
echo -e "${CYAN}Client Application:${NC}"
echo -e "${WHITE}  To start the client, open a new terminal and run:${NC}"
echo -e "${YELLOW}  cd PoTool.Client${NC}"
echo -e "${YELLOW}  dotnet run --no-build --configuration Release${NC}"
echo ""
echo -e "${WHITE}  The client will be available at: http://localhost:5001${NC}"
echo ""
echo -e "${CYAN}Testing Guide:${NC}"
echo -e "${WHITE}  1. Navigate to http://localhost:5001 in your browser${NC}"
echo -e "${WHITE}  2. Follow the test plan in docs/EXPLORATORY_TEST_PLAN.md${NC}"
echo -e "${WHITE}  3. Capture screenshots as you test each feature${NC}"
echo -e "${WHITE}  4. Document results in docs/TEST_RESULTS.md${NC}"
echo ""
echo -e "${CYAN}Features to Test:${NC}"
echo -e "${WHITE}  • Home Page (landing)${NC}"
echo -e "${WHITE}  • TFS Configuration (/tfsconfig)${NC}"
echo -e "${WHITE}  • Work Items (not yet implemented - skip)${NC}"
echo -e "${WHITE}  • Backlog Health (/backlog-health)${NC}"
echo -e "${WHITE}  • Effort Distribution (/effort-distribution)${NC}"
echo -e "${WHITE}  • PR Insights (/pr-insights)${NC}"
echo -e "${WHITE}  • State Timeline (/state-timeline)${NC}"
echo -e "${WHITE}  • Epic Forecast (/epic-forecast)${NC}"
echo -e "${WHITE}  • Dependency Graph (/dependency-graph)${NC}"
echo -e "${WHITE}  • Velocity Dashboard (/velocity-dashboard)${NC}"
echo ""
echo -e "${CYAN}To Stop Testing:${NC}"
echo -e "${WHITE}  Press Ctrl+C to stop this script${NC}"
echo ""
echo -e "${GREEN}========================================${NC}"

# Trap to cleanup on exit
cleanup() {
    echo ""
    echo -e "${YELLOW}Cleaning up...${NC}"
    kill $API_PID 2>/dev/null || true
    echo -e "${GREEN}✓ API server stopped${NC}"
}

trap cleanup EXIT

# Keep script running and optionally tail logs
echo ""
echo -e "${CYAN}Press Ctrl+C to stop the API server and exit${NC}"
echo -e "${GRAY}API logs:${NC}"
echo ""

# Tail the API logs
tail -f api.log
