using PoTool.Shared.Settings;
namespace PoTool.Client.Services;

/// <summary>
/// Interface for managing onboarding wizard state, particularly TFS verification status.
/// </summary>
public interface IOnboardingWizardState
{
    /// <summary>
    /// Gets whether TFS configuration has been successfully verified.
    /// </summary>
    bool TfsVerified { get; }

    /// <summary>
    /// Gets whether TFS fields have been modified after successful verification (dirty state).
    /// </summary>
    bool TfsDirty { get; }

    /// <summary>
    /// Marks TFS configuration as successfully verified with a fingerprint of the current values.
    /// </summary>
    /// <param name="url">TFS URL</param>
    /// <param name="project">Project name</param>
    void MarkTfsVerified(
        string url,
        string project,
        RevisionSource revisionSource,
        string? analyticsODataBaseUrl);

    /// <summary>
    /// Marks TFS configuration as not verified (failed verification or field changed).
    /// </summary>
    void MarkTfsUnverified();

    /// <summary>
    /// Checks if the current TFS field values match the verified fingerprint.
    /// If they don't match, automatically marks as dirty.
    /// </summary>
    /// <param name="url">Current TFS URL</param>
    /// <param name="project">Current project name</param>
    /// <returns>True if values match verified state, false if dirty</returns>
    bool CheckTfsFieldsUnchanged(
        string url,
        string project,
        RevisionSource revisionSource,
        string? analyticsODataBaseUrl);

    /// <summary>
    /// Resets all wizard state.
    /// </summary>
    void Reset();
}
