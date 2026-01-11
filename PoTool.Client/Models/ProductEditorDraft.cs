namespace PoTool.Client.Models;

/// <summary>
/// Draft state for ProductEditor form.
/// Used for persisting form data across navigation and page refreshes.
/// </summary>
public class ProductEditorDraft
{
    public string Name { get; set; } = string.Empty;
    public string BacklogRootWorkItemIdText { get; set; } = string.Empty;
    public int SelectedImageId { get; set; }
    public List<int> SelectedTeamIds { get; set; } = new();
}
