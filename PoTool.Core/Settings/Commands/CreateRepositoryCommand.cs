using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to create a new repository configuration for a product.
/// </summary>
/// <param name="ProductId">ID of the product to which this repository belongs</param>
/// <param name="Name">Repository name as used in Azure DevOps/TFS</param>
public sealed record CreateRepositoryCommand(
    int ProductId,
    string Name
) : ICommand<RepositoryDto>;
