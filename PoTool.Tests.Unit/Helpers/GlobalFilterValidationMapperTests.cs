using PoTool.Client.Helpers;
using PoTool.Client.Models;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class GlobalFilterValidationMapperTests
{
    [TestMethod]
    public void NormalizeFields_NormalizesBackendFieldNames()
    {
        var fields = GlobalFilterValidationMapper.NormalizeFields(["ProductIds", "teamId", "windowEndUtc"]);

        CollectionAssert.AreEquivalent(
            new[] { GlobalFilterValidationMapper.ProductIds, GlobalFilterValidationMapper.TeamIds, GlobalFilterValidationMapper.Time },
            fields.ToArray());
    }

    [TestMethod]
    public void MatchesControl_TimeFieldMatchesRangeControls()
    {
        var invalidFields = new[] { GlobalFilterValidationMapper.Time };

        Assert.IsTrue(GlobalFilterValidationMapper.MatchesControl(invalidFields, GlobalFilterValidationMapper.StartSprintId, FilterTimeMode.Range));
        Assert.IsTrue(GlobalFilterValidationMapper.MatchesControl(invalidFields, GlobalFilterValidationMapper.EndSprintId, FilterTimeMode.Range));
        Assert.IsFalse(GlobalFilterValidationMapper.MatchesControl(invalidFields, GlobalFilterValidationMapper.TeamIds, FilterTimeMode.Range));
    }
}
