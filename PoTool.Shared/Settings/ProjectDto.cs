namespace PoTool.Shared.Settings;

/// <summary>
/// Immutable DTO for a planning project that groups one or more products.
/// </summary>
public sealed record ProjectDto(
    string Id,
    string Alias,
    string Name,
    List<int> ProductIds)
{
    public ProjectDto()
        : this(string.Empty, string.Empty, string.Empty, new List<int>())
    {
    }
}
