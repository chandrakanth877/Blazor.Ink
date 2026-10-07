using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazor.Ink.Components;

public sealed class Text : ComponentBase
{
    [Parameter] public string? Content { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public TextWrap Wrap { get; set; } = TextWrap.Wrap;
    [Parameter] public string? Color { get; set; }
    [Parameter] public string? BackgroundColor { get; set; }
    [Parameter] public bool Bold { get; set; }
    [Parameter] public bool DimColor { get; set; }
    [Parameter] public bool Italic { get; set; }
    [Parameter] public bool Underline { get; set; }
    [Parameter] public bool Strikethrough { get; set; }
    [Parameter] public bool Inverse { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "ink-text");
        builder.AddAttribute(1, "wrap", Wrap);
        builder.AddAttribute(2, "color", Color);
        builder.AddAttribute(3, "backgroundColor", BackgroundColor);
        builder.AddAttribute(4, "bold", Bold);
        builder.AddAttribute(5, "dimColor", DimColor);
        builder.AddAttribute(6, "italic", Italic);
        builder.AddAttribute(7, "underline", Underline);
        builder.AddAttribute(8, "strikethrough", Strikethrough);
        builder.AddAttribute(9, "inverse", Inverse);
        builder.AddContent(10, Content);
        builder.AddContent(11, ChildContent);
        builder.CloseElement();
    }
}
