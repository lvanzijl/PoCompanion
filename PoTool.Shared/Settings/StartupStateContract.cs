namespace PoTool.Shared.Settings;

/// <summary>
/// Canonical constants shared by the backend startup-state authority and the client startup gate.
/// </summary>
public static class StartupStateContract
{
    public const int SyncAttemptToleranceSeconds = 5;

    public static readonly string[] StartupFlowPaths =
    [
        "/",
        "/profiles",
        "/sync-gate",
        "/startup-blocked"
    ];
}
