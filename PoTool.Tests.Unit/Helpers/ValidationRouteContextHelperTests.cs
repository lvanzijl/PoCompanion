using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Helpers;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public class ValidationRouteContextHelperTests
{
    [TestMethod]
    public void TryNormalizeCategory_KnownCategory_NormalizesCase()
    {
        var result = ValidationRouteContextHelper.TryNormalizeCategory("eff", out var categoryKey);

        Assert.IsTrue(result);
        Assert.AreEqual("EFF", categoryKey);
    }

    [TestMethod]
    public void TryNormalizeCategory_UnknownCategory_ReturnsFalse()
    {
        var result = ValidationRouteContextHelper.TryNormalizeCategory("bad", out var categoryKey);

        Assert.IsFalse(result);
        Assert.AreEqual(string.Empty, categoryKey);
    }

    [TestMethod]
    public void TryNormalizeRuleForCategory_KnownUiCategoryRule_NormalizesCase()
    {
        var result = ValidationRouteContextHelper.TryNormalizeRuleForCategory("rc-2", "EFF", out var ruleId);

        Assert.IsTrue(result);
        Assert.AreEqual("RC-2", ruleId);
    }

    [TestMethod]
    public void TryNormalizeRuleForCategory_RuleInWrongUiCategory_ReturnsFalse()
    {
        var result = ValidationRouteContextHelper.TryNormalizeRuleForCategory("RC-2", "RC", out var ruleId);

        Assert.IsFalse(result);
        Assert.AreEqual(string.Empty, ruleId);
    }

    [TestMethod]
    public void TryNormalizeRuleForCategory_UnrelatedRuleInWrongCategory_ReturnsFalse()
    {
        var result = ValidationRouteContextHelper.TryNormalizeRuleForCategory("SI-1", "EFF", out var ruleId);

        Assert.IsFalse(result);
        Assert.AreEqual(string.Empty, ruleId);
    }
}
