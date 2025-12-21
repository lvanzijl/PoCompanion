using PoTool.Client.Services;

namespace PoTool.Maui.Services;

/// <summary>
/// MAUI-specific implementation of ISecureStorageService using MAUI SecureStorage.
/// SecureStorage uses platform-native secure storage:
/// - Windows: Windows Credential Manager (DPAPI encryption)
/// - macOS: Keychain Services
/// - Linux: Secret Service API (libsecret/GNOME Keyring)
/// </summary>
public class MauiSecureStorageService : ISecureStorageService
{
    /// <inheritdoc/>
    public async Task SetAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch (Exception ex)
        {
            // Log and re-throw - critical operation that should not fail silently
            System.Diagnostics.Debug.WriteLine($"Error storing secure value for key '{key}': {ex.Message}");
            throw new InvalidOperationException($"Failed to store secure value for key '{key}'", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch (Exception ex)
        {
            // Log the error but return null - this allows graceful degradation
            System.Diagnostics.Debug.WriteLine($"Error retrieving secure value for key '{key}': {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc/>
    public bool Remove(string key)
    {
        try
        {
            return SecureStorage.Default.Remove(key);
        }
        catch (Exception ex)
        {
            // Log the error but return false
            System.Diagnostics.Debug.WriteLine($"Error removing secure value for key '{key}': {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public void RemoveAll()
    {
        try
        {
            SecureStorage.Default.RemoveAll();
        }
        catch (Exception ex)
        {
            // Log and re-throw - critical operation that should not fail silently
            System.Diagnostics.Debug.WriteLine($"Error removing all secure values: {ex.Message}");
            throw new InvalidOperationException("Failed to remove all secure values", ex);
        }
    }
}
