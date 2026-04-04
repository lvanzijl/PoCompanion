using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.DataState;

namespace PoTool.Tests.Unit.Client;

[TestClass]
public class ColdCacheNormalizationTests
{
    [TestMethod]
    public void DataStateViewModel_NotReady_MapsToCanonicalNotReadyState()
    {
        var model = new DataStateViewModel<string>(DataStateDto.NotReady, Reason: "Sync pending");

        Assert.AreEqual(UiDataState.NotReady, model.UiState);
        var display = CacheStatePresentation.Create("Delivery trends", model.State, model.Reason);
        Assert.AreEqual("Data not ready", display.Title);
        StringAssert.Contains(display.Message, "Cache not built");
    }

    [TestMethod]
    public void DataStateViewModel_Ready_MapsToCanonicalReadyState()
    {
        var model = DataStateViewModel<int>.Ready(42);

        Assert.AreEqual(UiDataState.Ready, model.UiState);
        Assert.AreEqual(42, model.Data);
    }

    [TestMethod]
    public void DataStateViewModel_Failed_MapsToCanonicalFailedState()
    {
        var model = DataStateViewModel<object>.Failed("Request failed");

        Assert.AreEqual(UiDataState.Failed, model.UiState);
        var display = CacheStatePresentation.Create("Portfolio flow", model.State, model.Reason);
        Assert.AreEqual("Data unavailable", display.Title);
        Assert.AreEqual("Request failed", display.Message);
    }

    [TestMethod]
    public void DataStateViewModel_Empty_MapsToCanonicalEmptyButValidState()
    {
        var model = DataStateViewModel<object>.Empty("No data for the current selection.");

        Assert.AreEqual(UiDataState.EmptyButValid, model.UiState);
        var display = CacheStatePresentation.Create("Portfolio delivery", model.State, model.Reason);
        Assert.AreEqual("No data available", display.Title);
        Assert.AreEqual("No data for the current selection.", display.Message);
    }

    [TestMethod]
    public void StatusTileSignal_NotReady_UsesCanonicalLabel()
    {
        var signal = StatusTileSignal.NotReady("Cache not built yet for the pipeline tile.");

        Assert.AreEqual(TileSignalKind.NotReady, signal.Kind);
        Assert.AreEqual("Data not ready", signal.Label);
        StringAssert.Contains(signal.Tooltip, "Cache not built");
    }
}
