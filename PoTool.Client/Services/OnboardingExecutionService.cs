using System.Net;
using System.Text.Json;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Shared.Onboarding;

namespace PoTool.Client.Services;

public sealed class OnboardingExecutionService
{
    private static readonly JsonSerializerOptions ErrorSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions SuccessSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IOnboardingCrudClient _crudClient;
    private readonly IOnboardingWorkspaceService _workspaceService;

    public OnboardingExecutionService(
        IOnboardingCrudClient crudClient,
        IOnboardingWorkspaceService workspaceService)
    {
        _crudClient = crudClient;
        _workspaceService = workspaceService;
    }

    public Task<OnboardingExecutionResult> CreateConnectionAsync(
        ExecutionIntentViewModel intent,
        CreateTfsConnectionRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateConnectionCreate(intent, request);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.CreateConnectionAsync(request, token),
            envelope => currentFilter with { ConnectionId = envelope.Data?.Id },
            "Connection updated",
            envelope => $"Connection context is ready for {envelope.Data?.OrganizationUrl ?? "the selected organization"}.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> UpdateConnectionAsync(
        ExecutionIntentViewModel intent,
        int connectionId,
        UpdateTfsConnectionRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateConnectionUpdate(intent, connectionId, request);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.UpdateConnectionAsync(connectionId, request, token),
            envelope => currentFilter with { ConnectionId = envelope.Data?.Id ?? connectionId, ProjectId = null, ProductRootId = null },
            "Connection updated",
            envelope => $"Connection settings were saved for {envelope.Data?.OrganizationUrl ?? "the selected organization"}.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> DeleteConnectionAsync(
        ExecutionIntentViewModel intent,
        int connectionId,
        OnboardingSoftDeleteRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateDelete(intent, OnboardingGraphSection.Connections, connectionId, request);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.DeleteConnectionAsync(connectionId, request, token),
            _ => currentFilter with { ConnectionId = null, ProjectId = null, ProductRootId = null },
            "Connection removed",
            _ => "The selected connection was soft-deleted and removed from the onboarding graph.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> CreateProjectAsync(
        ExecutionIntentViewModel intent,
        CreateProjectSourceRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateProjectCreate(intent, request);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.CreateProjectAsync(request, token),
            envelope => currentFilter with
            {
                ConnectionId = request.TfsConnectionId,
                ProjectId = envelope.Data?.Id,
                ProductRootId = null
            },
            "Project linked",
            envelope => $"Project {envelope.Data?.Snapshot.Name ?? request.ProjectExternalId} is now available in onboarding.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> UpdateProjectAsync(
        ExecutionIntentViewModel intent,
        int projectId,
        UpdateProjectSourceRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateProjectUpdate(intent, projectId);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.UpdateProjectAsync(projectId, request, token),
            envelope => currentFilter with { ProjectId = envelope.Data?.Id ?? projectId, ProductRootId = null },
            "Project updated",
            envelope => $"Project {envelope.Data?.Snapshot.Name ?? "settings"} was updated.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> DeleteProjectAsync(
        ExecutionIntentViewModel intent,
        int projectId,
        OnboardingSoftDeleteRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateDelete(intent, OnboardingGraphSection.Projects, projectId, request);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.DeleteProjectAsync(projectId, request, token),
            _ => currentFilter with { ProjectId = null, ProductRootId = null },
            "Project removed",
            _ => "The selected project was soft-deleted and removed from the onboarding graph.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> CreateTeamAsync(
        ExecutionIntentViewModel intent,
        CreateTeamSourceRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateTeamCreate(intent, request);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.CreateTeamAsync(request, token),
            envelope => currentFilter with { ProjectId = request.ProjectSourceId },
            "Team added",
            envelope => $"Team {envelope.Data?.Snapshot.Name ?? request.TeamExternalId} is now available for onboarding.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> UpdateTeamAsync(
        ExecutionIntentViewModel intent,
        int teamId,
        UpdateTeamSourceRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateTeamUpdate(intent, teamId);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.UpdateTeamAsync(teamId, request, token),
            envelope => currentFilter with { ProjectId = envelope.Data?.ProjectSourceId ?? currentFilter.ProjectId },
            "Team updated",
            envelope => $"Team {envelope.Data?.Snapshot.Name ?? "settings"} was updated.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> DeleteTeamAsync(
        ExecutionIntentViewModel intent,
        int teamId,
        OnboardingSoftDeleteRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateDelete(intent, OnboardingGraphSection.Teams, teamId, request);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.DeleteTeamAsync(teamId, request, token),
            _ => currentFilter,
            "Team removed",
            _ => "The selected team was soft-deleted and removed from the onboarding graph.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> CreatePipelineAsync(
        ExecutionIntentViewModel intent,
        CreatePipelineSourceRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidatePipelineCreate(intent, request);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.CreatePipelineAsync(request, token),
            envelope => currentFilter with { ProjectId = request.ProjectSourceId },
            "Pipeline added",
            envelope => $"Pipeline {envelope.Data?.Snapshot.Name ?? request.PipelineExternalId} is now available for onboarding.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> UpdatePipelineAsync(
        ExecutionIntentViewModel intent,
        int pipelineId,
        UpdatePipelineSourceRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidatePipelineUpdate(intent, pipelineId);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.UpdatePipelineAsync(pipelineId, request, token),
            envelope => currentFilter with { ProjectId = envelope.Data?.ProjectSourceId ?? currentFilter.ProjectId },
            "Pipeline updated",
            envelope => $"Pipeline {envelope.Data?.Snapshot.Name ?? "settings"} was updated.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> DeletePipelineAsync(
        ExecutionIntentViewModel intent,
        int pipelineId,
        OnboardingSoftDeleteRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateDelete(intent, OnboardingGraphSection.Pipelines, pipelineId, request);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.DeletePipelineAsync(pipelineId, request, token),
            _ => currentFilter,
            "Pipeline removed",
            _ => "The selected pipeline was soft-deleted and removed from the onboarding graph.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> CreateRootAsync(
        ExecutionIntentViewModel intent,
        CreateProductRootRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateRootCreate(intent, request);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.CreateRootAsync(request, token),
            envelope => currentFilter with { ProjectId = request.ProjectSourceId, ProductRootId = envelope.Data?.Id },
            "Product root added",
            envelope => $"Product root {envelope.Data?.Snapshot.Title ?? request.WorkItemExternalId} is now available for binding.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> UpdateRootAsync(
        ExecutionIntentViewModel intent,
        int rootId,
        UpdateProductRootRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateRootUpdate(intent, rootId);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.UpdateRootAsync(rootId, request, token),
            envelope => currentFilter with { ProjectId = envelope.Data?.ProjectSourceId ?? currentFilter.ProjectId, ProductRootId = envelope.Data?.Id ?? rootId },
            "Product root updated",
            envelope => $"Product root {envelope.Data?.Snapshot.Title ?? "settings"} was updated.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> DeleteRootAsync(
        ExecutionIntentViewModel intent,
        int rootId,
        OnboardingSoftDeleteRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateDelete(intent, OnboardingGraphSection.ProductRoots, rootId, request);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.DeleteRootAsync(rootId, request, token),
            _ => currentFilter with { ProductRootId = null },
            "Product root removed",
            _ => "The selected product root was soft-deleted and removed from the onboarding graph.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> CreateBindingAsync(
        ExecutionIntentViewModel intent,
        CreateProductSourceBindingRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateBindingCreate(intent, request);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.CreateBindingAsync(request, token),
            envelope => currentFilter with { ProductRootId = envelope.Data?.ProductRootId ?? request.ProductRootId },
            "Binding created",
            envelope => $"Binding {envelope.Data?.SourceType.ToString() ?? request.SourceType.ToString()} → {envelope.Data?.SourceExternalId ?? "source"} is now active.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> UpdateBindingAsync(
        ExecutionIntentViewModel intent,
        int bindingId,
        UpdateProductSourceBindingRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateBindingUpdate(intent, bindingId);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.UpdateBindingAsync(bindingId, request, token),
            envelope => currentFilter with { ProductRootId = envelope.Data?.ProductRootId ?? currentFilter.ProductRootId },
            "Binding updated",
            envelope => $"Binding {envelope.Data?.SourceExternalId ?? "settings"} was updated.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> ReplaceBindingSourceAsync(
        ExecutionIntentViewModel intent,
        bool enabled,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateBindingSourceReplacement(intent);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        var request = intent.SelectedReplacementSourceType switch
        {
            OnboardingProductSourceTypeDto.Team => new UpdateProductSourceBindingRequest(
                enabled,
                null,
                null,
                intent.SelectedReplacementSourceId,
                null,
                null,
                null),
            OnboardingProductSourceTypeDto.Pipeline => new UpdateProductSourceBindingRequest(
                enabled,
                null,
                null,
                null,
                intent.SelectedReplacementSourceId,
                null,
                null),
            _ => throw new InvalidOperationException("Binding replacement requires a team or pipeline source selection.")
        };

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.UpdateBindingAsync(intent.BindingId!.Value, request, token),
            envelope => currentFilter with { ProductRootId = envelope.Data?.ProductRootId ?? currentFilter.ProductRootId },
            "Binding source replaced",
            envelope => $"Binding now points to {envelope.Data?.SourceType.ToString() ?? intent.SelectedReplacementSourceType?.ToString() ?? "replacement"} → {envelope.Data?.SourceExternalId ?? "source"}.",
            cancellationToken);
    }

    public Task<OnboardingExecutionResult> DeleteBindingAsync(
        ExecutionIntentViewModel intent,
        int bindingId,
        OnboardingSoftDeleteRequest request,
        OnboardingWorkspaceFilter currentFilter,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateDelete(intent, OnboardingGraphSection.Bindings, bindingId, request);
        if (validationError is not null)
        {
            return Task.FromResult(validationError);
        }

        return ExecuteAsync(
            intent,
            currentFilter,
            token => _crudClient.DeleteBindingAsync(bindingId, request, token),
            _ => currentFilter,
            "Binding removed",
            _ => "The selected binding was soft-deleted and removed from the onboarding graph.",
            cancellationToken);
    }

    private async Task<OnboardingExecutionResult> ExecuteAsync<TResult>(
        ExecutionIntentViewModel intent,
        OnboardingWorkspaceFilter currentFilter,
        Func<CancellationToken, Task<TResult>> operation,
        Func<TResult, OnboardingWorkspaceFilter> resolveFilter,
        string successTitle,
        Func<TResult, string> successMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var operationResult = await operation(cancellationToken);
            return await BuildSuccessResultAsync(intent, operationResult, resolveFilter, successTitle, successMessage, cancellationToken);
        }
        catch (ApiException exception)
        {
            if (exception.StatusCode == (int)HttpStatusCode.Created &&
                TryParseSuccessPayload(exception.Response, out TResult operationResult))
            {
                return await BuildSuccessResultAsync(intent, operationResult, resolveFilter, successTitle, successMessage, cancellationToken);
            }

            var error = ParseError(exception);
            return await BuildFailureResultAsync(intent, currentFilter, error, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return new OnboardingExecutionResult(
                false,
                intent,
                currentFilter,
                null,
                new OnboardingExecutionFeedbackViewModel(
                    OnboardingExecutionFeedbackKind.Error,
                    "Execution failed",
                    "The onboarding mutation could not be completed.",
                    exception.Message,
                    true),
                null);
        }
    }

    private async Task<OnboardingExecutionResult> BuildSuccessResultAsync<TResult>(
        ExecutionIntentViewModel intent,
        TResult operationResult,
        Func<TResult, OnboardingWorkspaceFilter> resolveFilter,
        string successTitle,
        Func<TResult, string> successMessage,
        CancellationToken cancellationToken)
    {
        var nextFilter = resolveFilter(operationResult);
        try
        {
            var refreshedData = await _workspaceService.GetWorkspaceDataAsync(nextFilter, cancellationToken);
            return new OnboardingExecutionResult(
                true,
                intent,
                nextFilter,
                refreshedData,
                new OnboardingExecutionFeedbackViewModel(
                    OnboardingExecutionFeedbackKind.Success,
                    successTitle,
                    successMessage(operationResult),
                    null,
                    false),
                null);
        }
        catch (HttpRequestException refreshException)
        {
            return new OnboardingExecutionResult(
                true,
                intent,
                nextFilter,
                null,
                new OnboardingExecutionFeedbackViewModel(
                    OnboardingExecutionFeedbackKind.Warning,
                    successTitle,
                    $"{successMessage(operationResult)} The mutation was applied, but the workspace refresh failed.",
                    refreshException.Message,
                    true),
                null);
        }
    }

    private async Task<OnboardingExecutionResult> BuildFailureResultAsync(
        ExecutionIntentViewModel intent,
        OnboardingWorkspaceFilter currentFilter,
        OnboardingErrorDto error,
        CancellationToken cancellationToken)
    {
        OnboardingWorkspaceData? refreshedData = null;

        if (error.Code == OnboardingErrorCode.NotFound)
        {
            try
            {
                refreshedData = await _workspaceService.GetWorkspaceDataAsync(currentFilter, cancellationToken);
            }
            catch (HttpRequestException)
            {
                refreshedData = null;
            }
        }

        return new OnboardingExecutionResult(
            false,
            intent,
            currentFilter,
            refreshedData,
            new OnboardingExecutionFeedbackViewModel(
                OnboardingExecutionFeedbackKind.Error,
                "Execution failed",
                error.Message,
                error.Details,
                error.Retryable),
            error.Code);
    }

    private static OnboardingExecutionResult? ValidateConnectionCreate(ExecutionIntentViewModel intent, CreateTfsConnectionRequest request)
    {
        var intentError = ValidateIntent(intent, OnboardingGraphSection.Connections, "configure-connection", "resolve-validation");
        if (intentError is not null)
        {
            return intentError;
        }

        if (string.IsNullOrWhiteSpace(request.OrganizationUrl))
        {
            return BuildLocalValidationFailure(intent, "Organization URL is required.", nameof(request.OrganizationUrl));
        }

        return ValidateConnectionSettings(intent, request.AuthenticationMode, request.TimeoutSeconds, request.ApiVersion);
    }

    private static OnboardingExecutionResult? ValidateConnectionUpdate(ExecutionIntentViewModel intent, int connectionId, UpdateTfsConnectionRequest request)
    {
        var intentError = ValidateIntent(intent, OnboardingGraphSection.Connections, "configure-connection", "resolve-validation");
        if (intentError is not null)
        {
            return intentError;
        }

        if (connectionId <= 0)
        {
            return BuildLocalValidationFailure(intent, "Connection context is missing.", "connectionId");
        }

        return ValidateConnectionSettings(intent, request.AuthenticationMode, request.TimeoutSeconds, request.ApiVersion);
    }

    private static OnboardingExecutionResult? ValidateConnectionSettings(
        ExecutionIntentViewModel intent,
        string? authenticationMode,
        int? timeoutSeconds,
        string? apiVersion)
    {
        if (string.IsNullOrWhiteSpace(authenticationMode))
        {
            return BuildLocalValidationFailure(intent, "Authentication mode is required.", nameof(authenticationMode));
        }

        if (!timeoutSeconds.HasValue || timeoutSeconds.Value <= 0)
        {
            return BuildLocalValidationFailure(intent, "TimeoutSeconds must be greater than zero.", nameof(timeoutSeconds));
        }

        if (string.IsNullOrWhiteSpace(apiVersion))
        {
            return BuildLocalValidationFailure(intent, "API version is required.", nameof(apiVersion));
        }

        return null;
    }

    private static OnboardingExecutionResult? ValidateProjectCreate(ExecutionIntentViewModel intent, CreateProjectSourceRequest request)
    {
        var intentError = ValidateIntent(intent, OnboardingGraphSection.Projects, "link-project", "resolve-validation");
        if (intentError is not null)
        {
            return intentError;
        }

        if (request.TfsConnectionId <= 0)
        {
            return BuildLocalValidationFailure(intent, "A connection must be selected before linking a project.", nameof(request.TfsConnectionId));
        }

        if (string.IsNullOrWhiteSpace(request.ProjectExternalId))
        {
            return BuildLocalValidationFailure(intent, "Project external ID is required.", nameof(request.ProjectExternalId));
        }

        return null;
    }

    private static OnboardingExecutionResult? ValidateProjectUpdate(ExecutionIntentViewModel intent, int projectId)
    {
        var intentError = ValidateIntent(intent, OnboardingGraphSection.Projects, "link-project", "resolve-validation");
        if (intentError is not null)
        {
            return intentError;
        }

        return projectId <= 0
            ? BuildLocalValidationFailure(intent, "Project context is missing.", "projectId")
            : null;
    }

    private static OnboardingExecutionResult? ValidateTeamCreate(ExecutionIntentViewModel intent, CreateTeamSourceRequest request)
    {
        var intentError = ValidateIntent(intent, OnboardingGraphSection.Teams, "assign-team", "resolve-validation");
        if (intentError is not null)
        {
            return intentError;
        }

        if (request.ProjectSourceId <= 0)
        {
            return BuildLocalValidationFailure(intent, "A project must be selected before adding a team.", nameof(request.ProjectSourceId));
        }

        if (string.IsNullOrWhiteSpace(request.TeamExternalId))
        {
            return BuildLocalValidationFailure(intent, "Team external ID is required.", nameof(request.TeamExternalId));
        }

        return null;
    }

    private static OnboardingExecutionResult? ValidateTeamUpdate(ExecutionIntentViewModel intent, int teamId)
    {
        var intentError = ValidateIntent(intent, OnboardingGraphSection.Teams, "assign-team", "resolve-validation");
        if (intentError is not null)
        {
            return intentError;
        }

        return teamId <= 0
            ? BuildLocalValidationFailure(intent, "Team context is missing.", "teamId")
            : null;
    }

    private static OnboardingExecutionResult? ValidatePipelineCreate(ExecutionIntentViewModel intent, CreatePipelineSourceRequest request)
    {
        var intentError = ValidateIntent(intent, OnboardingGraphSection.Pipelines, "assign-pipeline", "resolve-validation");
        if (intentError is not null)
        {
            return intentError;
        }

        if (request.ProjectSourceId <= 0)
        {
            return BuildLocalValidationFailure(intent, "A project must be selected before adding a pipeline.", nameof(request.ProjectSourceId));
        }

        if (string.IsNullOrWhiteSpace(request.PipelineExternalId))
        {
            return BuildLocalValidationFailure(intent, "Pipeline external ID is required.", nameof(request.PipelineExternalId));
        }

        return null;
    }

    private static OnboardingExecutionResult? ValidatePipelineUpdate(ExecutionIntentViewModel intent, int pipelineId)
    {
        var intentError = ValidateIntent(intent, OnboardingGraphSection.Pipelines, "assign-pipeline", "resolve-validation");
        if (intentError is not null)
        {
            return intentError;
        }

        return pipelineId <= 0
            ? BuildLocalValidationFailure(intent, "Pipeline context is missing.", "pipelineId")
            : null;
    }

    private static OnboardingExecutionResult? ValidateRootCreate(ExecutionIntentViewModel intent, CreateProductRootRequest request)
    {
        var intentError = ValidateIntent(intent, OnboardingGraphSection.ProductRoots, "resolve-root-validation", "resolve-validation");
        if (intentError is not null)
        {
            return intentError;
        }

        if (request.ProjectSourceId <= 0)
        {
            return BuildLocalValidationFailure(intent, "A project must be selected before adding a product root.", nameof(request.ProjectSourceId));
        }

        if (string.IsNullOrWhiteSpace(request.WorkItemExternalId))
        {
            return BuildLocalValidationFailure(intent, "Work item external ID is required.", nameof(request.WorkItemExternalId));
        }

        return null;
    }

    private static OnboardingExecutionResult? ValidateRootUpdate(ExecutionIntentViewModel intent, int rootId)
    {
        var intentError = ValidateIntent(intent, OnboardingGraphSection.ProductRoots, "resolve-root-validation", "resolve-validation");
        if (intentError is not null)
        {
            return intentError;
        }

        return rootId <= 0
            ? BuildLocalValidationFailure(intent, "Product-root context is missing.", "rootId")
            : null;
    }

    private static OnboardingExecutionResult? ValidateBindingCreate(ExecutionIntentViewModel intent, CreateProductSourceBindingRequest request)
    {
        var intentError = ValidateIntent(intent, OnboardingGraphSection.Bindings, "create-binding", "resolve-validation");
        if (intentError is not null)
        {
            return intentError;
        }

        if (request.ProductRootId <= 0)
        {
            return BuildLocalValidationFailure(intent, "A product root must be selected before creating a binding.", nameof(request.ProductRootId));
        }

        if (intent.ConfidenceLevel == OnboardingExecutionConfidenceLevel.Fallback
            && (!intent.ProjectId.HasValue || !intent.RootId.HasValue))
        {
            return BuildLocalValidationFailure(intent, "Fallback execution intent does not have enough context to perform a binding mutation.", "intent");
        }

        return request.SourceType switch
        {
            OnboardingProductSourceTypeDto.Project when request.ProjectSourceId.GetValueOrDefault() <= 0
                => BuildLocalValidationFailure(intent, "A project source must be selected for a project binding.", nameof(request.ProjectSourceId)),
            OnboardingProductSourceTypeDto.Team when request.TeamSourceId.GetValueOrDefault() <= 0
                => BuildLocalValidationFailure(intent, "A team source must be selected for a team binding.", nameof(request.TeamSourceId)),
            OnboardingProductSourceTypeDto.Pipeline when request.PipelineSourceId.GetValueOrDefault() <= 0
                => BuildLocalValidationFailure(intent, "A pipeline source must be selected for a pipeline binding.", nameof(request.PipelineSourceId)),
            _ => null
        };
    }

    private static OnboardingExecutionResult? ValidateBindingUpdate(ExecutionIntentViewModel intent, int bindingId)
    {
        var intentError = ValidateIntent(intent, OnboardingGraphSection.Bindings, "create-binding", "replace-binding-source", "resolve-validation");
        if (intentError is not null)
        {
            return intentError;
        }

        return bindingId <= 0
            ? BuildLocalValidationFailure(intent, "Binding context is missing.", "bindingId")
            : null;
    }

    private static OnboardingExecutionResult? ValidateBindingSourceReplacement(ExecutionIntentViewModel intent)
    {
        if (!string.Equals(intent.IntentType, "replace-binding-source", StringComparison.Ordinal))
        {
            return BuildLocalValidationFailure(intent, "Binding source replacement requires an explicit replacement execution intent.", nameof(intent.IntentType));
        }

        var intentError = ValidateBindingUpdate(intent, intent.BindingId.GetValueOrDefault());
        if (intentError is not null)
        {
            return intentError;
        }

        if (!intent.SelectedReplacementSourceType.HasValue)
        {
            return BuildLocalValidationFailure(intent, "A replacement source type is required before correcting the binding.", nameof(intent.SelectedReplacementSourceType));
        }

        if (!intent.SelectedReplacementSourceId.HasValue || intent.SelectedReplacementSourceId.Value <= 0)
        {
            return BuildLocalValidationFailure(intent, "Select a replacement source before correcting the binding.", nameof(intent.SelectedReplacementSourceId));
        }

        return intent.SelectedReplacementSourceType.Value switch
        {
            OnboardingProductSourceTypeDto.Team or OnboardingProductSourceTypeDto.Pipeline => null,
            _ => BuildLocalValidationFailure(intent, "Only team and pipeline bindings support replacement correction.", nameof(intent.SelectedReplacementSourceType))
        };
    }

    private static OnboardingExecutionResult? ValidateDelete(
        ExecutionIntentViewModel intent,
        OnboardingGraphSection section,
        int entityId,
        OnboardingSoftDeleteRequest request)
    {
        string[] allowedIntentTypes = section switch
        {
            OnboardingGraphSection.Connections => ["configure-connection", "resolve-validation"],
            OnboardingGraphSection.Projects => ["link-project", "resolve-validation"],
            OnboardingGraphSection.Teams => ["assign-team", "resolve-validation"],
            OnboardingGraphSection.Pipelines => ["assign-pipeline", "resolve-validation"],
            OnboardingGraphSection.ProductRoots => ["resolve-root-validation", "resolve-validation"],
            OnboardingGraphSection.Bindings => ["create-binding", "resolve-validation"],
            _ => ["resolve-validation"]
        };

        var intentError = ValidateIntent(intent, section, allowedIntentTypes);
        if (intentError is not null)
        {
            return intentError;
        }

        if (entityId <= 0)
        {
            return BuildLocalValidationFailure(intent, "Delete context is missing.", "entityId");
        }

        return string.IsNullOrWhiteSpace(request.Reason)
            ? BuildLocalValidationFailure(intent, "Deletion reason is required.", nameof(request.Reason))
            : null;
    }

    private static OnboardingExecutionResult? ValidateIntent(
        ExecutionIntentViewModel intent,
        OnboardingGraphSection expectedSection,
        params string[] allowedIntentTypes)
    {
        if (intent.NavigationTarget.Section != expectedSection)
        {
            return BuildLocalValidationFailure(intent, "Mutation execution is only available from the matching execution intent context.", nameof(intent.NavigationTarget.Section));
        }

        if (allowedIntentTypes.Length > 0
            && !allowedIntentTypes.Contains(intent.IntentType, StringComparer.Ordinal))
        {
            return BuildLocalValidationFailure(intent, "This execution intent does not allow the requested mutation surface.", nameof(intent.IntentType));
        }

        return null;
    }

    private static OnboardingExecutionResult BuildLocalValidationFailure(
        ExecutionIntentViewModel intent,
        string message,
        string? details)
        => new(
            false,
            intent,
            new OnboardingWorkspaceFilter(intent.ConnectionId, intent.ProjectId, intent.RootId, null),
            null,
            new OnboardingExecutionFeedbackViewModel(
                OnboardingExecutionFeedbackKind.Error,
                "Validation failed",
                message,
                details,
                false),
            OnboardingErrorCode.ValidationFailed);

    private static OnboardingErrorDto ParseError(ApiException exception)
    {
        if (!string.IsNullOrWhiteSpace(exception.Response))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<OnboardingErrorDto>(exception.Response, ErrorSerializerOptions);
                if (parsed is not null)
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
            }
        }

        var statusCode = (HttpStatusCode)exception.StatusCode;
        return statusCode switch
        {
            HttpStatusCode.BadRequest => new OnboardingErrorDto(OnboardingErrorCode.ValidationFailed, "The mutation request was rejected.", exception.Message, false),
            HttpStatusCode.NotFound => new OnboardingErrorDto(OnboardingErrorCode.NotFound, "The selected onboarding entity no longer exists.", exception.Message, false),
            HttpStatusCode.Forbidden => new OnboardingErrorDto(OnboardingErrorCode.PermissionDenied, "Permission was denied for the requested onboarding mutation.", exception.Message, false),
            HttpStatusCode.Conflict => new OnboardingErrorDto(OnboardingErrorCode.Conflict, "The requested onboarding mutation conflicts with the current graph state.", exception.Message, false),
            HttpStatusCode.ServiceUnavailable => new OnboardingErrorDto(OnboardingErrorCode.TfsUnavailable, "The onboarding backend is temporarily unavailable.", exception.Message, true),
            _ => new OnboardingErrorDto(OnboardingErrorCode.ValidationFailed, "The onboarding mutation failed.", exception.Message, false)
        };
    }

    private static bool TryParseSuccessPayload<TResult>(string? response, out TResult result)
    {
        result = default!;

        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<TResult>(response, SuccessSerializerOptions);
            if (parsed is null)
            {
                return false;
            }

            result = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
