using FluentValidation;
using PoTool.Core.Settings.Commands;

namespace PoTool.Core.Settings.Validators;

/// <summary>
/// Validator for CreateProductCommand.
/// Ensures product creation requests have valid data.
/// </summary>
public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(200).WithMessage("Product name must not exceed 200 characters")
            .Matches(@"^[a-zA-Z0-9\s\-_\.]+$").WithMessage("Product name can only contain letters, numbers, spaces, hyphens, underscores, and periods");

        RuleFor(x => x.BacklogRootWorkItemId)
            .GreaterThan(0).WithMessage("Backlog root work item ID must be greater than 0");

        RuleFor(x => x.ProductOwnerId)
            .GreaterThan(0).WithMessage("Product owner ID must be greater than 0")
            .When(x => x.ProductOwnerId.HasValue);

        RuleFor(x => x.DefaultPictureId)
            .InclusiveBetween(0, 63).WithMessage("Default picture ID must be between 0 and 63")
            .When(x => x.PictureType == Shared.Settings.ProductPictureType.Default);

        RuleFor(x => x.CustomPicturePath)
            .NotEmpty().WithMessage("Custom picture path is required when picture type is Custom")
            .MaximumLength(500).WithMessage("Custom picture path must not exceed 500 characters")
            .When(x => x.PictureType == Shared.Settings.ProductPictureType.Custom);
    }
}
