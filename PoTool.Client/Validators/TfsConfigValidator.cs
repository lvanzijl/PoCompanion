using FluentValidation;

namespace PoTool.Client.Validators;

/// <summary>
/// Validator for TFS configuration model.
/// </summary>
public class TfsConfigValidator : AbstractValidator<TfsConfigModel>
{
    public TfsConfigValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Organization URL is required")
            .Must(BeValidUrl).WithMessage("Must be a valid URL (e.g., https://dev.azure.com/yourorg)");

        RuleFor(x => x.Project)
            .NotEmpty().WithMessage("Project name is required")
            .MinimumLength(1).WithMessage("Project name must be at least 1 character");

        RuleFor(x => x.Pat)
            .NotEmpty().WithMessage("Personal Access Token is required")
            .MinimumLength(20).WithMessage("PAT appears to be invalid (too short)");
    }

    private bool BeValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

/// <summary>
/// Model for TFS configuration validation.
/// </summary>
public class TfsConfigModel
{
    public string Url { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string Pat { get; set; } = string.Empty;
}
