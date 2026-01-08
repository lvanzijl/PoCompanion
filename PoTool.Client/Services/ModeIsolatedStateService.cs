using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Shared.Settings;
using System.Text.Json;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing UI state that is isolated per data mode (Mock vs TFS).
/// </summary>
public class ModeIsolatedStateService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<ModeIsolatedStateService> _logger;
    private DataMode _currentMode = DataMode.Mock;

    public ModeIsolatedStateService(IJSRuntime jsRuntime, ILogger<ModeIsolatedStateService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Sets the current data mode. This determines which isolated state is accessed.
    /// </summary>
    public void SetMode(DataMode mode)
    {
        _currentMode = mode;
    }

    /// <summary>
    /// Gets the current data mode.
    /// </summary>
    public DataMode GetMode() => _currentMode;

    /// <summary>
    /// Saves the expand/collapse state for the current mode.
    /// </summary>
    public async Task SaveExpandedStateAsync(Dictionary<int, bool> expandedState)
    {
        try
        {
            var key = GetExpandedStateKey();
            var json = JsonSerializer.Serialize(expandedState);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
        }
        catch (Exception ex)
        {
            // Log but don't throw - JS interop errors should not break the application
            _logger.LogWarning(ex, "Failed to save expanded state for mode {Mode}", _currentMode);
        }
    }

    /// <summary>
    /// Loads the expand/collapse state for the current mode.
    /// </summary>
    public async Task<Dictionary<int, bool>> LoadExpandedStateAsync()
    {
        try
        {
            var key = GetExpandedStateKey();
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
            
            if (!string.IsNullOrEmpty(json))
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                if (dict != null)
                {
                    var result = new Dictionary<int, bool>();
                    foreach (var kv in dict.Where(kv => int.TryParse(kv.Key, out _)))
                    {
                        if (int.TryParse(kv.Key, out var id))
                        {
                            result[id] = kv.Value;
                        }
                    }
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - JS interop errors should not break the application
            _logger.LogWarning(ex, "Failed to load expanded state for mode {Mode}", _currentMode);
        }

        return new Dictionary<int, bool>();
    }

    /// <summary>
    /// Saves the selected work item IDs for the current mode.
    /// </summary>
    public async Task SaveSelectedItemsAsync(List<int> selectedIds)
    {
        try
        {
            var key = GetSelectedItemsKey();
            var json = JsonSerializer.Serialize(selectedIds);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
        }
        catch (Exception ex)
        {
            // Log but don't throw - JS interop errors should not break the application
            _logger.LogWarning(ex, "Failed to save selected items for mode {Mode}", _currentMode);
        }
    }

    /// <summary>
    /// Loads the selected work item IDs for the current mode.
    /// </summary>
    public async Task<List<int>> LoadSelectedItemsAsync()
    {
        try
        {
            var key = GetSelectedItemsKey();
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
            
            if (!string.IsNullOrEmpty(json))
            {
                var list = JsonSerializer.Deserialize<List<int>>(json);
                if (list != null)
                {
                    return list;
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - JS interop errors should not break the application
            _logger.LogWarning(ex, "Failed to load selected items for mode {Mode}", _currentMode);
        }

        return new List<int>();
    }

    /// <summary>
    /// Clears all state for the current mode.
    /// </summary>
    public async Task ClearStateAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", GetExpandedStateKey());
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", GetSelectedItemsKey());
        }
        catch (Exception ex)
        {
            // Log but don't throw - JS interop errors should not break the application
            _logger.LogWarning(ex, "Failed to clear state for mode {Mode}", _currentMode);
        }
    }

    private string GetExpandedStateKey()
    {
        return $"workitems.expandedState.{_currentMode}";
    }

    private string GetSelectedItemsKey()
    {
        return $"workitems.selectedItems.{_currentMode}";
    }
}
