namespace PoTool.Shared.Settings;

public sealed class ReleaseNoteDto
{
    public string Date { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string? Link { get; set; }
}
