using FluentValidation;
using PoTool.Core.Settings.Commands;

namespace PoTool.Core.Settings.Validators;

/// <summary>
/// Validator for CreateRepositoryCommand.
/// Ensures repository creation requests have valid data.
/// </summary>
public sealed class CreateRepositoryCommandValidator : AbstractValidator<CreateRepositoryCommand>
{
    public CreateRepositoryCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0).WithMessage("Product ID must be greater than 0");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Repository name is required")
            .MaximumLength(200).WithMessage("Repository name must not exceed 200 characters")
            .Matches(@"^[a-zA-Z0-9\-_\.]+$").WithMessage("Repository name can only contain letters, numbers, hyphens, underscores, and periods (no spaces)")
            .Must(NotContainInvalidChars).WithMessage("Repository name contains invalid characters for Azure DevOps");
    }

    private static bool NotContainInvalidChars(string name)
    {
        // Azure DevOps repository names cannot contain: < > : " / \ | ? *
        char[] invalidChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        return !name.Any(c => invalidChars.Contains(c));
    }
}
