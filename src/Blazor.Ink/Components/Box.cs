using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Facebook.Yoga;

namespace Blazor.Ink.Components;

public class Box : ComponentBase
{
    [Parameter] public BoxStyle Style { get; set; } = new();
    [Parameter] public Length? Width { get; set; }
    [Parameter] public Length? Height { get; set; }
    [Parameter] public YGFlexDirection? FlexDirection { get; set; }
    [Parameter] public float? FlexGrow { get; set; }
    [Parameter] public float? Gap { get; set; }
    [Parameter] public float? Padding { get; set; }
    [Parameter] public BorderStyle? BorderStyle { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "ink-box");
        var style = Style with
        {
            Width = Width ?? Style.Width, Height = Height ?? Style.Height,
            FlexDirection = FlexDirection ?? Style.FlexDirection,
            FlexGrow = FlexGrow ?? Style.FlexGrow, Gap = Gap ?? Style.Gap,
            Padding = Padding ?? Style.Padding, BorderStyle = BorderStyle ?? Style.BorderStyle
        };
        style.WriteAttributes(builder);
        builder.AddContent(100, ChildContent);
        builder.CloseElement();
    }
}
