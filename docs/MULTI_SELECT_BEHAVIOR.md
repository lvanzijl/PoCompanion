# Multi-Select Dropdown Behavior

## Overview
This document explains how multi-select dropdowns work in the application using MudBlazor components.

## MudBlazor Multi-Select Behavior

### Default Behavior
When a `MudSelect` component has `MultiSelection="true"`:
1. **Checkboxes appear automatically** when you click to open the dropdown
2. Checkboxes are shown next to each selectable item
3. Multiple items can be selected by clicking their checkboxes
4. Selected items are displayed in the input field as comma-separated values
5. The dropdown remains open to allow multiple selections
6. Click outside or press Escape to close the dropdown

### Visual States
- **Closed**: Shows selected items as comma-separated text
- **Open**: Shows dropdown list with checkboxes next to each item
- **Selected Item**: Checkbox is checked and item may be highlighted
- **Unselected Item**: Checkbox is unchecked

### User Interaction
1. Click the dropdown field to open it
2. Click checkboxes next to items to select/deselect them
3. Click outside the dropdown or press Escape to close
4. The selected values are immediately reflected in the form

## Locations Using Multi-Select

### Profile Manager Dialog
- **Area Paths**: Multi-select for choosing multiple area paths
- **Goals**: Multi-select for choosing multiple goal IDs
- Both dropdowns support multiple selections with checkboxes

### Validation History Panel
- **Area Path Filter**: Multi-select for filtering by multiple area paths
- Includes "Apply Filters" and "Clear Filters" buttons

## Troubleshooting

### "I don't see checkboxes"
**Cause**: Checkboxes only appear when the dropdown is **open**.
**Solution**: Click on the dropdown field to open it, then you'll see checkboxes.

### "The dropdown shows but no items"
**Cause**: The data hasn't loaded yet or there's no data available.
**Solution**: 
- Wait for data to load (check for loading indicators)
- For Profiles: Ensure work items are synced
- For Area Paths: Sync work items first
- Check console logs for errors

### "Selected items don't show"
**Cause**: The component binding might not be working correctly.
**Solution**: 
- Check that you're using `@bind-SelectedValues` (not `@bind-Value`)
- Ensure the bound property is of type `IEnumerable<T>`

## Technical Implementation

### CompactSelect Wrapper
The `CompactSelect` component wraps `MudSelect` with compact defaults:
- `Dense="true"` for compact spacing
- `Margin="Margin.Dense"` for tight margins
- `MultiSelection` parameter is passed through directly
- All MudBlazor multi-select behavior is preserved

### Code Example
```razor
<CompactSelect T="string"
               Label="Area Paths"
               @bind-SelectedValues="_selectedAreaPaths"
               MultiSelection="true"
               HelperText="Select one or more area paths">
    @foreach (var areaPath in _availableAreaPaths)
    {
        <MudSelectItem T="string" Value="@areaPath">@areaPath</MudSelectItem>
    }
</CompactSelect>
```

```csharp
private IEnumerable<string> _selectedAreaPaths = new List<string>();
private List<string> _availableAreaPaths = new();

// When clearing selections programmatically, call StateHasChanged() to ensure UI updates
private void ClearSelections()
{
    _selectedAreaPaths = new List<string>();
    StateHasChanged(); // Ensures the UI reflects the change
}
```

### Important Notes
- When modifying `_selectedAreaPaths` directly (not through user interaction), call `StateHasChanged()` to ensure the UI updates
- The `@bind-SelectedValues` directive handles UI updates automatically for user interactions
- Direct assignment bypasses the normal change detection mechanism

## References
- [MudBlazor Select Documentation](https://mudblazor.com/components/select)
- [Fluent UI Compact Rules](./Fluent_UI_compat_rules.md)
- [UI Rules](./UI_RULES.md)
