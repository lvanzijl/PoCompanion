# Running PO Tool from Visual Studio

This guide provides step-by-step instructions for running the PO Tool application from Visual Studio.

## Prerequisites

### Required Software

1. **Visual Studio 2022** (version 17.8 or later)
   - Download from: https://visualstudio.microsoft.com/downloads/
   
2. **.NET 10 SDK**
   - Download from: https://dotnet.microsoft.com/download/dotnet/10.0
   - Verify installation:
     ```bash
     dotnet --version
     ```
     Should output: `10.0.x` or higher

### Required Visual Studio Workloads

Install the following workloads via Visual Studio Installer:

1. **ASP.NET and web development**
   - Required for the Web API backend
   - Includes EF Core tools
   
2. **.NET desktop development** (optional but recommended)
   - Useful for desktop debugging tools

To install/verify workloads:
1. Open **Visual Studio Installer**
2. Click **Modify** on your Visual Studio 2022 installation
3. Select the **Workloads** tab
4. Check the boxes for required workloads
5. Click **Modify** to install

## Opening the Solution

1. **Clone the repository** (if not already done):
   ```bash
   git clone https://github.com/lvanzijl/PoCompanion.git
   cd PoCompanion
   ```

2. **Open the solution in Visual Studio**:
   - Option 1: Double-click `PoTool.sln` in Windows Explorer
   - Option 2: In Visual Studio, go to **File → Open → Project/Solution** and select `PoTool.sln`

3. **Wait for package restore**:
   - Visual Studio will automatically restore NuGet packages
   - Wait for the status bar to show "Ready"
   - Check **Output → Package Manager** for any errors

## Configuring the Startup Project

The application uses a **single executable architecture** where the API project hosts both the backend and the Blazor WebAssembly frontend.

### Set PoTool.Api as Startup Project

1. In **Solution Explorer**, right-click on **PoTool.Api**
2. Select **Set as Startup Project**
3. The project name should now appear in **bold**

> **Note**: This is the only project you need to run. The Client project (Blazor UI) is automatically built and served by the API project.

## Running the Application

### Method 1: Using F5 (Debug Mode)

1. Press **F5** or click the **Start Debugging** button (green play icon)
2. Visual Studio will:
   - Build the solution
   - Build the Blazor Client project
   - Start the API server on `http://localhost:5291`
3. **The application will NOT automatically open a browser** (by design)
4. Manually open your browser and navigate to:
   ```
   http://localhost:5291
   ```

### Method 2: Using Ctrl+F5 (Without Debugging)

1. Press **Ctrl+F5** or select **Debug → Start Without Debugging**
2. The application runs faster without the debugger attached
3. Open your browser and navigate to `http://localhost:5291`

### Method 3: Using dotnet CLI from Visual Studio

1. Open **View → Terminal** (or press **Ctrl+`**)
2. Navigate to the API project:
   ```bash
   cd PoTool.Api
   ```
3. Run the application:
   ```bash
   dotnet run
   ```
4. Open your browser and navigate to `http://localhost:5291`

## What to Expect

When the application starts, you'll have access to:

### Main Application
- **URL**: `http://localhost:5291`
- **Description**: Blazor WebAssembly UI with work item explorer

### API Documentation (Development Only)
- **Swagger UI**: `http://localhost:5291/swagger`
- **Description**: Interactive API documentation for testing endpoints

### API Endpoints
- **Work Items**: `http://localhost:5291/api/workitems`
- **Health Check**: `http://localhost:5291/health`
- **SignalR Hub**: `http://localhost:5291/hubs/workitems`

## Debugging

### Backend (API) Debugging

1. Set breakpoints in API code:
   - Controllers (e.g., `WorkItemsController.cs`)
   - Handlers (e.g., in the `Handlers` folder)
   - Services (e.g., `WorkItemSyncService.cs`)
   - Repositories

2. Press **F5** to start debugging

3. Trigger the breakpoint by:
   - Using the Blazor UI
   - Making requests via Swagger UI
   - Using tools like Postman or curl

4. Use standard debugging features:
   - Step Over (**F10**)
   - Step Into (**F11**)
   - Continue (**F5**)
   - View variables in **Locals** and **Watch** windows

### Frontend (Blazor) Debugging

Blazor WebAssembly debugging is supported in the browser:

1. Start the application with **F5**
2. Open the application in your browser (`http://localhost:5291`)
3. Open **Browser DevTools**:
   - **Chrome/Edge**: Press **F12**
   - Navigate to **Sources** tab
4. Find your C# files under **file://** → **PoTool.Client**
5. Set breakpoints in C# code directly in the browser
6. Interact with the UI to trigger breakpoints

> **Tip**: Blazor debugging requires source maps, which are automatically enabled in Development mode.

### Database Inspection

The application uses SQLite with a local database file:

- **Database file location**: `PoTool.Api/potool.db`
- **Inspect using**:
  - Visual Studio: **View → SQL Server Object Explorer** → **Add Connection** → Select SQLite
  - External tools: DB Browser for SQLite, Azure Data Studio with SQLite extension
  - Command line: `sqlite3 potool.db`

## Building and Cleaning

### Build the Solution
- **Menu**: **Build → Build Solution** or press **Ctrl+Shift+B**
- **Output**: Check **Output → Build** window for errors

### Clean the Solution
If you encounter build issues:

1. **Menu**: **Build → Clean Solution**
2. Delete bin/obj folders manually (optional):
   ```bash
   # From repository root
   Get-ChildItem -Path . -Include bin,obj -Recurse | Remove-Item -Force -Recurse
   ```
3. **Menu**: **Build → Rebuild Solution**

### Restore NuGet Packages
If packages are missing:

1. Right-click on the solution in **Solution Explorer**
2. Select **Restore NuGet Packages**
3. Or use CLI:
   ```bash
   dotnet restore
   ```

## Running Tests

### All Tests
1. Open **Test Explorer**: **View → Test Explorer** or press **Ctrl+E, T**
2. Click **Run All Tests** button (▶▶ icon)

### Specific Test Project
- Right-click on a test project (e.g., **PoTool.Tests.Unit**)
- Select **Run Tests**

### Test Projects in Solution
- **PoTool.Tests.Unit** - MSTest unit tests
- **PoTool.Tests.Integration** - Reqnroll integration tests
- **PoTool.Tests.Blazor** - bUnit Blazor component tests

## Troubleshooting

### Issue: "The target framework 'net10.0' is not supported"

**Solution**: Install .NET 10 SDK
```bash
# Check current version
dotnet --version

# Download .NET 10 SDK from:
# https://dotnet.microsoft.com/download/dotnet/10.0
```

### Issue: Port 5291 Already in Use

**Symptom**: Error message about port already being used

**Solution**:
1. Stop any running instances of the application
2. Check for processes using the port:
   ```bash
   # Windows
   netstat -ano | findstr :5291
   
   # Kill the process (replace PID with actual process ID)
   taskkill /PID <PID> /F
   ```
3. Or change the port in `PoTool.Api/Properties/launchSettings.json`:
   ```json
   "applicationUrl": "http://localhost:5292"
   ```

### Issue: Blazor UI Shows White/Blank Screen

**Possible causes**:
1. Client project didn't build properly
2. Static files not being served

**Solution**:
1. Clean and rebuild the solution
2. Verify `PoTool.Client.csproj` built successfully (check **Output → Build**)
3. Check browser console (F12) for JavaScript errors
4. Verify `UseBlazorFrameworkFiles()` is in `Program.cs`

### Issue: SignalR Connection Fails

**Symptom**: Real-time updates don't work, console shows SignalR errors

**Solution**:
1. Check browser console for exact error
2. Verify hub URL in client code: `/hubs/workitems`
3. Ensure CORS is configured properly in API
4. Check that SignalR middleware is added in `Program.cs`

### Issue: Database Migration Errors

**Symptom**: EF Core errors about missing tables

**Solution**:
1. Delete the database file: `PoTool.Api/potool.db`
2. Restart the application (database is auto-created)
3. Or run migrations manually:
   ```bash
   cd PoTool.Api
   dotnet ef database update
   ```

### Issue: NuGet Package Restore Fails

**Solution**:
1. Clear NuGet cache:
   ```bash
   dotnet nuget locals all --clear
   ```
2. Restore packages:
   ```bash
   dotnet restore
   ```
3. Check internet connection and firewall settings

### Issue: "Cannot find type 'IMediator'"

**Symptom**: Build errors related to Mediator

**Solution**:
This is expected if source generators haven't run yet. Build twice:
1. First build: **Ctrl+Shift+B**
2. Second build: **Ctrl+Shift+B** again
3. Source generators run during the first build

## Development Tips

### Hot Reload

Visual Studio 2022 supports Hot Reload for Blazor:
1. Start with **F5**
2. Make changes to `.razor` files
3. Changes apply automatically without restart
4. Look for "Hot Reload" icon in the toolbar

### Multiple Startup Projects (Advanced)

If you want to run tests and the app simultaneously:
1. Right-click on the **Solution** in Solution Explorer
2. Select **Properties**
3. Select **Multiple startup projects**
4. Set **PoTool.Api** to **Start**
5. Set test projects as needed

### Viewing HTTP Requests

Use the built-in HTTP file for testing:
1. Open `PoTool.Api/PoTool.Api.http`
2. Click **Send Request** links to test endpoints
3. Requires the application to be running

## Configuration Files

Key configuration files you might need to modify:

### Application Settings
- **PoTool.Api/appsettings.json** - API configuration
- **PoTool.Api/appsettings.Development.json** - Development overrides

### Launch Settings
- **PoTool.Api/Properties/launchSettings.json** - Port, environment variables

### Project Files
- **PoTool.Api/PoTool.Api.csproj** - API project configuration
- **PoTool.Client/PoTool.Client.csproj** - Blazor client configuration

## Next Steps

After successfully running the application:

1. **Explore the UI** at `http://localhost:5291`
2. **Try the API** via Swagger at `http://localhost:5291/swagger`
3. **Set up TFS/Azure DevOps** connection (see user guide)
4. **Read the architecture docs**:
   - `docs/ARCHITECTURE_RULES.md`
   - `docs/SINGLE_EXECUTABLE_ARCHITECTURE.md`
   - `docs/GEBRUIKERSHANDLEIDING.md` (User Guide in Dutch)

## Additional Resources

- **Main README**: `README.md` - Project overview
- **Architecture Rules**: `docs/ARCHITECTURE_RULES.md`
- **Single Executable Architecture**: `docs/SINGLE_EXECUTABLE_ARCHITECTURE.md`
- **.NET Documentation**: https://docs.microsoft.com/dotnet/
- **Blazor Documentation**: https://docs.microsoft.com/aspnet/core/blazor/
- **Entity Framework Core**: https://docs.microsoft.com/ef/core/

## Support

If you encounter issues not covered in this guide:

1. Check the **Output** window in Visual Studio for detailed error messages
2. Check the **Error List** window for build errors
3. Review the `docs/` folder for additional documentation
4. Check the GitHub repository issues page
