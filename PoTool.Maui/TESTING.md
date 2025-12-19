# PoTool.Maui Testing Guide

This document outlines the testing strategy and checklist for the MAUI Hybrid application.

## Smoke Test Checklist

### Application Startup
- [ ] App starts successfully on Windows
- [ ] API is reachable at `http://localhost:5291/health`
- [ ] API logs show successful startup
- [ ] No errors in application logs during startup
- [ ] Blazor UI loads in MAUI WebView
- [ ] Loading indicator displays during initialization

### Navigation
- [ ] Home page loads correctly
- [ ] Navigation to WorkItems page works
- [ ] Navigation to TfsConfig page works
- [ ] Back navigation works correctly
- [ ] Deep linking to specific pages works

### UI Rendering
- [ ] MudBlazor components render correctly
- [ ] Dark theme is applied correctly
- [ ] Responsive layout works properly
- [ ] Icons and images display correctly
- [ ] Custom fonts load properly

### API Communication
- [ ] GET `/api/workitems` succeeds
- [ ] GET `/api/tfsconfig` succeeds
- [ ] POST `/api/tfsconfig` succeeds
- [ ] GET `/api/tfsvalidate` responds correctly
- [ ] POST `/api/workitems/sync` triggers sync

### SignalR Communication
- [ ] SignalR connection established successfully
- [ ] SignalR hub URL is correct (`http://localhost:5291/hubs/workitems`)
- [ ] Real-time updates via SignalR work
- [ ] Sync status notifications appear in UI
- [ ] SignalR reconnection works after disconnect

### Data Persistence
- [ ] Database file created in correct location
- [ ] TFS configuration can be saved
- [ ] TFS configuration can be retrieved
- [ ] Work items are cached locally
- [ ] Settings persist across app restarts

### Application Shutdown
- [ ] App shuts down cleanly without errors
- [ ] API stops gracefully on app exit
- [ ] No orphaned processes remain after exit
- [ ] Database connections are closed properly

## Integration Test Verification

Run the integration tests to verify API functionality:

```bash
cd PoTool.Tests.Integration
dotnet test
```

All Reqnroll scenarios should pass:
- [ ] Health endpoint tests pass
- [ ] TFS configuration tests pass
- [ ] Work items API tests pass
- [ ] SignalR hub tests pass

## Platform-Specific Testing

### Windows
- [ ] App runs on Windows 10 (19041 or later)
- [ ] App runs on Windows 11
- [ ] Native window controls work
- [ ] File system access works
- [ ] Database path is correct (AppData)

### macOS (Mac Catalyst)
- [ ] App runs on macOS 11+ (Big Sur or later)
- [ ] Native menu bar integration works
- [ ] File system access works
- [ ] Database path is correct (Application Support)

### Android (Optional)
- [ ] App runs on Android 5.0+ (API 21+)
- [ ] Network permissions granted
- [ ] Database path is correct (internal storage)

### iOS (Optional)
- [ ] App runs on iOS 14.2+
- [ ] Network permissions granted
- [ ] Database path is correct (Documents)

## Performance Testing

- [ ] App startup time < 5 seconds
- [ ] API startup time < 2 seconds
- [ ] UI rendering is smooth (60 FPS)
- [ ] Memory usage is reasonable (< 200 MB)
- [ ] No memory leaks during extended use

## Error Scenarios

### API Failures
- [ ] App shows error if API fails to start
- [ ] App recovers if API crashes
- [ ] Health check timeout is handled gracefully
- [ ] Network errors show user-friendly messages

### Database Errors
- [ ] Legacy database detection works
- [ ] Database migration errors are logged
- [ ] Fallback to EnsureCreated works

### SignalR Errors
- [ ] SignalR connection failure is handled
- [ ] Automatic reconnection works
- [ ] UI updates continue even if SignalR fails

## Development Mode Testing

- [ ] Blazor developer tools are available
- [ ] Hot reload works for Blazor components
- [ ] Debug logging is enabled
- [ ] Swagger UI is accessible at `/swagger`
- [ ] Error details are displayed in UI

## Production Mode Testing

- [ ] Exception handler is active
- [ ] Detailed error information is hidden from users
- [ ] Swagger UI is not accessible
- [ ] Debug logging is disabled
- [ ] HTTPS redirection works (if configured)

## Known Limitations

- MAUI workload is not available on Linux CI/CD environments
- The app requires .NET 10 SDK and MAUI workload to build
- Cross-platform testing requires access to each target platform
- Some features may vary by platform (e.g., file paths, native controls)

## Reporting Issues

When reporting issues, please include:
- Platform and OS version
- Steps to reproduce
- Expected behavior
- Actual behavior
- Logs from application output
- Screenshot or video (if UI issue)
