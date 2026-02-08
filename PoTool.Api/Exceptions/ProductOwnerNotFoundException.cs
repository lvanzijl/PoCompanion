namespace PoTool.Api.Exceptions;

/// <summary>
/// Exception thrown when attempting to access or create resources for a non-existent ProductOwner.
/// </summary>
public class ProductOwnerNotFoundException : Exception
{
    /// <summary>
    /// Gets the ProductOwner ID that was not found.
    /// </summary>
    public int ProductOwnerId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductOwnerNotFoundException"/> class.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID that was not found.</param>
    public ProductOwnerNotFoundException(int productOwnerId)
        : base($"ProductOwner does not exist (ID: {productOwnerId}).")
    {
        ProductOwnerId = productOwnerId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductOwnerNotFoundException"/> class with a custom message.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID that was not found.</param>
    /// <param name="message">The error message.</param>
    public ProductOwnerNotFoundException(int productOwnerId, string message)
        : base(message)
    {
        ProductOwnerId = productOwnerId;
    }
}
