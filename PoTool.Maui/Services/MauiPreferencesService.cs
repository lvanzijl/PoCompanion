using PoTool.Client.Services;

namespace PoTool.Maui.Services;

/// <summary>
/// MAUI-specific implementation of IPreferencesService using MAUI Preferences.
/// </summary>
public class MauiPreferencesService : IPreferencesService
{
    public bool GetBool(string key, bool defaultValue)
    {
        return Preferences.Default.Get(key, defaultValue);
    }

    public void SetBool(string key, bool value)
    {
        Preferences.Default.Set(key, value);
    }

    public void Remove(string key)
    {
        Preferences.Default.Remove(key);
    }
}
