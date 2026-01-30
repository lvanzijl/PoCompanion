using FluentValidation;
using PoTool.Core.Settings.Commands;

namespace PoTool.Core.Settings.Validators;

/// <summary>
/// Validator for CreateTeamCommand.
/// Ensures team creation requests have valid data.
/// </summary>
public sealed class CreateTeamCommandValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Team name is required")
            .MaximumLength(200).WithMessage("Team name must not exceed 200 characters")
            .Matches(@"^[a-zA-Z0-9\s\-_\.]+$").WithMessage("Team name can only contain letters, numbers, spaces, hyphens, underscores, and periods");

        RuleFor(x => x.TeamAreaPath)
            .NotEmpty().WithMessage("Team area path is required")
            .MaximumLength(500).WithMessage("Team area path must not exceed 500 characters")
            .Must(BeValidAreaPath).WithMessage("Team area path must be a valid TFS area path format (e.g., 'Project\\Team')");

        RuleFor(x => x.DefaultPictureId)
            .InclusiveBetween(0, 63).WithMessage("Default picture ID must be between 0 and 63")
            .When(x => x.PictureType == Shared.Settings.TeamPictureType.Default);

        RuleFor(x => x.CustomPicturePath)
            .NotEmpty().WithMessage("Custom picture path is required when picture type is Custom")
            .MaximumLength(500).WithMessage("Custom picture path must not exceed 500 characters")
            .When(x => x.PictureType == Shared.Settings.TeamPictureType.Custom);

        RuleFor(x => x.ProjectName)
            .MaximumLength(200).WithMessage("Project name must not exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.ProjectName));

        RuleFor(x => x.TfsTeamId)
            .MaximumLength(100).WithMessage("TFS team ID must not exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.TfsTeamId));

        RuleFor(x => x.TfsTeamName)
            .MaximumLength(200).WithMessage("TFS team name must not exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.TfsTeamName));
    }

    private static bool BeValidAreaPath(string areaPath)
    {
        if (string.IsNullOrWhiteSpace(areaPath))
            return false;

        // Area path should contain at least one backslash and no invalid characters
        return areaPath.Contains('\\') && 
               !areaPath.Contains('/') && 
               !areaPath.Contains('*') &&
               !areaPath.StartsWith('\\') &&
               !areaPath.EndsWith('\\');
    }
}
