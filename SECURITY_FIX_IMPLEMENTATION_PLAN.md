# Security Fix Implementation Plan — PO Companion

**Date**: 2026-01-13  
**Related**: SECURITY_AUDIT_REPORT.md

---

## Overview

This document outlines the phased approach to addressing security vulnerabilities identified in the security audit, grouped into small, reviewable, and testable steps.

---

## Phase 1: XSS Remediation (CRITICAL - Priority 1)

### Goal
Fix the XSS vulnerability in work item description rendering by implementing proper HTML sanitization.

### Steps

#### Step 1.1: Add HTML Sanitization Library
**Task**: Add `HtmlSanitizer` package to the solution  
**Projects**: `PoTool.Shared`  
**Changes**:
- Add `Ganss.Xss` (HtmlSanitizer) NuGet package to `PoTool.Shared.csproj`
- Version: Latest stable (currently 5.x)
- Check for security advisories before adding

**Rationale**: `Ganss.Xss` is a well-maintained, widely-used HTML sanitizer for .NET with:
- Active development and security updates
- Configurable whitelist-based approach
- Good performance
- .NET Standard 2.0 support

**Testing**: Verify package installation and build succeeds

#### Step 1.2: Create HTML Sanitization Helper
**Task**: Implement centralized HTML sanitization service  
**Location**: `PoTool.Shared/Helpers/HtmlSanitizer.cs`  
**Changes**:
```csharp
namespace PoTool.Shared.Helpers;

/// <summary>
/// Provides HTML sanitization for user-generated content to prevent XSS attacks.
/// Uses allowlist-based approach to permit only safe HTML elements and attributes.
/// </summary>
public static class HtmlSanitizationHelper
{
    private static readonly Lazy<Ganss.Xss.HtmlSanitizer> _sanitizer = new(() =>
    {
        var sanitizer = new Ganss.Xss.HtmlSanitizer();
        
        // Configure allowed tags (basic formatting only)
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.Add("p");
        sanitizer.AllowedTags.Add("br");
        sanitizer.AllowedTags.Add("strong");
        sanitizer.AllowedTags.Add("em");
        sanitizer.AllowedTags.Add("u");
        sanitizer.AllowedTags.Add("ul");
        sanitizer.AllowedTags.Add("ol");
        sanitizer.AllowedTags.Add("li");
        sanitizer.AllowedTags.Add("a");
        
        // Configure allowed attributes
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href"); // For <a> tags
        
        // Configure allowed schemes for URLs
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        
        // Remove any javascript: or data: URLs
        sanitizer.AllowedSchemes.Remove("javascript");
        sanitizer.AllowedSchemes.Remove("data");
        
        return sanitizer;
    });
    
    /// <summary>
    /// Sanitizes HTML content by removing potentially dangerous elements and attributes.
    /// Only allows safe formatting tags and attributes.
    /// </summary>
    /// <param name="html">Raw HTML content to sanitize.</param>
    /// <returns>Sanitized HTML safe for rendering.</returns>
    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;
            
        return _sanitizer.Value.Sanitize(html);
    }
}
```

**Rationale**:
- Centralized implementation ensures consistent sanitization
- Whitelist approach (only allow known-safe elements)
- Lazy initialization for performance
- Static class for easy consumption
- Thread-safe singleton pattern

**Testing**: Unit tests (see Step 1.4)

#### Step 1.3: Update WorkItemDetailPanel to Use Sanitization
**Task**: Apply sanitization before MarkupString conversion  
**Location**: `PoTool.Client/Components/WorkItems/SubComponents/WorkItemDetailPanel.razor`  
**Changes**:

1. Add using directive at top:
```razor
@using PoTool.Shared.Helpers
```

2. Update line 74 (multi-select):
```razor
<!-- Before -->
<div class="description-content">@((MarkupString)(GetCommonDescription() ?? string.Empty))</div>

<!-- After -->
<div class="description-content">@((MarkupString)HtmlSanitizationHelper.Sanitize(GetCommonDescription()))</div>
```

3. Update line 185 (single select):
```razor
<!-- Before -->
<div class="description-content">@((MarkupString)SelectedWorkItem.Description)</div>

<!-- After -->
<div class="description-content">@((MarkupString)HtmlSanitizationHelper.Sanitize(SelectedWorkItem.Description))</div>
```

**Rationale**:
- Minimal code changes
- Sanitization happens at rendering (last line of defense)
- No changes to data model or API
- Backward compatible

**Testing**: Manual testing + Blazor tests (see Step 1.5)

#### Step 1.4: Add Unit Tests for Sanitization
**Task**: Create comprehensive unit tests for HTML sanitization  
**Location**: `PoTool.Tests.Unit/Helpers/HtmlSanitizationHelperTests.cs`  
**Test Cases**:

```csharp
[TestClass]
public class HtmlSanitizationHelperTests
{
    [TestMethod]
    public void Sanitize_NullInput_ReturnsEmpty()
    
    [TestMethod]
    public void Sanitize_EmptyInput_ReturnsEmpty()
    
    [TestMethod]
    public void Sanitize_PlainText_ReturnsUnchanged()
    
    [TestMethod]
    public void Sanitize_SafeHtml_PreservesFormatting()
    
    [TestMethod]
    public void Sanitize_ScriptTag_RemovesScript()
    
    [TestMethod]
    public void Sanitize_OnEventAttribute_RemovesAttribute()
    
    [TestMethod]
    public void Sanitize_JavascriptUrl_RemovesUrl()
    
    [TestMethod]
    public void Sanitize_DataUrl_RemovesUrl()
    
    [TestMethod]
    public void Sanitize_IframeTag_RemovesIframe()
    
    [TestMethod]
    public void Sanitize_EncodedScript_DecodesAndRemoves()
    
    [TestMethod]
    public void Sanitize_MixedContent_RemovesOnlyDangerous()
    
    [TestMethod]
    public void Sanitize_RealWorldTfsHtml_PreservesSafe()
}
```

**Testing Approach**:
- Test with common XSS payloads
- Test with TFS-typical HTML (lists, formatting)
- Test edge cases (null, empty, very large)
- Test encoded attacks

#### Step 1.5: Add Integration Tests
**Task**: Test sanitization with full component rendering  
**Location**: `PoTool.Tests.Blazor/WorkItemDetailPanelSecurityTests.cs`  
**Test Cases**:

```csharp
[TestClass]
public class WorkItemDetailPanelSecurityTests
{
    [TestMethod]
    public void WorkItemWithScriptInDescription_DoesNotExecuteScript()
    
    [TestMethod]
    public void WorkItemWithEventHandler_DoesNotExecuteHandler()
    
    [TestMethod]
    public void WorkItemWithSafeHtml_DisplaysFormatted()
    
    [TestMethod]
    public void MultiSelectWithMaliciousDescription_DoesNotExecuteScript()
}
```

**Testing Approach**:
- Use bUnit for Blazor component testing
- Verify HTML output doesn't contain dangerous elements
- Verify safe formatting is preserved

#### Step 1.6: Document Sanitization Pattern
**Task**: Create developer documentation for HTML sanitization  
**Location**: `docs/SECURITY_HTML_SANITIZATION.md`  
**Content**:
- When to use HTML sanitization
- How to use `HtmlSanitizationHelper`
- What's allowed vs. blocked
- Testing guidelines
- Examples of safe vs. unsafe HTML

---

## Phase 2: Input Validation Enhancement (HIGH - Priority 2)

### Goal
Strengthen WIQL injection protection and input validation across the application.

### Steps

#### Step 2.1: Enhance WIQL Escaping
**Task**: Improve WIQL query parameterization  
**Location**: `PoTool.Api/Services/RealTfsClient.cs`  
**Changes**:
- Enhance `EscapeWiql` method to handle brackets, keywords
- Add validation against TFS area path whitelist
- Document WIQL injection risks and mitigations

#### Step 2.2: Add Input Validation to API Boundaries
**Task**: Apply validation at API entry points  
**Location**: Controllers and handlers  
**Changes**:
- Use `InputValidator.IsValidAreaPath` before processing
- Add validation attributes to DTOs
- Return 400 Bad Request for invalid inputs

#### Step 2.3: Add Tests for Injection Attempts
**Task**: Test WIQL and input injection resistance  
**Location**: `PoTool.Tests.Unit/Services/RealTfsClientSecurityTests.cs`  
**Test Cases**:
- WIQL injection with single quotes
- WIQL injection with brackets
- Area path with special characters
- SQL injection attempts (should be blocked by EF Core)

---

## Phase 3: Exception Handling Hardening (HIGH - Priority 3)

### Goal
Prevent sensitive information leakage through error messages.

### Steps

#### Step 3.1: Create Centralized Exception Filter
**Task**: Implement global exception handler  
**Location**: `PoTool.Api/Filters/SecurityExceptionFilter.cs`  
**Changes**:
- Create exception filter to sanitize all exceptions
- Log full details server-side only
- Return sanitized messages to clients
- Never expose stack traces in production

#### Step 3.2: Apply Filter Globally
**Task**: Register filter in DI  
**Location**: `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`  
**Changes**:
- Add exception filter to MVC options
- Configure based on environment (development vs. production)

#### Step 3.3: Audit and Update Existing Exception Handlers
**Task**: Review all catch blocks  
**Location**: Throughout codebase  
**Changes**:
- Ensure SanitizeErrorMessage is applied
- Remove any direct exception message exposure
- Use structured logging appropriately

---

## Phase 4: Authentication & Authorization (HIGH - Priority 4)

### Goal
Implement proper authentication and authorization for API endpoints.

**Note**: This requires architectural decisions about deployment model and user identity.

### Steps

#### Step 4.1: Design Authentication Strategy
**Task**: Document authentication approach  
**Considerations**:
- Windows Authentication (for domain scenarios)
- API Keys (for programmatic access)
- JWT tokens (for external clients)
- Integration with existing TFS authentication

#### Step 4.2: Implement Authentication Middleware
**Task**: Add authentication to API  
**Location**: `PoTool.Api/Configuration/`  
**Changes**:
- Add authentication middleware
- Configure authentication schemes
- Update Blazor client to include auth tokens

#### Step 4.3: Add Authorization Policies
**Task**: Implement role-based access control  
**Location**: `PoTool.Api/Configuration/`  
**Changes**:
- Define authorization policies
- Add [Authorize] attributes to controllers
- Implement fine-grained permissions

#### Step 4.4: Add CSRF Protection
**Task**: Implement antiforgery token validation  
**Location**: `PoTool.Api/Configuration/`  
**Changes**:
- Enable antiforgery tokens
- Add validation middleware
- Update Blazor client to send tokens

---

## Phase 5: Security Headers & HTTPS (MEDIUM - Priority 5)

### Goal
Add defense-in-depth security headers and enforce HTTPS.

### Steps

#### Step 5.1: Add Security Headers Middleware
**Task**: Configure security headers  
**Location**: `PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs`  
**Headers to Add**:
- X-Frame-Options: DENY
- X-Content-Type-Options: nosniff
- Content-Security-Policy: (restrictive policy)
- Referrer-Policy: strict-origin-when-cross-origin
- Permissions-Policy: (disable unnecessary features)

#### Step 5.2: Enable HSTS
**Task**: Add HTTP Strict Transport Security  
**Location**: `PoTool.Api/Configuration/`  
**Changes**:
- Add HSTS middleware
- Configure HSTS max-age
- Enable HTTPS redirection

---

## Phase 6: Rate Limiting & DoS Protection (MEDIUM - Priority 6)

### Goal
Prevent abuse and resource exhaustion.

### Steps

#### Step 6.1: Add Rate Limiting Middleware
**Task**: Implement rate limiting  
**Library**: AspNetCoreRateLimit  
**Configuration**:
- Per-client limits
- Per-endpoint limits
- Stricter limits for expensive operations

---

## Phase 7: Logging & Monitoring (MEDIUM - Priority 7)

### Goal
Improve security logging without exposing sensitive data.

### Steps

#### Step 7.1: Implement Log Sanitization
**Task**: Create log sanitization helper  
**Location**: `PoTool.Shared/Helpers/LogSanitizer.cs`  
**Features**:
- Remove/mask sensitive data
- Structured logging support
- Configurable sensitivity levels

#### Step 7.2: Add Security Event Logging
**Task**: Log security-relevant events  
**Events to Log**:
- Authentication failures
- Authorization failures
- Input validation failures
- Rate limit exceedances
- Unusual access patterns

---

## Phase 8: JSON Validation (MEDIUM - Priority 8)

### Goal
Protect against malformed and malicious JSON payloads.

### Steps

#### Step 8.1: Add JSON Schema Validation
**Task**: Validate TFS responses  
**Location**: `PoTool.Api/Services/RealTfsClient.cs`  
**Changes**:
- Define expected JSON schemas
- Validate responses before processing
- Reject oversized payloads

---

## Testing Strategy

### Security Testing Checklist
For each phase, ensure:
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Manual security testing performed
- [ ] Code review completed
- [ ] Documentation updated

### Security Test Suite
Create dedicated security test project:
- `PoTool.Tests.Security/`
  - XSS tests
  - Injection tests
  - Authentication tests
  - Authorization tests
  - Error handling tests

---

## Rollout Strategy

### Development
1. Implement changes in feature branches
2. Run full test suite
3. Perform security-focused code review
4. Merge to development

### Staging
1. Deploy to staging environment
2. Run security test suite
3. Perform penetration testing
4. Verify no regressions

### Production
1. Deploy during maintenance window
2. Monitor for errors
3. Verify security controls active
4. Update incident response procedures

---

## Success Criteria

### Phase 1 (XSS) Complete When:
- [ ] HtmlSanitizer package added
- [ ] Sanitization helper implemented
- [ ] WorkItemDetailPanel updated
- [ ] All tests pass (unit + integration)
- [ ] Manual testing confirms no script execution
- [ ] Code review approved
- [ ] Documentation complete

### Overall Success Criteria:
- [ ] All critical vulnerabilities addressed
- [ ] All high vulnerabilities addressed
- [ ] Security test suite implemented
- [ ] Documentation complete
- [ ] Team trained on secure coding practices

---

## Risk Management

### Risks During Implementation

**Risk**: Sanitization breaks legitimate HTML formatting  
**Mitigation**: Comprehensive testing with real TFS data; allowlist can be expanded if needed

**Risk**: Performance impact from sanitization  
**Mitigation**: Lazy initialization; benchmark performance; consider caching

**Risk**: Authentication implementation blocks legitimate users  
**Mitigation**: Phased rollout; comprehensive testing; fallback mechanisms

**Risk**: Changes break existing functionality  
**Mitigation**: Extensive regression testing; incremental changes; feature flags

---

## Maintenance Plan

### Ongoing Security Activities

1. **Regular Dependency Updates**
   - Monitor security advisories
   - Update HtmlSanitizer and other security libraries
   - Run automated dependency scanning

2. **Periodic Security Reviews**
   - Quarterly code audits
   - Annual penetration testing
   - Review security logs

3. **Security Training**
   - Onboarding security training for new developers
   - Annual security refresher training
   - Share security bulletins and best practices

4. **Incident Response**
   - Define security incident process
   - Maintain incident response runbook
   - Conduct incident response drills

---

## Timeline Estimate

**Phase 1 (XSS)**: 2-3 days  
**Phase 2 (Input Validation)**: 2-3 days  
**Phase 3 (Exception Handling)**: 2 days  
**Phase 4 (Auth/Authz)**: 5-7 days (requires design decisions)  
**Phase 5 (Security Headers)**: 1 day  
**Phase 6 (Rate Limiting)**: 2 days  
**Phase 7 (Logging)**: 2-3 days  
**Phase 8 (JSON Validation)**: 2 days  

**Total Estimated Time**: 18-27 days (depending on architectural decisions for Phase 4)

---

## Conclusion

This phased approach ensures:
- Critical vulnerabilities are addressed first
- Changes are small, reviewable, and testable
- Security is improved incrementally
- Minimal disruption to development
- Long-term security posture is established

Start with Phase 1 (XSS) immediately, as it's critical and self-contained.
