using System.Web;
using Microsoft.AspNetCore.Components;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing navigation context throughout the application.
/// Implements immutable context management with URL serialization support.
/// </summary>
public class NavigationContextService : INavigationContextService
{
    private readonly NavigationManager _navigationManager;
    private readonly IProfileService _profileService;
    private NavigationContext? _current;
    private readonly Stack<NavigationContext> _contextStack = new();

    public NavigationContextService(
        NavigationManager navigationManager,
        IProfileService profileService)
    {
        _navigationManager = navigationManager;
        _profileService = profileService;
    }

    /// <inheritdoc />
    public NavigationContext Current
    {
        get
        {
            if (!HasValidProfile())
            {
                throw new InvalidOperationException("No profile selected");
            }
            return _current ?? throw new InvalidOperationException("No navigation context set");
        }
    }

    /// <inheritdoc />
    public NavigationContext? CurrentOrDefault => _current;

    /// <inheritdoc />
    public event EventHandler<NavigationContextChangedEventArgs>? ContextChanged;

    /// <inheritdoc />
    public async Task NavigateWithContextAsync(string route, NavigationContext context)
    {
        var previous = _current;
        
        // Push current context to stack for back navigation
        if (_current != null)
        {
            _contextStack.Push(_current);
        }
        
        // Set new context with parent reference
        _current = context with { Parent = previous };
        
        // Raise event
        RaiseContextChanged(previous, _current);
        
        // Build URL with context parameters
        var url = BuildUrlWithContext(route, _current);
        
        // Navigate
        _navigationManager.NavigateTo(url);
        
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task NavigateBackAsync()
    {
        if (_contextStack.Count == 0 || _current?.Parent == null)
        {
            // Navigate to home if no parent context
            _navigationManager.NavigateTo("/home");
            return;
        }

        var previous = _current;
        _current = _contextStack.Pop();
        
        RaiseContextChanged(previous, _current);
        
        // Navigate to appropriate workspace based on context
        var route = GetRouteForContext(_current);
        var url = BuildUrlWithContext(route, _current);
        _navigationManager.NavigateTo(url);
        
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public NavigationContext WithUpdates(Func<NavigationContext, NavigationContext> updater)
    {
        if (_current == null)
        {
            throw new InvalidOperationException("No current context to update");
        }
        
        return updater(_current);
    }

    /// <inheritdoc />
    public bool CanPerform(string action)
    {
        if (_current == null)
        {
            return false;
        }

        // Define action permissions based on context
        return action switch
        {
            "navigate-to-analysis" => true,
            "navigate-to-planning" => true,
            "navigate-to-communication" => true,
            "navigate-to-team" => _current.Scope.ProductId != null,
            "share" => _current.Intent != Intent.Plannen || _current.TimeHorizon == TimeHorizon.Future,
            _ => true
        };
    }

    /// <inheritdoc />
    public bool HasValidProfile()
    {
        return _current?.Scope?.ProfileId != null 
            && _profileService.IsActiveProfileValid();
    }

    /// <inheritdoc />
    public void SetInitialContext(Intent intent, int profileId)
    {
        var previous = _current;
        _contextStack.Clear();
        
        _current = new NavigationContext
        {
            Intent = intent,
            Scope = new Scope
            {
                Level = ScopeLevel.Portfolio,
                ProfileId = profileId
            },
            Trigger = new Trigger { Type = TriggerType.Choice },
            TimeHorizon = GetDefaultTimeHorizonForIntent(intent)
        };
        
        RaiseContextChanged(previous, _current);
    }

    /// <inheritdoc />
    public void ClearContext()
    {
        var previous = _current;
        _current = null;
        _contextStack.Clear();
        
        if (previous != null)
        {
            ContextChanged?.Invoke(this, new NavigationContextChangedEventArgs
            {
                Previous = previous,
                Current = null
            });
        }
    }

    /// <inheritdoc />
    public string ToQueryString(NavigationContext context)
    {
        var parameters = new List<string>();
        
        parameters.Add($"intent={context.Intent.ToString().ToLowerInvariant()}");
        parameters.Add($"scope={context.Scope.Level.ToString().ToLowerInvariant()}");
        
        if (context.Scope.ProductId.HasValue)
        {
            parameters.Add($"productId={context.Scope.ProductId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(context.Scope.ProjectAlias))
        {
            parameters.Add($"projectAlias={HttpUtility.UrlEncode(context.Scope.ProjectAlias)}");
        }
        
        if (context.Scope.TeamId.HasValue)
        {
            parameters.Add($"teamId={context.Scope.TeamId.Value}");
        }
        
        if (!string.IsNullOrEmpty(context.Mode))
        {
            parameters.Add($"mode={HttpUtility.UrlEncode(context.Mode)}");
        }
        
        if (context.TimeHorizon != TimeHorizon.Current)
        {
            parameters.Add($"time={context.TimeHorizon.ToString().ToLowerInvariant()}");
        }
        
        if (context.Trigger?.Type != TriggerType.Choice)
        {
            parameters.Add($"trigger={context.Trigger?.Type.ToString().ToLowerInvariant()}");
        }
        
        return string.Join("&", parameters);
    }

    /// <inheritdoc />
    public NavigationContext? FromQueryString(string queryString)
    {
        if (string.IsNullOrEmpty(queryString))
        {
            return null;
        }

        try
        {
            var query = HttpUtility.ParseQueryString(queryString);
            
            var intentStr = query["intent"];
            if (string.IsNullOrEmpty(intentStr) || !Enum.TryParse<Intent>(intentStr, true, out var intent))
            {
                return null;
            }
            
            var scopeStr = query["scope"];
            if (string.IsNullOrEmpty(scopeStr) || !Enum.TryParse<ScopeLevel>(scopeStr, true, out var scopeLevel))
            {
                scopeLevel = ScopeLevel.Portfolio;
            }
            
            var projectAlias = query["projectAlias"];
            int? productId = int.TryParse(query["productId"], out var pid) ? pid : null;
            int? teamId = int.TryParse(query["teamId"], out var tid) ? tid : null;
            
            var mode = query["mode"];
            
            var timeStr = query["time"];
            var timeHorizon = Enum.TryParse<TimeHorizon>(timeStr, true, out var th) ? th : TimeHorizon.Current;
            
            var triggerStr = query["trigger"];
            var triggerType = Enum.TryParse<TriggerType>(triggerStr, true, out var tt) ? tt : TriggerType.Choice;
            
            return new NavigationContext
            {
                Intent = intent,
                Scope = new Scope
                {
                    Level = scopeLevel,
                    ProfileId = _profileService.GetActiveProfileId(),
                    ProjectAlias = projectAlias,
                    ProductId = productId,
                    TeamId = teamId
                },
                Mode = mode,
                TimeHorizon = timeHorizon,
                Trigger = new Trigger { Type = triggerType }
            };
        }
        catch
        {
            return null;
        }
    }

    private void RaiseContextChanged(NavigationContext? previous, NavigationContext current)
    {
        ContextChanged?.Invoke(this, new NavigationContextChangedEventArgs
        {
            Previous = previous,
            Current = current
        });
    }

    private string BuildUrlWithContext(string route, NavigationContext context)
    {
        var queryString = ToQueryString(context);
        return string.IsNullOrEmpty(queryString) ? route : $"{route}?{queryString}";
    }

    private static string GetRouteForContext(NavigationContext context)
    {
        return WorkspaceRoutes.GetRouteForIntent(context.Intent, context.Scope.Level);
    }

    private static TimeHorizon GetDefaultTimeHorizonForIntent(Intent intent)
    {
        return intent switch
        {
            Intent.Plannen => TimeHorizon.Future,
            Intent.Begrijpen => TimeHorizon.Current,
            _ => TimeHorizon.Current
        };
    }
}
