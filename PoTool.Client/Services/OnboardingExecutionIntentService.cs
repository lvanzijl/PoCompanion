using PoTool.Client.Helpers;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

public sealed class OnboardingExecutionIntentService
{
    public ExecutionIntentViewModel CreateIntent(
        string suggestedAction,
        OnboardingProblemScope scope,
        int? connectionId,
        int? projectId,
        int? rootId,
        int? bindingId,
        OnboardingGraphSection section,
        string anchorId,
        string targetElementId,
        int visibleProjectCount,
        int visibleTeamCount,
        int visiblePipelineCount)
    {
        var intentType = ResolveIntentType(suggestedAction, bindingId);
        var confidenceLevel = ResolveConfidenceLevel(
            intentType,
            visibleProjectCount,
            visibleTeamCount,
            visiblePipelineCount);
        var navigationSection = ResolveNavigationSection(intentType, section);
        var navigationTargetElementId = ResolveTargetElementId(intentType, bindingId, targetElementId);

        var expandedSections = ResolveExpandedSections(intentType, navigationSection);
        var route = WorkspaceQueryContextHelper.BuildRoute(
            WorkspaceRoutes.OnboardingWorkspace,
            new WorkspaceQueryContext(),
            BuildAdditionalParameters(
                intentType,
                connectionId,
                projectId,
                rootId,
                bindingId,
                navigationSection,
                navigationTargetElementId));

        return new ExecutionIntentViewModel(
            intentType,
            scope,
            connectionId,
            projectId,
            rootId,
            bindingId,
            suggestedAction,
            confidenceLevel,
            new ExecutionIntentNavigationTargetViewModel(
                route,
                anchorId,
                navigationSection,
                navigationTargetElementId,
                expandedSections));
    }

    private static string ResolveIntentType(string suggestedAction, int? bindingId)
        => suggestedAction switch
        {
            "Create or select a connection" => "configure-connection",
            "Grant the required connection read permissions" => "configure-connection",
            "Resolve the missing read permissions" => "configure-connection",
            "Enable the required connection capabilities" => "configure-connection",
            "Link project to connection" => "link-project",
            "Assign pipeline to project" when bindingId.HasValue => "replace-binding-source",
            "Assign pipeline to project" => "assign-pipeline",
            "Assign team to project" when bindingId.HasValue => "replace-binding-source",
            "Assign team to project" => "assign-team",
            "Create binding for product root" => "create-binding",
            "Resolve product root validation issue" => "resolve-root-validation",
            _ => "resolve-validation"
        };

    private static OnboardingExecutionConfidenceLevel ResolveConfidenceLevel(
        string intentType,
        int visibleProjectCount,
        int visibleTeamCount,
        int visiblePipelineCount)
        => intentType switch
        {
            "assign-pipeline" when visiblePipelineCount > 1 => OnboardingExecutionConfidenceLevel.Medium,
            "assign-team" when visibleTeamCount > 1 => OnboardingExecutionConfidenceLevel.Medium,
            "link-project" when visibleProjectCount > 1 => OnboardingExecutionConfidenceLevel.Medium,
            "resolve-validation" => OnboardingExecutionConfidenceLevel.Fallback,
            _ => OnboardingExecutionConfidenceLevel.High
        };

    private static IReadOnlyList<OnboardingGraphSection> ResolveExpandedSections(string intentType, OnboardingGraphSection section)
    {
        var sections = new HashSet<OnboardingGraphSection> { section };

        switch (intentType)
        {
            case "configure-connection":
                sections.Add(OnboardingGraphSection.Connections);
                break;
            case "link-project":
                sections.Add(OnboardingGraphSection.Connections);
                sections.Add(OnboardingGraphSection.Projects);
                break;
            case "assign-pipeline":
                sections.Add(OnboardingGraphSection.Projects);
                sections.Add(OnboardingGraphSection.Pipelines);
                break;
            case "assign-team":
                sections.Add(OnboardingGraphSection.Projects);
                sections.Add(OnboardingGraphSection.Teams);
                break;
            case "create-binding":
            case "replace-binding-source":
                sections.Add(OnboardingGraphSection.ProductRoots);
                sections.Add(OnboardingGraphSection.Bindings);
                break;
            case "resolve-root-validation":
                sections.Add(OnboardingGraphSection.ProductRoots);
                break;
        }

        return sections.ToList();
    }

    private static OnboardingGraphSection ResolveNavigationSection(string intentType, OnboardingGraphSection fallbackSection)
        => intentType switch
        {
            "configure-connection" => OnboardingGraphSection.Connections,
            "link-project" => OnboardingGraphSection.Projects,
            "create-binding" => OnboardingGraphSection.Bindings,
            "replace-binding-source" => OnboardingGraphSection.Bindings,
            "resolve-root-validation" => OnboardingGraphSection.ProductRoots,
            _ => fallbackSection
        };

    private static string BuildAdditionalParameters(
        string intentType,
        int? connectionId,
        int? projectId,
        int? rootId,
        int? bindingId,
        OnboardingGraphSection section,
        string targetElementId)
    {
        var parameters = new List<string>
        {
            $"{OnboardingExecutionIntentQueryKeys.IntentType}={intentType}",
            $"{OnboardingExecutionIntentQueryKeys.Section}={section}",
            $"{OnboardingExecutionIntentQueryKeys.TargetElementId}={Uri.EscapeDataString(targetElementId)}"
        };

        if (connectionId.HasValue)
        {
            parameters.Add($"{OnboardingExecutionIntentQueryKeys.ConnectionId}={connectionId.Value}");
        }

        if (projectId.HasValue)
        {
            parameters.Add($"{OnboardingExecutionIntentQueryKeys.ProjectId}={projectId.Value}");
        }

        if (rootId.HasValue)
        {
            parameters.Add($"{OnboardingExecutionIntentQueryKeys.RootId}={rootId.Value}");
        }

        if (bindingId.HasValue)
        {
            parameters.Add($"{OnboardingExecutionIntentQueryKeys.BindingId}={bindingId.Value}");
        }

        return string.Join("&", parameters);
    }

    private static string ResolveTargetElementId(string intentType, int? bindingId, string targetElementId)
        => intentType == "replace-binding-source" && bindingId.HasValue
            ? $"binding-{bindingId.Value}"
            : targetElementId;
}
