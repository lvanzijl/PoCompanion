using PoTool.Core;

namespace PoTool.Tests.Unit;

[TestClass]
public sealed class RevisionFieldWhitelistTests
{
    [TestMethod]
    public void Fields_IncludePhaseACorrectionFields()
    {
        CollectionAssert.Contains(RevisionFieldWhitelist.Fields.ToList(), "Rhodium.Funding.ProjectNumber");
        CollectionAssert.Contains(RevisionFieldWhitelist.Fields.ToList(), "Rhodium.Funding.ProjectElement");
        CollectionAssert.Contains(RevisionFieldWhitelist.Fields.ToList(), "Microsoft.VSTS.Common.TimeCriticality");
    }

    [TestMethod]
    public void Fields_DoNotContainDuplicates()
    {
        var fields = RevisionFieldWhitelist.Fields.ToList();

        Assert.AreEqual(fields.Count, fields.Distinct(StringComparer.Ordinal).Count());
    }
}
