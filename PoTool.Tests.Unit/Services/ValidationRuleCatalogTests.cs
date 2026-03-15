using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class ValidationRuleCatalogTests
{
    [TestMethod]
    public void Rc2_PreservesCanonicalFamily_AndUsesSingleUiAlias()
    {
        var descriptor = ValidationRuleCatalog.KnownRules["RC-2"];

        Assert.AreEqual("RC-2", descriptor.RuleId);
        Assert.AreEqual("RC", descriptor.FamilyKey);
        Assert.AreEqual("EFF", descriptor.UiCategoryKey);
        Assert.AreEqual(ValidationCategory.MissingEffort, descriptor.Category);
    }

    [TestMethod]
    public void NonAliasedRule_PassesThroughUnchangedUiCategory()
    {
        var descriptor = ValidationRuleCatalog.KnownRules["RC-1"];

        Assert.AreEqual("RC", descriptor.FamilyKey);
        Assert.AreEqual("RC", descriptor.UiCategoryKey);
        Assert.AreEqual(ValidationCategory.RefinementCompleteness, descriptor.Category);
    }
}
