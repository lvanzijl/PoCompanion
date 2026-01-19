using System.Security.Cryptography;
using System.Text;

namespace PoTool.Client.Services;

/// <summary>
/// Manages onboarding wizard state, particularly TFS verification status.
/// This is a scoped service that lives for the duration of the wizard dialog.
/// </summary>
public class OnboardingWizardState : IOnboardingWizardState
{
    private const string FingerprintSeparator = "|";
    private bool _tfsVerified;
    private string? _verifiedFingerprint;

    /// <inheritdoc/>
    public bool TfsVerified => _tfsVerified;

    /// <inheritdoc/>
    public bool TfsDirty { get; private set; }

    /// <inheritdoc/>
    public void MarkTfsVerified(string url, string project)
    {
        _tfsVerified = true;
        TfsDirty = false;
        _verifiedFingerprint = ComputeFingerprint(url, project);
    }

    /// <inheritdoc/>
    public void MarkTfsUnverified()
    {
        _tfsVerified = false;
        TfsDirty = false;
        _verifiedFingerprint = null;
    }

    /// <inheritdoc/>
    public bool CheckTfsFieldsUnchanged(string url, string project)
    {
        if (!_tfsVerified || _verifiedFingerprint == null)
        {
            return false;
        }

        var currentFingerprint = ComputeFingerprint(url, project);
        var unchanged = currentFingerprint == _verifiedFingerprint;

        if (!unchanged)
        {
            TfsDirty = true;
        }

        return unchanged;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        _tfsVerified = false;
        TfsDirty = false;
        _verifiedFingerprint = null;
    }

    private static string ComputeFingerprint(string url, string project)
    {
        var combined = $"{url?.Trim() ?? ""}{FingerprintSeparator}{project?.Trim() ?? ""}";
        var bytes = Encoding.UTF8.GetBytes(combined);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
