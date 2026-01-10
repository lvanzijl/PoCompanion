using Mediator;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to delete a product.
/// </summary>
public sealed record DeleteProductCommand(int Id) : ICommand<bool>;
