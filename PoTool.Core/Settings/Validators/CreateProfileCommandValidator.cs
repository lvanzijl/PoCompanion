using FluentValidation;
using PoTool.Core.Settings.Commands;

namespace PoTool.Core.Settings.Validators;

/// <summary>
/// Validator for CreateProfileCommand.
/// Ensures profile creation requests have valid data.
/// </summary>
public sealed class CreateProfileCommandValidator : AbstractValidator<CreateProfileCommand>
{
    public CreateProfileCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Profile name is required")
            .MaximumLength(200).WithMessage("Profile name must not exceed 200 characters")
            .Matches(@"^[a-zA-Z0-9\s\-_\.]+$").WithMessage("Profile name can only contain letters, numbers, spaces, hyphens, underscores, and periods");

        RuleFor(x => x.GoalIds)
            .NotNull().WithMessage("Goal IDs list cannot be null");

        RuleForEach(x => x.GoalIds)
            .GreaterThan(0).WithMessage("Each goal ID must be greater than 0");

        RuleFor(x => x.DefaultPictureId)
            .InclusiveBetween(0, 63).WithMessage("Default picture ID must be between 0 and 63")
            .When(x => x.PictureType == Shared.Settings.ProfilePictureType.Default);

        RuleFor(x => x.CustomPicturePath)
            .NotEmpty().WithMessage("Custom picture path is required when picture type is Custom")
            .MaximumLength(500).WithMessage("Custom picture path must not exceed 500 characters")
            .When(x => x.PictureType == Shared.Settings.ProfilePictureType.Custom);
    }
}
