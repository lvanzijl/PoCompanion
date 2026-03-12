using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using PoTool.Client.Components.Common.Compact;

namespace PoTool.Tests.Unit.Components.Common.Compact;

[TestClass]
public class CompactButtonStyleResolverTests
{
    [TestMethod]
    public void Resolve_UtilityRole_ReturnsTextDefaultAppearance()
    {
        var appearance = CompactButtonStyleResolver.Resolve(ButtonRole.Utility);

        Assert.AreEqual(Variant.Text, appearance.Variant);
        Assert.AreEqual(Color.Default, appearance.Color);
        Assert.AreEqual("compact-button--utility", appearance.CssClass);
    }

    [TestMethod]
    public void Resolve_ActionRole_ReturnsOutlinedDefaultAppearance()
    {
        var appearance = CompactButtonStyleResolver.Resolve(ButtonRole.Action);

        Assert.AreEqual(Variant.Outlined, appearance.Variant);
        Assert.AreEqual(Color.Default, appearance.Color);
        Assert.AreEqual("compact-button--action", appearance.CssClass);
    }

    [TestMethod]
    public void Resolve_CriticalRole_ReturnsFilledErrorAppearance()
    {
        var appearance = CompactButtonStyleResolver.Resolve(ButtonRole.Critical);

        Assert.AreEqual(Variant.Filled, appearance.Variant);
        Assert.AreEqual(Color.Error, appearance.Color);
        Assert.AreEqual("compact-button--critical", appearance.CssClass);
    }
}
