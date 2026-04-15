using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class TrendUiStateFactoryTests
{
    [TestMethod]
    public void InvalidFilter_FromBlockingMessages_UsesInvalidUiStateAndCombinedReason()
    {
        var evaluation = new FilterExecutionGateResult(
            CanExecuteQueries: false,
            BlockingMessages: ["Select a team.", "Select a sprint range."],
            NotAppliedMessages: [],
            CorrectionMessages: []);

        var state = TrendUiStateFactory.InvalidFilter<string>(evaluation, "Fallback");

        Assert.AreEqual(UiDataState.Invalid, state.UiState);
        Assert.AreEqual("Select a team. Select a sprint range.", state.Reason);
    }
}
