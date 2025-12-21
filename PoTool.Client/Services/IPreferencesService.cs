namespace PoTool.Client.Services;

/// <summary>
/// Abstraction for preferences storage that works across platforms.
/// For MAUI Hybrid, this wraps MAUI Preferences.
/// For Blazor WebAssembly, this would wrap local storage.
/// </summary>
public interface IPreferencesService
{
    /// <summary>
    /// Gets a boolean value from preferences.
    /// </summary>
    bool GetBool(string key, bool defaultValue);

    /// <summary>
    /// Sets a boolean value in preferences.
    /// </summary>
    void SetBool(string key, bool value);

    /// <summary>
    /// Removes a key from preferences.
    /// </summary>
    void Remove(string key);
}
