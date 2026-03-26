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
    public void BuildODataRevisionSelectionSpec_ContainsScalarMappingsForPhaseACorrectionFields()
    {
        var selectionSpec = RevisionFieldWhitelist.BuildODataRevisionSelectionSpec();

        var projectNumber = selectionSpec.ParseDescriptors.Single(descriptor => descriptor.RestFieldRef == "Rhodium.Funding.ProjectNumber");
        var projectElement = selectionSpec.ParseDescriptors.Single(descriptor => descriptor.RestFieldRef == "Rhodium.Funding.ProjectElement");
        var timeCriticality = selectionSpec.ParseDescriptors.Single(descriptor => descriptor.RestFieldRef == "Microsoft.VSTS.Common.TimeCriticality");

        Assert.AreEqual("ProjectNumber", projectNumber.ScalarProperty);
        Assert.AreEqual("ProjectElement", projectElement.ScalarProperty);
        Assert.AreEqual("TimeCriticality", timeCriticality.ScalarProperty);
    }
}
