using FluentValidation;
using PoTool.Core.BuildQuality.Queries;

namespace PoTool.Core.BuildQuality.Validators;

/// <summary>
/// Validates BuildQuality pipeline detail requests.
/// </summary>
public sealed class GetBuildQualityPipelineDetailQueryValidator : AbstractValidator<GetBuildQualityPipelineDetailQuery>
{
    public GetBuildQualityPipelineDetailQueryValidator()
    {
        RuleFor(x => x.ProductOwnerId)
            .GreaterThan(0);

        RuleFor(x => x.SprintId)
            .GreaterThan(0);

        RuleFor(x => x)
            .Must(query => query.PipelineDefinitionId.HasValue ^ query.RepositoryId.HasValue)
            .WithMessage("Exactly one of PipelineDefinitionId or RepositoryId must be provided.");

        When(x => x.PipelineDefinitionId.HasValue, () =>
        {
            RuleFor(x => x.PipelineDefinitionId!.Value)
                .GreaterThan(0);
        });

        When(x => x.RepositoryId.HasValue, () =>
        {
            RuleFor(x => x.RepositoryId!.Value)
                .GreaterThan(0);
        });
    }
}
