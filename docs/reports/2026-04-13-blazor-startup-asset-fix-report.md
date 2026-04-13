# Blazor WebAssembly startup asset fix validation report

## Root cause
The hosted server project was missing a project reference to `PoTool.Client/PoTool.Client.csproj`. Because of that, the API host did not load the client static web assets manifest, had no effective web root for the hosted client, and could not serve the Blazor WebAssembly startup files from `/_framework` or the client `index.html` fallback.

## Exact failing asset/request before fix
- `GET http://localhost:5291/` returned `404` in local reproduction before the fix.
- `GET http://localhost:5291/_framework/blazor.webassembly.js` returned `404` before the fix.
- Startup logs also showed: `The WebRootPath was not found: /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/wwwroot. Static files may be unavailable.`
- The client `index.html` also contained an in-repo invalid preload element: `<link rel="preload" id="webassembly" />`, which produced the preload warning but was not the fatal blocker.

## Files changed
- `PoTool.Api/PoTool.Api.csproj`
- `PoTool.Api/packages.lock.json`
- `PoTool.Client/wwwroot/index.html`
- `docs/release-notes.json`
- `docs/reports/2026-04-13-blazor-startup-asset-fix-report.md`

## Why the chosen fix is correct
Adding the hosted API → client project reference restores the standard hosted Blazor WebAssembly static-web-assets flow. After that change, the API host serves the client `index.html`, `PoTool.Client.styles.css`, and the required `/_framework/*` startup files from the hosted client output as intended. Removing the empty preload tag eliminates a real in-repo warning without changing the hosted startup model or disabling diagnostics.

## Build/output inspection
- Pre-fix `dotnet run` on the API logged missing web-root warnings and served neither `/` nor `/_framework/blazor.webassembly.js` successfully.
- Post-fix `dotnet publish PoTool.Api/PoTool.Api.csproj -c Release -o /tmp/potool-api-publish --nologo` produced:
  - `/tmp/potool-api-publish/wwwroot/index.html`
  - `/tmp/potool-api-publish/wwwroot/_framework/blazor.webassembly.js`
  - `/tmp/potool-api-publish/wwwroot/_framework/dotnet.js`
  - the expected hashed WebAssembly and PDB startup assets under `/tmp/potool-api-publish/wwwroot/_framework`
- This .NET 10 output did not emit a `blazor.boot.json` file, so consistency was verified against the actual emitted `_framework` asset set and live startup requests instead.

## Validation steps performed
1. Built the solution before changes with `dotnet build PoTool.sln -c Release --nologo`.
2. Ran unit tests before changes with `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --nologo` and confirmed there were pre-existing unrelated failures in governance/documentation audit tests.
3. Reproduced the startup failure locally by running the API host on `http://localhost:5291/` and confirming `404` responses for `/` and `/_framework/blazor.webassembly.js` before the fix.
4. Applied the hosted client project reference fix and removed the invalid preload tag.
5. Rebuilt successfully with `dotnet build PoTool.sln -c Release --nologo`.
6. Ran the fixed host on `http://localhost:5291/` and verified:
   - `GET /` => `200`
   - `GET /_framework/blazor.webassembly.js` => `200`
   - `GET /_framework/dotnet.js` => `200`
   - hashed `/_framework/*.wasm` and `/_framework/*.pdb` requests => `200`
7. Opened the app in Playwright and confirmed the hosted client advanced past the loading shell into the running application (`/sync-gate?returnUrl=%2Fhome`).
8. Published the API host and verified the hosted client assets were present in publish output.

## Remaining warnings/noise
- A blocked request to `https://fonts.googleapis.com/...` was observed in this sandboxed browser session as `ERR_BLOCKED_BY_CLIENT.Inspector`; this is external/environmental and not the startup blocker.
- Browser extension or BrowserLink-style noise was not present in repository code and was not treated as causal.
- The preload warning is resolved by removing the invalid empty preload element from the client `index.html`.

## Follow-up recommendations
No immediate follow-up is required beyond keeping the hosted API → client project reference intact when editing solution/project structure.
