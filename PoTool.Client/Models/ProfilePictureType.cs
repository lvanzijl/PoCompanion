namespace PoTool.Client.Models;

/// <summary>
/// Enumeration for profile picture types.
/// This is a client-side copy that matches PoTool.Core.Settings.ProfilePictureType.
/// </summary>
public enum ProfilePictureType
{
    /// <summary>
    /// Uses a default maritime-themed picture (0-63).
    /// </summary>
    Default = 0,
    
    /// <summary>
    /// Uses a custom user-provided picture.
    /// </summary>
    Custom = 1
}
