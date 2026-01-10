using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Commands;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for product management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets all products for a Product Owner.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProductsByOwner(
        [FromQuery] int productOwnerId,
        CancellationToken cancellationToken)
    {
        var products = await _mediator.Send(new GetProductsByOwnerQuery(productOwnerId), cancellationToken);
        return Ok(products);
    }

    /// <summary>
    /// Gets a product by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetProductById(int id, CancellationToken cancellationToken)
    {
        var product = await _mediator.Send(new GetProductByIdQuery(id), cancellationToken);

        if (product == null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    /// <summary>
    /// Creates a new product.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateProductCommand(
            request.ProductOwnerId,
            request.Name,
            request.ProductAreaPath,
            request.BacklogRootWorkItemId,
            request.PictureType,
            request.DefaultPictureId,
            request.CustomPicturePath);

        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetProductById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing product.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> UpdateProduct(
        int id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UpdateProductCommand(
                id,
                request.Name,
                request.ProductAreaPath,
                request.BacklogRootWorkItemId,
                request.PictureType,
                request.DefaultPictureId,
                request.CustomPicturePath);

            var result = await _mediator.Send(command, cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Deletes a product.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteProductCommand(id), cancellationToken);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Reorders products for a Product Owner.
    /// </summary>
    [HttpPost("reorder")]
    [ProducesResponseType(typeof(List<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProductDto>>> ReorderProducts(
        [FromBody] ReorderProductsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ReorderProductsCommand(request.ProductOwnerId, request.ProductIds);
        var result = await _mediator.Send(command, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Links a team to a product.
    /// </summary>
    [HttpPost("{productId}/teams/{teamId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LinkTeamToProduct(
        int productId,
        int teamId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new LinkTeamToProductCommand(productId, teamId), cancellationToken);

        if (!result)
        {
            return NotFound();
        }

        return Ok();
    }

    /// <summary>
    /// Unlinks a team from a product.
    /// </summary>
    [HttpDelete("{productId}/teams/{teamId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkTeamFromProduct(
        int productId,
        int teamId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UnlinkTeamFromProductCommand(productId, teamId), cancellationToken);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }
}

/// <summary>
/// Request model for creating a product.
/// </summary>
public record CreateProductRequest(
    int ProductOwnerId,
    string Name,
    string ProductAreaPath,
    int? BacklogRootWorkItemId = null,
    ProductPictureType PictureType = ProductPictureType.Default,
    int DefaultPictureId = 0,
    string? CustomPicturePath = null
);

/// <summary>
/// Request model for updating a product.
/// </summary>
public record UpdateProductRequest(
    string Name,
    string ProductAreaPath,
    int? BacklogRootWorkItemId = null,
    ProductPictureType? PictureType = null,
    int? DefaultPictureId = null,
    string? CustomPicturePath = null
);

/// <summary>
/// Request model for reordering products.
/// </summary>
public record ReorderProductsRequest(
    int ProductOwnerId,
    List<int> ProductIds
);
