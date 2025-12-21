# PAT Storage Best Practices

## Overview

This document defines the authoritative rules for storing Personal Access Tokens (PAT) and other sensitive credentials in PoCompanion. These rules prioritize security and follow industry best practices for desktop application credential management.

## Core Principles

### 1. Client-Side Storage Only

**Rule**: PAT and other user credentials MUST be stored on the client device, NEVER on the server.

**Rationale**:
- Credentials are user-specific and tied to the user's workstation
- Centralized credential storage creates a single point of failure
- Server breaches would expose all user credentials
- Users may have different credentials on different workstations
- Credentials should not roam between workstations for security reasons

### 2. Platform-Specific Secure Storage

**Rule**: PAT MUST be stored using platform-native secure storage mechanisms via MAUI SecureStorage API.

**Implementation**:
- **Windows**: Windows Credential Manager (DPAPI encryption)
- **macOS**: Keychain Services
- **Linux**: Secret Service API (libsecret/GNOME Keyring)

**MAUI SecureStorage** automatically selects the appropriate platform mechanism.

### 3. No Server Persistence

**Rule**: The server/API MUST NEVER persist PAT or other credentials to any storage (database, files, cache).

**Allowed Server Behavior**:
- Receive PAT via API call for immediate validation
- Hold PAT in memory for the duration of a request
- Use PAT to authenticate with TFS/Azure DevOps
- Return validation results

**Prohibited Server Behavior**:
- Storing PAT in database (even encrypted)
- Caching PAT in memory beyond request lifetime
- Logging PAT in any form
- Transmitting PAT to other services

### 4. Two-Tier Storage Architecture

**Rule**: Distinguish between roaming settings (server-side) and local settings (client-side).

#### Server-Side Storage (Database)
Settings that should persist across workstations:
- TFS/Azure DevOps URL
- Project name
- User preferences (UI settings, filters, etc.)
- Application state (work item caches, metadata)

#### Client-Side Storage (Secure Local)
Settings that are workstation-specific and security-sensitive:
- Personal Access Token (PAT)
- Session tokens
- Temporary authentication state
- User-specific secrets

## Implementation Requirements

### Client Implementation

1. **Use MAUI SecureStorage for PAT**:
```csharp
// Store PAT
await SecureStorage.SetAsync("tfs_pat", patValue);

// Retrieve PAT
string pat = await SecureStorage.GetAsync("tfs_pat");

// Remove PAT
SecureStorage.Remove("tfs_pat");
```

2. **PAT Lifecycle**:
   - User enters PAT in UI
   - Client stores PAT in SecureStorage
   - Client sends PAT to API for each TFS operation that requires it
   - PAT never leaves client storage permanently

3. **Memory Protection**:
   - Use `SecureString` or clear sensitive strings after use where possible
   - Avoid string concatenation or logging that could leave PAT in memory

### API Implementation

1. **Accept PAT per Request**:
   - API endpoints that interact with TFS should accept PAT as a parameter
   - Alternative: Accept PAT once per session and store in memory-only session state
   - PAT must be cleared from memory after use

2. **No Database Storage**:
   - Remove `ProtectedPat` field from `TfsConfigEntity`
   - Update database migrations to drop PAT storage
   - TfsConfigurationService should only handle non-sensitive configuration

3. **Request Validation**:
   - Validate PAT immediately upon receipt
   - Return validation results to client
   - Do not persist validation state with PAT

### API Endpoints

**Configuration Endpoints**:
```csharp
// Save non-sensitive config (no PAT)
POST /api/settings/tfs-config
{
  "url": "https://dev.azure.com/org",
  "project": "MyProject"
}

// Validate PAT (receives PAT, doesn't store it)
POST /api/settings/validate-tfs
{
  "url": "https://dev.azure.com/org",
  "project": "MyProject",
  "pat": "xxxxx"  // Used for validation only
}
```

**TFS Operation Endpoints**:
```csharp
// Option A: Include PAT in each request
POST /api/workitems/query
{
  "pat": "xxxxx",
  "query": "..."
}

// Option B: Establish session with PAT
POST /api/sessions/tfs
{
  "pat": "xxxxx"
}
// Returns session token, subsequent requests use session token
```

## Security Considerations

### What This Protects Against

✅ **Server Breach**: Even if server is compromised, no PATs are exposed
✅ **Database Theft**: Database contains no credentials
✅ **Multi-User Security**: Each user's credentials stay on their workstation
✅ **Credential Theft**: Platform secure storage provides encryption at rest

### What This Doesn't Protect Against

❌ **Compromised Workstation**: If user's machine is compromised, PAT can be extracted
❌ **Man-in-the-Middle**: PAT transmitted over network (mitigated by HTTPS)
❌ **Malicious Client**: Client application could extract PAT from secure storage

### Additional Security Measures

1. **HTTPS Only**: All API communication MUST use HTTPS in production
2. **PAT Expiration**: Encourage users to set PAT expiration in Azure DevOps
3. **Minimal Permissions**: PAT should have minimal required scopes
4. **Audit Logging**: Log TFS operations (not PAT) for audit trail
5. **Session Timeout**: Clear in-memory PAT after inactivity period

## Migration from Current Implementation

### Current State (As-Is)
- PAT stored in SQLite database in `TfsConfigEntity.ProtectedPat`
- PAT encrypted using ASP.NET Data Protection
- PAT persists on server/API side
- Client retrieves config without PAT

### Target State (To-Be)
- PAT stored in MAUI SecureStorage on client
- API receives PAT per request or per session
- Database contains only non-sensitive config (URL, Project)
- Client manages PAT lifecycle

### Migration Steps

1. **Add SecureStorage to Client**
   - Create secure storage service abstraction
   - Implement MAUI SecureStorage wrapper
   - Add PAT storage/retrieval methods

2. **Update Client UI**
   - Modify TFS config form to store PAT locally
   - Update TFS operations to include PAT from secure storage
   - Add PAT re-entry flow if missing or invalid

3. **Update API**
   - Add PAT parameter to TFS validation endpoint
   - Update TFS client to accept PAT per operation
   - Remove PAT storage from TfsConfigurationService

4. **Update Database**
   - Create migration to remove ProtectedPat column
   - Preserve existing URL and Project data
   - No data migration needed for PAT (users will re-enter)

5. **Update Tests**
   - Update tests to provide PAT via request
   - Mock secure storage in client tests
   - Update integration tests

## Validation

Before considering this change complete, verify:

- [ ] PAT stored using MAUI SecureStorage
- [ ] PAT never written to database
- [ ] PAT cleared from server memory after use
- [ ] Database migration removes ProtectedPat column
- [ ] TFS operations work with client-provided PAT
- [ ] PAT validation endpoint works without storing PAT
- [ ] All tests pass
- [ ] Security scan (CodeQL) passes
- [ ] Documentation updated

## References

- [MAUI SecureStorage Documentation](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage)
- [Azure DevOps PAT Best Practices](https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate)
- [OWASP Credential Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Credential_Storage_Cheat_Sheet.html)

## Approval

This document supersedes any previous rules regarding PAT storage in:
- `docs/ARCHITECTURE_RULES.md` (Section 7)
- `docs/TFS_ONPREM_INTEGRATION_PLAN.md` (Authentication section)

All code MUST comply with the rules defined in this document.
