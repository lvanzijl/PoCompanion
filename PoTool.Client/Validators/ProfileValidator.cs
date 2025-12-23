using FluentValidation;

namespace PoTool.Client.Validators;

/// <summary>
/// Validator for profile create/update model.
/// </summary>
public class ProfileValidator : AbstractValidator<ProfileModel>
{
    public ProfileValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Profile name is required")
            .MaximumLength(200).WithMessage("Profile name must not exceed 200 characters");

        RuleFor(x => x.AreaPaths)
            .NotEmpty().WithMessage("At least one area path must be selected")
            .Must(paths => paths.Count > 0).WithMessage("At least one area path must be selected");

        RuleForEach(x => x.AreaPaths)
            .NotEmpty().WithMessage("Area path cannot be empty")
            .MaximumLength(500).WithMessage("Area path must not exceed 500 characters");

        RuleFor(x => x.TeamName)
            .NotEmpty().WithMessage("Team name is required")
            .MaximumLength(200).WithMessage("Team name must not exceed 200 characters");

        RuleFor(x => x.GoalIds)
            .NotNull().WithMessage("Goal IDs list cannot be null");

        RuleForEach(x => x.GoalIds)
            .GreaterThan(0).WithMessage("Goal ID must be greater than 0");
    }
}

/// <summary>
/// Model for profile validation.
/// </summary>
public class ProfileModel
{
    public string Name { get; set; } = string.Empty;
    public List<string> AreaPaths { get; set; } = new();
    public string TeamName { get; set; } = string.Empty;
    public List<int> GoalIds { get; set; } = new();
}
