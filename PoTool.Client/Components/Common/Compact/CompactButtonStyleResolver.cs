using MudBlazor;

namespace PoTool.Client.Components.Common.Compact;

public readonly record struct CompactButtonAppearance(Variant Variant, Color Color, string CssClass);

public static class CompactButtonStyleResolver
{
    public static CompactButtonAppearance Resolve(ButtonRole role) => role switch
    {
        ButtonRole.Utility => new(Variant.Text, Color.Default, "compact-button--utility"),
        ButtonRole.Action => new(Variant.Outlined, Color.Default, "compact-button--action"),
        ButtonRole.Critical => new(Variant.Filled, Color.Error, "compact-button--critical"),
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}
