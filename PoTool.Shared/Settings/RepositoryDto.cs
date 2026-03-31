namespace PoTool.Shared.Settings;

/// <summary>
/// Immutable DTO for a Repository.
/// A Repository represents a Git repository in Azure DevOps/TFS associated with a Product.
/// </summary>
public sealed record RepositoryDto(
    int Id,
    int ProductId,
    string Name,
    DateTimeOffset CreatedAt,
    string? RepositoryId = null
)
{
    public RepositoryDto()
        : this(0, 0, string.Empty, default)
    {
    }
}
