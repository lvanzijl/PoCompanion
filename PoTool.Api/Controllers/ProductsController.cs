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

    /// <summary>
    /// Gets all products in the system.
    /// </summary>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAllProducts(CancellationToken cancellationToken)
    {
        var products = await _mediator.Send(new GetAllProductsQuery(), cancellationToken);
        return Ok(products);
    }

    /// <summary>
    /// Gets all orphaned products (products with no owner).
    /// </summary>
    [HttpGet("orphans")]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetOrphanProducts(CancellationToken cancellationToken)
    {
        var products = await _mediator.Send(new GetOrphanProductsQuery(), cancellationToken);
        return Ok(products);
    }

    /// <summary>
    /// Gets products selectable by a specific Product Owner (owned + orphaned).
    /// </summary>
    [HttpGet("selectable")]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetSelectableProducts(
        [FromQuery] int productOwnerId,
        CancellationToken cancellationToken)
    {
        var products = await _mediator.Send(new GetSelectableProductsQuery(productOwnerId), cancellationToken);
        return Ok(products);
    }

    /// <summary>
    /// Changes the Product Owner for a product.
    /// </summary>
    [HttpPatch("{productId}/owner")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> ChangeProductOwner(
        int productId,
        [FromBody] ChangeProductOwnerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeProductOwnerCommand(productId, request.NewProductOwnerId);
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}

/// <summary>
/// Request model for creating a product.
/// </summary>
public record CreateProductRequest(
    int ProductOwnerId,
    string Name,
    int BacklogRootWorkItemId,
    ProductPictureType PictureType = ProductPictureType.Default,
    int DefaultPictureId = 0,
    string? CustomPicturePath = null
);

/// <summary>
/// Request model for updating a product.
/// </summary>
public record UpdateProductRequest(
    string Name,
    int BacklogRootWorkItemId,
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

/// <summary>
/// Request model for changing product owner.
/// </summary>
public record ChangeProductOwnerRequest(
    int? NewProductOwnerId
);
