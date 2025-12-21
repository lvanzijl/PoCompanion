namespace PoTool.Client.Services;

/// <summary>
/// Abstraction for secure storage that works across platforms.
/// For MAUI Hybrid, this wraps MAUI SecureStorage.
/// For other platforms, implementations should use platform-native secure storage.
/// </summary>
public interface ISecureStorageService
{
    /// <summary>
    /// Stores a value securely.
    /// </summary>
    /// <param name="key">The key to store the value under.</param>
    /// <param name="value">The value to store.</param>
    Task SetAsync(string key, string value);

    /// <summary>
    /// Retrieves a securely stored value.
    /// </summary>
    /// <param name="key">The key to retrieve the value for.</param>
    /// <returns>The stored value, or null if not found.</returns>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// Removes a securely stored value.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>True if the value was removed, false if it didn't exist.</returns>
    bool Remove(string key);

    /// <summary>
    /// Removes all securely stored values.
    /// </summary>
    void RemoveAll();
}
