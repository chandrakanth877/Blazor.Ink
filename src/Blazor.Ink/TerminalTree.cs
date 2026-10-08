using Facebook.Yoga;
using static Facebook.Yoga.YGNodeAPI;
using static Facebook.Yoga.YGNodeStyleAPI;
using static Facebook.Yoga.YGNodeLayoutAPI;

namespace Blazor.Ink;

internal readonly record struct TerminalFrame(string Text, int Height)
{
    public string[] Lines => Height == 0 ? [] : Text.Split('\n');
    public static TerminalFrame Empty => new("", 0);
}

internal sealed record TerminalUpdate(TerminalFrame Live, IReadOnlyList<TerminalFrame> Static, int Columns, int Rows);

internal sealed class TerminalNode(string name)
{
    public object? Identity { get; init; }
    public string Name { get; } = name;
    public string Value { get; set; } = "";
    public Dictionary<string, object?> Attributes { get; } = [];
    public List<TerminalNode> Children { get; } = [];
    public Node? Layout { get; set; }
    public BoxStyle Style { get; set; } = new();
    public List<Glyph> Glyphs { get; set; } = [];
    public string PaintStyle { get; private set; } = "";
    public void CommitFrom(TerminalNode snapshot, IEnumerable<TerminalNode> children)
    {
        Value = snapshot.Value;
        Attributes.Clear();
        foreach (var pair in snapshot.Attributes) Attributes.Add(pair.Key, pair.Value);
        Children.Clear();
        Children.AddRange(children);
        Style = snapshot.Style;
        Glyphs = snapshot.Glyphs;
        PaintStyle = snapshot.PaintStyle;
        Layout = null;
    }

    public void Build(bool insideText = false, string inheritedStyle = "")
    {
        Style = BoxStyle.Read(Attributes);
        if (Name == "ink-root") Style = Style with { FlexDirection = YGFlexDirection.Column };
        if (insideText && Name == "ink-box") throw new InvalidOperationException("Box cannot be nested inside Text.");
        if (Name == "#text")
        {
            if (!insideText && !string.IsNullOrWhiteSpace(Value))
                throw new InvalidOperationException("Text must be inside Text.");
            Glyphs = insideText ? TerminalText.Parse(Value, inheritedStyle) : [];
            return;
        }
        if (Name is not ("ink-root" or "ink-box" or "ink-text"))
            throw new NotSupportedException($"Unsupported terminal element: {Name}");
        var isText = Name == "ink-text";
        var decorations = inheritedStyle + Decorations(Attributes);
        PaintStyle = decorations;
        foreach (var child in Children) child.Build(insideText || isText, decorations);
        if (isText)
        {
            Glyphs = TerminalText.Combine(Children.SelectMany(c => c.Glyphs));
            if (Attributes.TryGetValue("transform", out var transform) && transform is Func<string, int, string> callback)
            {
                var lines = TerminalText.Lines(Glyphs, int.MaxValue, TextWrap.Hard);
                Glyphs = TerminalText.Parse(string.Join('\n', lines.Select((line, index) => callback(TerminalText.Encode(line), index))), decorations);
            }
        }
        if (insideText)
        {
            return;
        }
        Layout = YGNodeNewWithConfig(new Config());
        Style.Apply(Layout);
        if (isText)
        {
            var wrap = WrapMode();
            var naturalLines = TerminalText.Lines(Glyphs, int.MaxValue, TextWrap.Hard);
            var naturalWidth = naturalLines.Max(line => line.Sum(g => g.Width));
            YGNodeSetMeasureFunc(Layout, (_, width, mode, _, _) =>
            {
                if (mode == MeasureMode.Undefined || naturalWidth <= width)
                    return new YGSize { Width = naturalWidth, Height = Glyphs.Count == 0 ? 0 : naturalLines.Count };
                var lines = TerminalText.Lines(Glyphs, Math.Max(0, (int)Math.Ceiling(width)), wrap);
                var truncated = wrap is TextWrap.Truncate or TextWrap.TruncateEnd or TextWrap.TruncateStart or TextWrap.TruncateMiddle;
                return new YGSize { Width = truncated ? width : lines.Max(l => l.Sum(g => g.Width)),
                    Height = Glyphs.Count == 0 ? 0 : lines.Count };
            });
        }
        else
            foreach (var child in Children.Where(c => c.Layout is not null))
                YGNodeInsertChild(Layout, child.Layout!, YGNodeGetChildCount(Layout));
    }

    public TextWrap WrapMode() => Attributes.TryGetValue("wrap", out var wrap)
        ? Enum.Parse<TextWrap>(wrap!.ToString()!.Replace("-", ""), true) : Style.TextWrap ?? TextWrap.Wrap;

    internal static string Decorations(Dictionary<string, object?> attributes)
    {
        var result = "";
        foreach (var (name, code) in new[] { ("bold", 1), ("dimColor", 2), ("italic", 3), ("underline", 4), ("inverse", 7), ("strikethrough", 9) })
            if (attributes.TryGetValue(name, out var value) && bool.TryParse(value?.ToString(), out var enabled) && enabled)
                result += $"\x1b[{code}m";
        foreach (var name in new[] { "color", "backgroundColor" })
            if (attributes.TryGetValue(name, out var color) && color is string text)
            {
                var hex = text.StartsWith('#') ? text : NamedColor(text);
                if (hex.Length != 7) throw new ArgumentException("Colors require #RRGGBB or a supported name.");
                var red = Convert.ToByte(hex[1..3], 16);
                var green = Convert.ToByte(hex[3..5], 16);
                var blue = Convert.ToByte(hex[5..7], 16);
                result += $"\x1b[{(name == "color" ? 38 : 48)};2;{red};{green};{blue}m";
            }
        return result;
    }

    private static string NamedColor(string name) => name.ToLowerInvariant() switch
    {
        "black" => "#000000", "red" => "#ff0000", "green" => "#008000", "yellow" => "#ffff00",
        "blue" => "#0000ff", "magenta" => "#ff00ff", "cyan" => "#00ffff", "white" => "#ffffff",
        "gray" or "grey" => "#808080", _ => throw new ArgumentException($"Unsupported preview color: {name}")
    };
}

internal static class TerminalTree
{
    // ponytail: bounded recursion protects Yoga's recursive solver; lift only after stack/soak evidence.
    internal const int MaxDepth = 64;
    internal static void CheckDepth(int depth)
    {
        if (depth > MaxDepth) throw new NotSupportedException($"Preview tree depth is limited to {MaxDepth}.");
    }

    internal static void Validate(TerminalNode root)
    {
        var pending = new Stack<(TerminalNode Node, bool InsideText, int Depth)>();
        pending.Push((root, false, 0));
        var count = 0;
        while (pending.TryPop(out var item))
        {
            CheckDepth(item.Depth);
            if (++count > 10_000) throw new NotSupportedException("Preview tree is limited to 10,000 nodes.");
            if (item.Node.Name == "ink-box" && item.InsideText)
                throw new InvalidOperationException("Box cannot be nested inside Text.");
            if (item.Node.Name == "#text")
            {
                if (!item.InsideText && !string.IsNullOrWhiteSpace(item.Node.Value))
                    throw new InvalidOperationException("Text must be inside Text.");
            }
            else if (item.Node.Name is not ("ink-root" or "ink-box" or "ink-text"))
                throw new NotSupportedException($"Unsupported terminal element: {item.Node.Name}");
            foreach (var child in item.Node.Children)
                pending.Push((child, item.InsideText || item.Node.Name == "ink-text", item.Depth + 1));
        }
    }

    internal static string Paint(TerminalNode root, int columns) => PaintFrame(root, columns).Text;

    internal static TerminalFrame PaintFrame(TerminalNode root, int columns)
    {
        try
        {
            root.Build();
            YGNodeStyleSetWidth(root.Layout!, columns);
            YGNodeCalculateLayout(root.Layout!, float.NaN, float.NaN, YGDirection.LTR);
            var height = YGNodeLayoutGetHeight(root.Layout!);
            if (!float.IsFinite(height) || height < 0 || height > Canvas.MaxDimension)
                throw new ArgumentOutOfRangeException(nameof(height), "Rendered height exceeds preview limits.");
            var canvas = new Canvas(columns, (int)height);
            Draw(root, canvas, 0, 0, 0, 0, canvas.Width, canvas.Height);
            return new(canvas.ToString(), canvas.Height);
        }
        finally
        {
            if (root.Layout is not null) YGNodeFreeRecursive(root.Layout);
        }
    }

    private static void Draw(TerminalNode node, Canvas canvas, int px, int py, int x1, int y1, int x2, int y2)
    {
        if (node.Layout is not { } layout || node.Style.Display == YGDisplay.None) return;
        var x = px + (int)YGNodeLayoutGetLeft(layout);
        var y = py + (int)YGNodeLayoutGetTop(layout);
        var w = (int)YGNodeLayoutGetWidth(layout);
        var h = (int)YGNodeLayoutGetHeight(layout);
        if (node.Name == "ink-text")
        {
            var lines = TerminalText.Lines(node.Glyphs, w, node.WrapMode());
            for (var row = 0; row < lines.Count; row++)
            {
                var column = x;
                foreach (var glyph in lines[row])
                {
                    if (column >= x1 && column + glyph.Width <= x2 && y + row >= y1 && y + row < y2)
                        canvas.Put(column, y + row, glyph);
                    else if (glyph.Width > 1 && y + row >= y1 && y + row < y2)
                        for (var cell = Math.Max(column, x1); cell < Math.Min(column + glyph.Width, x2); cell++)
                            canvas.Put(cell, y + row, new(" ", 1, TerminalText.Background(glyph.Style)));
                    column += glyph.Width;
                }
            }
            return;
        }
        var background = TerminalText.Background(node.PaintStyle);
        if (background != "")
            for (var row = Math.Max(y, y1); row < Math.Min(y + h, y2); row++)
                for (var col = Math.Max(x, x1); col < Math.Min(x + w, x2); col++)
                    canvas.Put(col, row, new(" ", 1, background));
        if (node.Style.BorderStyle is { } border && w > 0 && h > 0)
        {
            var chars = border switch
            {
                BorderStyle.Double => "╔╗╚╝═║", BorderStyle.Round => "╭╮╰╯─│", BorderStyle.Bold => "┏┓┗┛━┃",
                BorderStyle.Classic => "++++-|", BorderStyle.SingleDouble => "╓╖╙╜─║",
                BorderStyle.DoubleSingle => "╒╕╘╛═│", _ => "┌┐└┘─│"
            };
            string BorderPaint(string? color, string? bg, bool? dim) => background + TerminalNode.Decorations(new()
            {
                ["color"] = color ?? node.Style.BorderColor,
                ["backgroundColor"] = bg ?? node.Style.BorderBackgroundColor,
                ["dimColor"] = dim ?? node.Style.BorderDimColor
            });
            var topStyle = BorderPaint(node.Style.BorderTopColor, node.Style.BorderTopBackgroundColor, node.Style.BorderTopDimColor);
            var bottomStyle = BorderPaint(node.Style.BorderBottomColor, node.Style.BorderBottomBackgroundColor, node.Style.BorderBottomDimColor);
            var leftStyle = BorderPaint(node.Style.BorderLeftColor, node.Style.BorderLeftBackgroundColor, node.Style.BorderLeftDimColor);
            var rightStyle = BorderPaint(node.Style.BorderRightColor, node.Style.BorderRightBackgroundColor, node.Style.BorderRightDimColor);
            void Put(int cx, int cy, char c, string style)
            { if (cx >= x1 && cx < x2 && cy >= y1 && cy < y2) canvas.Put(cx, cy, new(c.ToString(), 1, style)); }
            if (node.Style.BorderTop != false)
                for (var i = 0; i < w; i++) Put(x + i, y, i == 0 && node.Style.BorderLeft != false ? chars[0] :
                    i == w - 1 && node.Style.BorderRight != false ? chars[1] : chars[4], topStyle);
            if (node.Style.BorderBottom != false)
                for (var i = 0; i < w; i++) Put(x + i, y + h - 1, i == 0 && node.Style.BorderLeft != false ? chars[2] :
                    i == w - 1 && node.Style.BorderRight != false ? chars[3] : chars[4], bottomStyle);
            for (var i = node.Style.BorderTop == false ? 0 : 1; i < h - (node.Style.BorderBottom == false ? 0 : 1); i++)
            {
                if (node.Style.BorderLeft != false) Put(x, y + i, chars[5], leftStyle);
                if (node.Style.BorderRight != false) Put(x + w - 1, y + i, chars[5], rightStyle);
            }
        }
        if ((node.Style.OverflowX ?? node.Style.Overflow) == Overflow.Hidden)
        { x1 = Math.Max(x1, x + (int)YGNodeLayoutGetBorder(layout, YGEdge.Left)); x2 = Math.Min(x2, x + w - (int)YGNodeLayoutGetBorder(layout, YGEdge.Right)); }
        if ((node.Style.OverflowY ?? node.Style.Overflow) == Overflow.Hidden)
        { y1 = Math.Max(y1, y + (int)YGNodeLayoutGetBorder(layout, YGEdge.Top)); y2 = Math.Min(y2, y + h - (int)YGNodeLayoutGetBorder(layout, YGEdge.Bottom)); }
        foreach (var child in node.Children)
            Draw(child, canvas, x - Offset(node.Style.ContentOffsetX), y - Offset(node.Style.ContentOffsetY), x1, y1, x2, y2);
    }

    private static int Offset(float? value) => value is { } v && float.IsFinite(v) ? (int)v : 0;
}

internal sealed class Canvas
{
    internal const int MaxDimension = 16_384;
    internal const int MaxCells = 1_000_000;
    public int Width { get; }
    public int Height { get; }
    private readonly Glyph?[,] cells;
    public Canvas(int width, int height)
    {
        if (width is < 0 or > MaxDimension || height is < 0 or > MaxDimension || (long)width * height > MaxCells)
            throw new ArgumentOutOfRangeException(nameof(width), "Canvas exceeds preview dimension/cell limits.");
        Width = width;
        Height = height;
        cells = new Glyph?[height, width];
    }

    public void Put(int x, int y, Glyph glyph)
    {
        if (x < 0 || y < 0 || y >= Height || x + glyph.Width > Width) return;
        if (glyph.Width == 0)
        {
            if (x > 0 && cells[y, x - 1] is { Width: > 0 } previous)
                cells[y, x - 1] = previous with { Value = previous.Value + glyph.Value };
            return;
        }
        for (var i = x; i < x + glyph.Width; i++)
        {
            if (cells[y, i] is { Width: 0 } && i > 0 && cells[y, i - 1] is { } leader)
                cells[y, i - 1] = new(" ", 1, TerminalText.Background(leader.Style));
            if (cells[y, i] is { Width: 2 } leader2 && i + 1 < Width)
                cells[y, i + 1] = new(" ", 1, TerminalText.Background(leader2.Style));
            cells[y, i] = null;
        }
        cells[y, x] = glyph;
        for (var i = 1; i < glyph.Width; i++) cells[y, x + i] = new("", 0, "");
    }

    public override string ToString()
    {
        var lines = new List<string>();
        for (var y = 0; y < Height; y++)
        {
            var end = Width;
            while (end > 0 && cells[y, end - 1] is null) end--;
            var line = new System.Text.StringBuilder();
            var style = "";
            for (var x = 0; x < end; x++)
            {
                var g = cells[y, x] ?? new Glyph(" ", 1, "");
                if (g.Style != style) { if (style != "") line.Append("\x1b[0m"); line.Append(g.Style); style = g.Style; }
                line.Append(g.Value);
            }
            if (style != "") line.Append("\x1b[0m");
            lines.Add(line.ToString());
        }
        return string.Join('\n', lines);
    }
}
