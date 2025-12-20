# PoTool.Web

Standalone Blazor Server host for testing and documentation screenshots.

## Purpose

This project provides a web-based version of the PO Companion application that:
- Runs the full application stack (API + Client) in a single web server
- Properly serves all CSS and static assets
- Can be used for testing when MAUI is not available (e.g., CI/CD environments)
- Enables screenshot generation for documentation

## Running

```bash
cd PoTool.Web
dotnet run
```

The application will be available at `http://localhost:5291`

## Features

- Full API functionality (WorkItems, TFS Config, SignalR)
- Complete Blazor UI with proper styling (MudBlazor dark theme)
- All client-side features including:
  - Work Item Explorer with hierarchical tree view
  - Search and filtering
  - Validation rules
  - Settings management

## Use Cases

1. **Testing**: Run the application on Linux/CI environments where MAUI is not available
2. **Screenshots**: Generate documentation screenshots with proper styling
3. **Development**: Quick testing without launching MAUI
4. **Demos**: Web-based demonstrations of the application

## Differences from MAUI

- Uses Blazor Server instead of Blazor Hybrid
- No native window chrome (browser-based)
- Same UI and functionality as the MAUI application
