using Microsoft.AspNetCore.Mvc;
using PoTool.Core.Filters;

namespace PoTool.Api.Controllers;

internal static class FilterValidationResponseHelper
{
    public static BadRequestObjectResult CreateBadRequest(ControllerBase controller, FilterValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(validation);

        var message = validation.Messages.Count == 0
            ? "Invalid filter state."
            : string.Join(" ", validation.Messages.Select(static issue => issue.Message));

        return controller.BadRequest(message);
    }
}
