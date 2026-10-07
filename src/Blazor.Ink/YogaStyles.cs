using Facebook.Yoga;
using static Facebook.Yoga.YGNodeStyleAPI;

namespace Blazor.Ink;

internal static class YogaStyles
{
    public static void Apply(this BoxStyle s, Node n)
    {
        YGNodeStyleSetFlexDirection(n, s.FlexDirection ?? YGFlexDirection.Row);
        YGNodeStyleSetFlexGrow(n, Finite(s.FlexGrow ?? 0));
        YGNodeStyleSetFlexShrink(n, Finite(s.FlexShrink ?? 1));
        YGNodeStyleSetFlexWrap(n, s.FlexWrap ?? YGWrap.NoWrap);
        YGNodeStyleSetAlignItems(n, s.AlignItems ?? YGAlign.Stretch);
        YGNodeStyleSetAlignSelf(n, s.AlignSelf ?? YGAlign.Auto);
        YGNodeStyleSetAlignContent(n, s.AlignContent ?? YGAlign.FlexStart);
        YGNodeStyleSetJustifyContent(n, s.JustifyContent ?? YGJustify.FlexStart);
        YGNodeStyleSetDisplay(n, s.Display ?? YGDisplay.Flex);
        YGNodeStyleSetPositionType(n, s.Position ?? YGPositionType.Relative);
        Dimension(n, s.Width, YGNodeStyleSetWidth, YGNodeStyleSetWidthPercent);
        Dimension(n, s.Height, YGNodeStyleSetHeight, YGNodeStyleSetHeightPercent);
        Dimension(n, s.MinWidth, YGNodeStyleSetMinWidth, YGNodeStyleSetMinWidthPercent);
        Dimension(n, s.MinHeight, YGNodeStyleSetMinHeight, YGNodeStyleSetMinHeightPercent);
        Dimension(n, s.MaxWidth, YGNodeStyleSetMaxWidth, YGNodeStyleSetMaxWidthPercent);
        Dimension(n, s.MaxHeight, YGNodeStyleSetMaxHeight, YGNodeStyleSetMaxHeightPercent);
        Dimension(n, s.FlexBasis, YGNodeStyleSetFlexBasis, YGNodeStyleSetFlexBasisPercent);
        if (s.AspectRatio is { } ratio) YGNodeStyleSetAspectRatio(n, Finite(ratio));
        foreach (var (edge, value) in new[] { (YGEdge.Top, s.Top), (YGEdge.Bottom, s.Bottom), (YGEdge.Left, s.Left), (YGEdge.Right, s.Right) })
            if (value is { } length)
                if (length.IsPercent) YGNodeStyleSetPositionPercent(n, edge, length.Value);
                else YGNodeStyleSetPosition(n, edge, length.Value);
        Edges(n, YGNodeStyleSetMargin, s.Margin, s.MarginX, s.MarginY, s.MarginTop, s.MarginRight, s.MarginBottom, s.MarginLeft);
        Edges(n, YGNodeStyleSetPadding, s.Padding, s.PaddingX, s.PaddingY, s.PaddingTop, s.PaddingRight, s.PaddingBottom, s.PaddingLeft);
        YGNodeStyleSetGap(n, YGGutter.Row, Finite(s.RowGap ?? s.Gap ?? 0));
        YGNodeStyleSetGap(n, YGGutter.Column, Finite(s.ColumnGap ?? s.Gap ?? 0));
        var border = s.BorderStyle is null ? 0 : 1;
        YGNodeStyleSetBorder(n, YGEdge.Top, s.BorderTop == false ? 0 : border);
        YGNodeStyleSetBorder(n, YGEdge.Bottom, s.BorderBottom == false ? 0 : border);
        YGNodeStyleSetBorder(n, YGEdge.Left, s.BorderLeft == false ? 0 : border);
        YGNodeStyleSetBorder(n, YGEdge.Right, s.BorderRight == false ? 0 : border);
    }

    private static float Finite(float value) => float.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static void Dimension(Node node, Length? value, Action<Node, float> points, Action<Node, float> percent)
    { if (value is { } length) (length.IsPercent ? percent : points)(node, length.Value); }
    private static void Edges(Node node, Action<Node, YGEdge, float> set, float? all, float? horizontal, float? vertical,
        float? top, float? right, float? bottom, float? left)
    {
        set(node, YGEdge.Top, Finite(top ?? vertical ?? all ?? 0));
        set(node, YGEdge.Bottom, Finite(bottom ?? vertical ?? all ?? 0));
        set(node, YGEdge.Left, Finite(left ?? horizontal ?? all ?? 0));
        set(node, YGEdge.Right, Finite(right ?? horizontal ?? all ?? 0));
    }
}
