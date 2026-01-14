using PoTool.Shared.Helpers;

// Suppress MSTEST0037: This analyzer prefers Assert.Contains/DoesNotContain over Assert.IsTrue/IsFalse with string.Contains().
// While the suggested methods provide better error messages, the current pattern is clear and consistent with existing tests.
// Consider migrating to the new pattern in a future refactoring for improved test output diagnostics.
#pragma warning disable MSTEST0037 // Use Assert.Contains/DoesNotContain instead of Assert.IsTrue/IsFalse

namespace PoTool.Tests.Unit.Helpers;

/// <summary>
/// Security-focused tests for HTML sanitization to prevent XSS attacks.
/// Tests various XSS payloads and ensures safe HTML is preserved.
/// </summary>
[TestClass]
public class HtmlSanitizationHelperTests
{
    [TestMethod]
    public void Sanitize_NullInput_ReturnsEmpty()
    {
        // Act
        var result = HtmlSanitizationHelper.Sanitize(null);
        
        // Assert
        Assert.AreEqual(string.Empty, result);
    }
    
    [TestMethod]
    public void Sanitize_EmptyInput_ReturnsEmpty()
    {
        // Act
        var result = HtmlSanitizationHelper.Sanitize(string.Empty);
        
        // Assert
        Assert.AreEqual(string.Empty, result);
    }
    
    [TestMethod]
    public void Sanitize_WhitespaceInput_ReturnsEmpty()
    {
        // Act
        var result = HtmlSanitizationHelper.Sanitize("   \t\n   ");
        
        // Assert
        Assert.AreEqual(string.Empty, result);
    }
    
    [TestMethod]
    public void Sanitize_PlainText_ReturnsUnchanged()
    {
        // Arrange
        var plainText = "This is plain text without any HTML.";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(plainText);
        
        // Assert
        Assert.AreEqual(plainText, result);
    }
    
    [TestMethod]
    public void Sanitize_SafeHtmlFormatting_PreservesFormatting()
    {
        // Arrange
        var safeHtml = "<p>This is <strong>bold</strong> and <em>italic</em> text.</p>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(safeHtml);
        
        // Assert
        Assert.AreEqual(safeHtml, result);
    }
    
    [TestMethod]
    public void Sanitize_SafeList_PreservesListFormatting()
    {
        // Arrange
        var safeHtml = "<ul><li>Item 1</li><li>Item 2</li></ul>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(safeHtml);
        
        // Assert
        Assert.AreEqual(safeHtml, result);
    }
    
    [TestMethod]
    public void Sanitize_SafeTable_PreservesTableFormatting()
    {
        // Arrange
        var safeHtml = "<table><thead><tr><th>Header</th></tr></thead><tbody><tr><td>Data</td></tr></tbody></table>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(safeHtml);
        
        // Assert
        Assert.AreEqual(safeHtml, result);
    }
    
    [TestMethod]
    public void Sanitize_SafeLink_PreservesLink()
    {
        // Arrange
        var safeHtml = "<a href=\"https://example.com\">Click here</a>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(safeHtml);
        
        // Assert
        Assert.AreEqual(safeHtml, result);
    }
    
    [TestMethod]
    public void Sanitize_ScriptTag_RemovesScript()
    {
        // Arrange
        var maliciousHtml = "<p>Hello</p><script>alert('XSS')</script><p>World</p>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(maliciousHtml);
        
        // Assert
        Assert.IsFalse(result.Contains("<script"), "Script tag should be removed");
        Assert.IsFalse(result.Contains("alert"), "Script content should be removed");
        Assert.IsTrue(result.Contains("Hello"), "Safe content should remain");
        Assert.IsTrue(result.Contains("World"), "Safe content should remain");
    }
    
    [TestMethod]
    public void Sanitize_OnEventAttribute_RemovesAttribute()
    {
        // Arrange
        var maliciousHtml = "<div onclick=\"alert('XSS')\">Click me</div>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(maliciousHtml);
        
        // Assert
        Assert.IsFalse(result.Contains("onclick"), "Event handler should be removed");
        Assert.IsFalse(result.Contains("alert"), "Script should be removed");
        Assert.IsTrue(result.Contains("Click me"), "Text content should remain");
    }
    
    [TestMethod]
    public void Sanitize_OnMouseOverAttribute_RemovesAttribute()
    {
        // Arrange
        var maliciousHtml = "<img src=\"x\" onerror=\"alert('XSS')\">";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(maliciousHtml);
        
        // Assert
        Assert.IsFalse(result.Contains("onerror"), "Event handler should be removed");
        Assert.IsFalse(result.Contains("alert"), "Script should be removed");
    }
    
    [TestMethod]
    public void Sanitize_JavascriptUrl_RemovesUrl()
    {
        // Arrange
        var maliciousHtml = "<a href=\"javascript:alert('XSS')\">Click</a>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(maliciousHtml);
        
        // Assert
        Assert.IsFalse(result.Contains("javascript:"), "JavaScript URL should be removed");
        Assert.IsFalse(result.Contains("alert"), "Script should be removed");
    }
    
    [TestMethod]
    public void Sanitize_DataUrl_RemovesUrl()
    {
        // Arrange
        var maliciousHtml = "<a href=\"data:text/html,<script>alert('XSS')</script>\">Click</a>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(maliciousHtml);
        
        // Assert
        Assert.IsFalse(result.Contains("data:"), "Data URL should be removed");
        Assert.IsFalse(result.Contains("<script>"), "Embedded script should be removed");
    }
    
    [TestMethod]
    public void Sanitize_IframeTag_RemovesIframe()
    {
        // Arrange
        var maliciousHtml = "<p>Content</p><iframe src=\"evil.com\"></iframe>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(maliciousHtml);
        
        // Assert
        Assert.IsFalse(result.Contains("<iframe"), "Iframe should be removed");
        Assert.IsFalse(result.Contains("evil.com"), "Iframe source should be removed");
        Assert.IsTrue(result.Contains("Content"), "Safe content should remain");
    }
    
    [TestMethod]
    public void Sanitize_ObjectTag_RemovesObject()
    {
        // Arrange
        var maliciousHtml = "<object data=\"evil.swf\"></object>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(maliciousHtml);
        
        // Assert
        Assert.IsFalse(result.Contains("<object"), "Object tag should be removed");
    }
    
    [TestMethod]
    public void Sanitize_EmbedTag_RemovesEmbed()
    {
        // Arrange
        var maliciousHtml = "<embed src=\"evil.swf\">";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(maliciousHtml);
        
        // Assert
        Assert.IsFalse(result.Contains("<embed"), "Embed tag should be removed");
    }
    
    [TestMethod]
    public void Sanitize_EncodedScript_KeepsEncodedSafely()
    {
        // Arrange - using HTML entity encoding
        var htmlWithEntities = "<p>&lt;script&gt;alert('XSS')&lt;/script&gt;</p>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(htmlWithEntities);
        
        // Assert
        // The sanitizer keeps HTML entities as-is, which is safe
        // They display as text, not as executable script
        Assert.IsTrue(result.Contains("&lt;") || result.Contains("<p>"), "Encoded entities should be preserved or normalized");
        Assert.IsFalse(result.Contains("<script>"), "Should not have actual script tag");
    }
    
    [TestMethod]
    public void Sanitize_MixedContent_RemovesOnlyDangerous()
    {
        // Arrange
        var mixedHtml = @"
            <div>
                <p>Safe paragraph</p>
                <script>alert('XSS')</script>
                <ul>
                    <li onclick=""alert('XSS')"">Item 1</li>
                    <li>Item 2</li>
                </ul>
                <a href=""javascript:void(0)"">Bad link</a>
                <a href=""https://example.com"">Good link</a>
            </div>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(mixedHtml);
        
        // Assert
        Assert.IsTrue(result.Contains("Safe paragraph"), "Safe content should remain");
        Assert.IsTrue(result.Contains("Item 2"), "Safe list items should remain");
        Assert.IsTrue(result.Contains("Good link"), "Safe links should remain");
        Assert.IsTrue(result.Contains("https://example.com"), "Safe URLs should remain");
        Assert.IsFalse(result.Contains("<script"), "Script tags should be removed");
        Assert.IsFalse(result.Contains("onclick"), "Event handlers should be removed");
        Assert.IsFalse(result.Contains("javascript:"), "JavaScript URLs should be removed");
    }
    
    [TestMethod]
    public void Sanitize_RealWorldTfsHtml_PreservesSafe()
    {
        // Arrange - typical TFS work item description with formatting
        var tfsHtml = @"
            <div>
                <p><strong>User Story:</strong></p>
                <p>As a <em>product owner</em>, I want to view work items with <u>formatting</u>.</p>
                <p><strong>Acceptance Criteria:</strong></p>
                <ul>
                    <li>Display formatted descriptions</li>
                    <li>Prevent XSS attacks</li>
                    <li>Maintain existing styling</li>
                </ul>
                <p><a href=""https://docs.microsoft.com/tfs"">Documentation</a></p>
            </div>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(tfsHtml);
        
        // Assert
        Assert.IsTrue(result.Contains("<strong>"), "Bold formatting should be preserved");
        Assert.IsTrue(result.Contains("<em>"), "Italic formatting should be preserved");
        Assert.IsTrue(result.Contains("<u>"), "Underline formatting should be preserved");
        Assert.IsTrue(result.Contains("<ul>"), "Lists should be preserved");
        Assert.IsTrue(result.Contains("<li>"), "List items should be preserved");
        Assert.IsTrue(result.Contains("<a href="), "Links should be preserved");
        Assert.IsTrue(result.Contains("User Story"), "Content should be preserved");
        Assert.IsTrue(result.Contains("Acceptance Criteria"), "Content should be preserved");
    }
    
    [TestMethod]
    public void Sanitize_StyleAttribute_RemovesStyleIfDangerous()
    {
        // Arrange - style attribute with expression (IE specific XSS)
        var maliciousHtml = "<div style=\"width: expression(alert('XSS'))\">Content</div>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(maliciousHtml);
        
        // Assert
        // The sanitizer should remove dangerous CSS
        Assert.IsFalse(result.Contains("expression"), "CSS expression should be removed");
        Assert.IsFalse(result.Contains("alert"), "Script should be removed");
    }
    
    [TestMethod]
    public void Sanitize_SvgWithScript_RemovesScript()
    {
        // Arrange - SVG-based XSS
        var maliciousHtml = "<svg><script>alert('XSS')</script></svg>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(maliciousHtml);
        
        // Assert
        Assert.IsFalse(result.Contains("<script"), "Script in SVG should be removed");
        Assert.IsFalse(result.Contains("alert"), "Script content should be removed");
    }
    
    [TestMethod]
    public void Sanitize_BaseTag_RemovesBaseTag()
    {
        // Arrange - base tag can be used for clickjacking
        var maliciousHtml = "<base href=\"http://evil.com/\"><a href=\"login\">Login</a>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(maliciousHtml);
        
        // Assert
        Assert.IsFalse(result.Contains("<base"), "Base tag should be removed");
        Assert.IsFalse(result.Contains("evil.com"), "Malicious URL should be removed");
    }
    
    [TestMethod]
    public void Sanitize_MetaRefresh_RemovesMetaTag()
    {
        // Arrange - meta refresh can redirect users
        var maliciousHtml = "<meta http-equiv=\"refresh\" content=\"0;url=http://evil.com\">";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(maliciousHtml);
        
        // Assert
        Assert.IsFalse(result.Contains("<meta"), "Meta tag should be removed");
        Assert.IsFalse(result.Contains("evil.com"), "Redirect URL should be removed");
    }
    
    [TestMethod]
    public void Sanitize_VeryLargeInput_HandlesGracefully()
    {
        // Arrange - test performance with large input
        var largeHtml = new string('a', 100000) + "<script>alert('XSS')</script>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(largeHtml);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.Contains("<script"), "Script should be removed even in large input");
    }
    
    [TestMethod]
    public void Sanitize_NestedScripts_RemovesAllScripts()
    {
        // Arrange - nested script attempts
        var maliciousHtml = "<div><p><script>alert('XSS')</script></p><div><script>alert('XSS2')</script></div></div>";
        
        // Act
        var result = HtmlSanitizationHelper.Sanitize(maliciousHtml);
        
        // Assert
        Assert.IsFalse(result.Contains("<script"), "All scripts should be removed");
        Assert.IsFalse(result.Contains("alert"), "All script content should be removed");
    }
    
    [TestMethod]
    public void Sanitize_ThreadSafety_MultipleCallsSucceed()
    {
        // Arrange
        var testHtml = "<p>Test <script>alert('XSS')</script> content</p>";
        var tasks = new Task<string>[100];
        
        // Act - call sanitization from multiple threads
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => HtmlSanitizationHelper.Sanitize(testHtml));
        }
        Task.WaitAll(tasks);
        
        // Assert - all results should be consistent
        var firstResult = tasks[0].Result;
        Assert.IsTrue(tasks.All(t => t.Result == firstResult), "All results should be identical");
        Assert.IsFalse(firstResult.Contains("<script"), "Script should be removed in all results");
    }
}
