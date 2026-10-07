using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Facebook.Yoga;

namespace Blazor.Ink.Components;

public sealed class Newline : ComponentBase
{
    [Parameter] public int Count { get; set; } = 1;
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Count < 0) throw new ArgumentOutOfRangeException(nameof(Count));
        builder.OpenElement(0, "ink-text");
        builder.AddContent(1, new string('\n', Count));
        builder.CloseElement();
    }
}

public sealed class Spacer : Box
{
    public Spacer() => Style = new() { FlexGrow = 1 };
}

public sealed class Transform : ComponentBase
{
    [Parameter, EditorRequired] public Func<string, int, string> Apply { get; set; } = null!;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(Apply);
        builder.OpenElement(0, "ink-text");
        builder.AddAttribute(1, "transform", Apply);
        builder.AddContent(2, ChildContent);
        builder.CloseElement();
    }
}

/// <summary>Append-only committed output in a session; ordinary static layout when rendered headlessly.</summary>
public sealed class Static<TItem> : ComponentBase
{
    [Parameter] public IReadOnlyList<TItem> Items { get; set; } = [];
    [Parameter, EditorRequired] public RenderFragment<TItem>? ChildContent { get; set; }
    [Parameter] public BoxStyle Style { get; set; } = new();
    private int rendered;
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Items);
        rendered = Math.Min(rendered, Items.Count);
    }
    private void Commit(int count)
    {
        if (rendered == count) return;
        rendered = count;
        StateHasChanged();
    }
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(Items);
        builder.OpenElement(0, "ink-box");
        (Style with { FlexDirection = Style.FlexDirection ?? YGFlexDirection.Column,
            Display = Items.Count > rendered ? Style.Display : YGDisplay.None }).WriteAttributes(builder);
        builder.AddAttribute(70, "static", true);
        builder.AddAttribute(71, "staticCount", Items.Count);
        builder.AddAttribute(72, "staticCommit", (Action<int>)Commit);
        for (var i = rendered; i < Items.Count; i++)
        {
            builder.OpenElement(73, "ink-box");
            builder.SetKey(i);
            builder.AddAttribute(74, "flexDirection", "column");
            builder.AddAttribute(75, "staticItem", i);
            builder.AddContent(76, ChildContent?.Invoke(Items[i]));
            builder.CloseElement();
        }
        builder.CloseElement();
    }
}
