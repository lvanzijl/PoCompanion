# HTML Sanitization Security Guidelines

**Document Version**: 1.0  
**Last Updated**: 2026-01-13  
**Related**: SECURITY_AUDIT_REPORT.md, SECURITY_FIX_IMPLEMENTATION_PLAN.md

---

## Overview

This document provides guidelines for HTML sanitization in the PO Companion application to prevent Cross-Site Scripting (XSS) attacks. All developers must follow these guidelines when handling user-generated HTML content.

---

## The XSS Threat

### What is XSS?

Cross-Site Scripting (XSS) is a security vulnerability that allows attackers to inject malicious scripts into web pages viewed by other users. In the context of PO Companion:

1. An attacker with TFS write access adds malicious HTML/JavaScript to a work item description
2. When a Product Owner views the work item, the malicious script executes in their browser
3. The script can:
   - Steal session tokens and credentials
   - Perform actions on behalf of the user
   - Modify the page to phish for additional information
   - Redirect to malicious sites

### Why Blazor `MarkupString` is Dangerous

Blazor's `MarkupString` type bypasses HTML encoding and renders content as raw HTML. This is necessary for displaying formatted TFS descriptions but creates an XSS risk:

```razor
@* UNSAFE - DO NOT USE *@
<div>@((MarkupString)workItem.Description)</div>
```

If `workItem.Description` contains `<script>alert('XSS')</script>`, the script will execute.

---

## The Solution: HtmlSanitizationHelper

### Usage Pattern

**ALWAYS** sanitize HTML before converting to `MarkupString`:

```razor
@using PoTool.Shared.Helpers

@* SAFE - USE THIS PATTERN *@
<div>@((MarkupString)HtmlSanitizationHelper.Sanitize(workItem.Description))</div>
```

### How It Works

`HtmlSanitizationHelper` uses a **whitelist-based approach**:

1. **Whitelist safe tags**: Only allows known-safe HTML elements (p, strong, em, ul, li, a, etc.)
2. **Whitelist safe attributes**: Only allows safe attributes (href, title, class)
3. **Whitelist safe URL schemes**: Only allows http:// and https:// URLs
4. **Remove everything else**: Scripts, event handlers, dangerous CSS, etc.

### What's Allowed

The following HTML is **safe** and will be preserved:

- **Text formatting**: `<strong>`, `<em>`, `<u>`, `<b>`, `<i>`
- **Paragraphs**: `<p>`, `<br>`, `<div>`, `<span>`
- **Headings**: `<h1>` through `<h6>`
- **Lists**: `<ul>`, `<ol>`, `<li>`
- **Links**: `<a href="https://...">` (only http/https)
- **Tables**: `<table>`, `<thead>`, `<tbody>`, `<tr>`, `<th>`, `<td>`
- **Code**: `<code>`, `<pre>`, `<blockquote>`

### What's Blocked

The following is **dangerous** and will be removed:

- **Scripts**: `<script>` tags and `javascript:` URLs
- **Event handlers**: `onclick`, `onerror`, `onload`, etc.
- **Frames**: `<iframe>`, `<frame>`, `<frameset>`
- **Objects**: `<object>`, `<embed>`, `<applet>`
- **Base/Meta tags**: `<base>`, `<meta>` (can redirect or modify page)
- **Forms**: `<form>`, `<input>`, `<button>` (can phish credentials)
- **Dangerous CSS**: `expression()`, `behavior()`, `@import`
- **Data URIs**: `data:text/html,...` (can contain embedded scripts)
- **SVG scripts**: Scripts within `<svg>` tags
- **Style attributes**: Potentially dangerous inline CSS

---

## When to Sanitize

### MUST Sanitize

You **MUST** sanitize when:

1. **Displaying TFS work item descriptions** - These come from TFS and may contain HTML
2. **Displaying any user-generated HTML** - Comments, notes, rich text fields
3. **Before any `MarkupString` conversion** - If content might contain HTML

### Don't Need to Sanitize

You **DON'T** need to sanitize when:

1. **Displaying plain text** - Use `@workItem.Title` (Blazor auto-encodes)
2. **Displaying server-generated HTML** - If the server controls the content
3. **Hardcoded HTML in Razor** - `<p>Hardcoded text</p>` is safe

### Example: Safe vs. Unsafe

```razor
@* Safe - plain text is auto-encoded by Blazor *@
<h2>@workItem.Title</h2>

@* Safe - hardcoded HTML *@
<p>This is a <strong>hardcoded</strong> paragraph</p>

@* UNSAFE - raw MarkupString from TFS *@
<div>@((MarkupString)workItem.Description)</div>

@* SAFE - sanitized MarkupString from TFS *@
<div>@((MarkupString)HtmlSanitizationHelper.Sanitize(workItem.Description))</div>
```

---

## Testing Guidelines

### Test Cases for Sanitization

When adding new UI components that display HTML, add tests for:

1. **Null/empty input** - Should return empty string
2. **Plain text** - Should pass through unchanged
3. **Safe HTML** - Should preserve formatting
4. **Script tags** - Should be removed
5. **Event handlers** - Should be removed
6. **JavaScript URLs** - Should be removed
7. **Mixed content** - Safe parts preserved, dangerous parts removed

### Example Test

```razor
[TestMethod]
public void WorkItemDescription_WithScript_DoesNotExecuteScript()
{
    // Arrange
    var workItem = new WorkItemDto
    {
        Description = "<p>Safe text</p><script>alert('XSS')</script>"
    };
    
    // Act
    var sanitized = HtmlSanitizationHelper.Sanitize(workItem.Description);
    
    // Assert
    Assert.IsFalse(sanitized.Contains("<script>"));
    Assert.IsTrue(sanitized.Contains("Safe text"));
}
```

---

## Code Review Checklist

When reviewing code that handles HTML:

- [ ] Is `MarkupString` used? If yes:
  - [ ] Is the content sanitized with `HtmlSanitizationHelper.Sanitize()`?
  - [ ] Is the content from a trusted source (hardcoded, server-generated)?
- [ ] Are there new Razor components displaying TFS data?
  - [ ] Do they sanitize HTML fields (descriptions, comments)?
- [ ] Are there tests for XSS prevention?
  - [ ] Tests with script tags?
  - [ ] Tests with event handlers?
  - [ ] Tests with JavaScript URLs?

---

## Performance Considerations

### Sanitization Performance

- **Lazy initialization**: Sanitizer is initialized once and reused
- **Thread-safe**: Safe to call from multiple threads simultaneously
- **Minimal overhead**: Typical sanitization takes < 1ms per description
- **No caching needed**: Sanitization is fast enough for real-time rendering

### When to Optimize

Only optimize if:

1. Profiling shows sanitization is a bottleneck
2. You're sanitizing thousands of descriptions at once
3. Descriptions are very large (> 100KB)

In those cases, consider:
- Caching sanitized HTML in the database
- Sanitizing in batches during off-peak hours
- Storing both raw and sanitized versions

---

## Common Mistakes to Avoid

### ❌ Mistake 1: Forgetting to Sanitize

```razor
@* WRONG - Unsanitized MarkupString *@
<div>@((MarkupString)description)</div>
```

### ✅ Correct:

```razor
@* RIGHT - Sanitized MarkupString *@
<div>@((MarkupString)HtmlSanitizationHelper.Sanitize(description))</div>
```

### ❌ Mistake 2: Sanitizing Plain Text

```razor
@* WRONG - Unnecessary sanitization of plain text *@
<h2>@HtmlSanitizationHelper.Sanitize(workItem.Title)</h2>
```

### ✅ Correct:

```razor
@* RIGHT - Blazor auto-encodes plain text *@
<h2>@workItem.Title</h2>
```

### ❌ Mistake 3: Custom HTML Filtering

```csharp
// WRONG - Custom filtering is error-prone
public string RemoveScripts(string html)
{
    return html.Replace("<script>", "").Replace("</script>", "");
}
```

### ✅ Correct:

```csharp
// RIGHT - Use the established sanitizer
public string SanitizeHtml(string html)
{
    return HtmlSanitizationHelper.Sanitize(html);
}
```

### ❌ Mistake 4: Trusting "Safe" Sources

```csharp
// WRONG - Assuming all TFS data is safe
<div>@((MarkupString)tfsWorkItem.Description)</div>
```

### ✅ Correct:

```csharp
// RIGHT - Always sanitize external data
<div>@((MarkupString)HtmlSanitizationHelper.Sanitize(tfsWorkItem.Description))</div>
```

---

## Extending the Whitelist

### When to Extend

Only extend the allowed tags/attributes if:

1. There's a legitimate business requirement
2. Security review has been completed
3. The new tag/attribute is demonstrably safe

### How to Extend

Edit `PoTool.Shared/Helpers/HtmlSanitizationHelper.cs`:

```csharp
// Adding a new safe tag
sanitizer.AllowedTags.Add("mark"); // For highlighting

// Adding a new safe attribute
sanitizer.AllowedAttributes.Add("data-id"); // For custom data
```

Then:
1. Add tests covering the new tag/attribute
2. Document why it's safe
3. Get security review approval
4. Update this document

---

## Incident Response

### If XSS is Discovered

1. **Immediately assess impact**:
   - Which users viewed the malicious content?
   - What data might have been compromised?
   - Were any actions performed on behalf of users?

2. **Contain the threat**:
   - Identify and sanitize/remove the malicious work item
   - Check for other work items from the same attacker
   - Review audit logs for suspicious activity

3. **Fix the vulnerability**:
   - Apply sanitization to the affected code
   - Add tests to prevent regression
   - Deploy fix urgently

4. **Post-incident**:
   - Notify affected users
   - Update security guidelines
   - Add monitoring for similar attacks
   - Conduct security training

---

## Additional Resources

### Security References

- **OWASP XSS Prevention Cheat Sheet**: https://cheatsheetseries.owasp.org/cheatsheets/Cross_Site_Scripting_Prevention_Cheat_Sheet.html
- **HtmlSanitizer Documentation**: https://github.com/mganss/HtmlSanitizer
- **Blazor Security**: https://learn.microsoft.com/aspnet/core/blazor/security/

### Internal Documents

- `SECURITY_AUDIT_REPORT.md` - Comprehensive security findings
- `SECURITY_FIX_IMPLEMENTATION_PLAN.md` - Implementation roadmap
- `docs/COPILOT_ARCHITECTURE_CONTRACT.md` - Architecture rules

### Getting Help

- Security questions: Ask the security team
- Implementation questions: Reference this document and tests
- Incident response: Follow the incident response plan

---

## Document History

| Version | Date       | Author              | Changes                          |
|---------|------------|---------------------|----------------------------------|
| 1.0     | 2026-01-13 | Security Review Team | Initial document, XSS fix        |

---

## Appendix: XSS Attack Examples

### Example 1: Script Tag Injection

**Malicious TFS Description**:
```html
<p>Valid content</p>
<script>
  fetch('/api/steal-data', {
    method: 'POST',
    body: JSON.stringify({ sessionToken: document.cookie })
  });
</script>
```

**After Sanitization**:
```html
<p>Valid content</p>

```

### Example 2: Event Handler Injection

**Malicious TFS Description**:
```html
<img src="x" onerror="alert(document.cookie)">
```

**After Sanitization**:
```html

```

### Example 3: JavaScript URL

**Malicious TFS Description**:
```html
<a href="javascript:void(fetch('/api/attack'))">Click me</a>
```

**After Sanitization**:
```html
<a>Click me</a>
```

### Example 4: Data URI Attack

**Malicious TFS Description**:
```html
<a href="data:text/html,<script>alert('XSS')</script>">Link</a>
```

**After Sanitization**:
```html
<a>Link</a>
```

All of these attacks are automatically blocked by `HtmlSanitizationHelper`.
