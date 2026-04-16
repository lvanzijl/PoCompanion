using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.DataState;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class CacheStatePresentationTests
{
    [TestMethod]
    public void ToUiDataState_NotReady_MapsToLoading()
    {
        Assert.AreEqual(UiDataState.Loading, CacheStatePresentation.ToUiDataState(DataStateDto.NotReady));
        Assert.AreEqual(UiDataState.Loading, CacheStatePresentation.ToUiDataState(DataStateResultStatus.NotReady));
    }
}
