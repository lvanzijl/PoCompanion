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
    Task<bool> GetBoolAsync(string key, bool defaultValue);

    /// <summary>
    /// Sets a boolean value in preferences.
    /// </summary>
    Task SetBoolAsync(string key, bool value);

    /// <summary>
    /// Gets an integer value from preferences.
    /// </summary>
    Task<int?> GetIntAsync(string key);

    /// <summary>
    /// Sets an integer value in preferences.
    /// </summary>
    Task SetIntAsync(string key, int value);

    /// <summary>
    /// Removes a key from preferences.
    /// </summary>
    Task RemoveAsync(string key);
}
