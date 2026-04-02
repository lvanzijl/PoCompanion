using System.Collections.Specialized;

using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class RouteStateGuardTests
{
    [TestMethod]
    public void TryGetRequiredQueryValue_ReturnsFalse_WhenMissing()
    {
        var query = new NameValueCollection();

        var found = RouteStateGuard.TryGetRequiredQueryValue(query, "category", out var value);

        Assert.IsFalse(found);
        Assert.AreEqual(string.Empty, value);
    }

    [TestMethod]
    public void TryGetRequiredQueryValue_TrimsWhitespace_WhenPresent()
    {
        var query = new NameValueCollection
        {
            ["category"] = "  SI  "
        };

        var found = RouteStateGuard.TryGetRequiredQueryValue(query, "category", out var value);

        Assert.IsTrue(found);
        Assert.AreEqual("SI", value);
    }

    [TestMethod]
    public void CreateMissingQueryParameterError_ReturnsGuidedMessage()
    {
        var response = RouteStateGuard.CreateMissingQueryParameterError(
            "ruleId",
            "Open a queue and start a fix session from there.");

        Assert.AreEqual("This page needs 'ruleId' before it can open.", response.UserMessage);
        Assert.AreEqual("Open a queue and start a fix session from there.", response.Suggestion);
    }

    [TestMethod]
    public void CreateSafeErrorResponse_StripsTechnicalDetails()
    {
        var service = new ErrorMessageService();

        var response = RouteStateGuard.CreateSafeErrorResponse(
            service,
            new InvalidOperationException("Status: 500 Response: stack details"),
            "Portfolio history");

        Assert.AreEqual("Portfolio history: Server error occurred. Please try again later.", response.UserMessage);
        Assert.AreEqual(500, response.TechnicalDetails.StatusCode);
        Assert.IsNull(response.TechnicalDetails.ExceptionMessage);
        Assert.IsNull(response.TechnicalDetails.StackTrace);
    }

    [TestMethod]
    public void ValidationCategoryMeta_IsSupported_RecognizesCanonicalCategories()
    {
        Assert.IsTrue(ValidationCategoryMeta.IsSupported("si"));
        Assert.IsTrue(ValidationCategoryMeta.IsSupported("RR"));
        Assert.IsFalse(ValidationCategoryMeta.IsSupported("unknown"));
        Assert.IsFalse(ValidationCategoryMeta.IsSupported(null));
    }
}
