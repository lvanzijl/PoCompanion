namespace PoTool.Shared.Helpers;

/// <summary>
/// Provides HTML sanitization for user-generated content to prevent XSS attacks.
/// Uses allowlist-based approach to permit only safe HTML elements and attributes.
/// Thread-safe singleton pattern with lazy initialization.
/// </summary>
public static class HtmlSanitizationHelper
{
    /// <summary>
    /// Lazy-initialized HTML sanitizer instance with secure configuration.
    /// Configured to allow only safe formatting tags and attributes.
    /// </summary>
    private static readonly Lazy<Ganss.Xss.HtmlSanitizer> _sanitizer = new(() =>
    {
        var sanitizer = new Ganss.Xss.HtmlSanitizer();
        
        // Configure allowed tags (basic formatting only)
        // Start with empty set and explicitly add safe tags
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.Add("p");
        sanitizer.AllowedTags.Add("br");
        sanitizer.AllowedTags.Add("strong");
        sanitizer.AllowedTags.Add("b");
        sanitizer.AllowedTags.Add("em");
        sanitizer.AllowedTags.Add("i");
        sanitizer.AllowedTags.Add("u");
        sanitizer.AllowedTags.Add("ul");
        sanitizer.AllowedTags.Add("ol");
        sanitizer.AllowedTags.Add("li");
        sanitizer.AllowedTags.Add("a");
        sanitizer.AllowedTags.Add("div");
        sanitizer.AllowedTags.Add("span");
        sanitizer.AllowedTags.Add("h1");
        sanitizer.AllowedTags.Add("h2");
        sanitizer.AllowedTags.Add("h3");
        sanitizer.AllowedTags.Add("h4");
        sanitizer.AllowedTags.Add("h5");
        sanitizer.AllowedTags.Add("h6");
        sanitizer.AllowedTags.Add("table");
        sanitizer.AllowedTags.Add("thead");
        sanitizer.AllowedTags.Add("tbody");
        sanitizer.AllowedTags.Add("tr");
        sanitizer.AllowedTags.Add("th");
        sanitizer.AllowedTags.Add("td");
        sanitizer.AllowedTags.Add("code");
        sanitizer.AllowedTags.Add("pre");
        sanitizer.AllowedTags.Add("blockquote");
        
        // Configure allowed attributes
        // Start with empty set and explicitly add safe attributes
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");  // For <a> tags
        sanitizer.AllowedAttributes.Add("title"); // For tooltips
        sanitizer.AllowedAttributes.Add("class"); // For styling (CSS classes are safe)
        
        // Configure allowed schemes for URLs
        // Only allow http/https, block javascript:, data:, file:, etc.
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        
        // Remove any potentially dangerous CSS properties
        sanitizer.AllowedCssProperties.Clear();
        
        // Don't allow data attributes (can contain arbitrary data)
        sanitizer.AllowDataAttributes = false;
        
        return sanitizer;
    });
    
    /// <summary>
    /// Sanitizes HTML content by removing potentially dangerous elements and attributes.
    /// Only allows safe formatting tags and attributes defined in the allowlist.
    /// </summary>
    /// <param name="html">Raw HTML content to sanitize. Can be null or empty.</param>
    /// <returns>Sanitized HTML safe for rendering with MarkupString. Returns empty string for null/empty input.</returns>
    /// <remarks>
    /// This method is thread-safe and performant due to lazy initialization of the sanitizer.
    /// Use this method before converting any user-generated HTML to MarkupString in Blazor components.
    /// 
    /// Example usage in Razor component:
    /// <code>
    /// @using PoTool.Shared.Helpers
    /// &lt;div&gt;@((MarkupString)HtmlSanitizationHelper.Sanitize(description))&lt;/div&gt;
    /// </code>
    /// </remarks>
    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;
            
        return _sanitizer.Value.Sanitize(html);
    }
}
