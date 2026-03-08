namespace PoTool.Client.Services;

public enum ProductLanePrioritySwapFailureReason
{
    MissingPriority,
    InvalidPriority,
    DuplicatePriority
}

public sealed record ProductLanePrioritySwapPlan(
    int SelectedObjectiveTfsId,
    double SelectedOriginalPriority,
    int NeighborObjectiveTfsId,
    double NeighborOriginalPriority)
{
    public double SelectedWrittenPriority => NeighborOriginalPriority;
    public double NeighborWrittenPriority => SelectedOriginalPriority;
}

public static class ProductLanePrioritySwapPlanner
{
    public static bool TryCreateExactNeighborSwap(
        int selectedObjectiveTfsId,
        double? selectedPriority,
        int neighborObjectiveTfsId,
        double? neighborPriority,
        out ProductLanePrioritySwapPlan? plan,
        out ProductLanePrioritySwapFailureReason? failureReason)
    {
        if (!selectedPriority.HasValue || !neighborPriority.HasValue)
        {
            plan = null;
            failureReason = ProductLanePrioritySwapFailureReason.MissingPriority;
            return false;
        }

        if (!IsUsablePriority(selectedPriority.Value) || !IsUsablePriority(neighborPriority.Value))
        {
            plan = null;
            failureReason = ProductLanePrioritySwapFailureReason.InvalidPriority;
            return false;
        }

        if (selectedPriority.Value == neighborPriority.Value)
        {
            plan = null;
            failureReason = ProductLanePrioritySwapFailureReason.DuplicatePriority;
            return false;
        }

        plan = new ProductLanePrioritySwapPlan(
            selectedObjectiveTfsId,
            selectedPriority.Value,
            neighborObjectiveTfsId,
            neighborPriority.Value);
        failureReason = null;
        return true;
    }

    private static bool IsUsablePriority(double priority)
    {
        return !double.IsNaN(priority) && !double.IsInfinity(priority);
    }
}
