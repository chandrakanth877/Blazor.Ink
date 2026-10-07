using System.Globalization;
using System.Text;
using Wcwidth;

namespace Blazor.Ink;

internal readonly record struct Glyph(string Value, int Width, string Style);

public static class TerminalText
{
    public static string Plain(string value) => string.Concat(Parse(value, preserveStyles: false).Select(g => g.Value));
    public static int Width(string value) => Lines(Parse(value, preserveStyles: false), int.MaxValue, TextWrap.Hard).Max(line => line.Sum(g => g.Width));

    internal static string Background(string style)
    {
        var result = "";
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(style, @"\x1b\[([0-9;]*)m"))
        {
            var codes = match.Groups[1].Value.Split(';').Select(code => int.TryParse(code, out var number) ? number : 0).ToArray();
            for (var i = 0; i < codes.Length; i++)
            {
                var code = codes[i];
                if (code is 0 or 49) result = "";
                else if (code is >= 40 and <= 47 or >= 100 and <= 107) result = $"\x1b[{code}m";
                else if (code is 38 or 48)
                {
                    var count = i + 1 < codes.Length ? codes[i + 1] switch { 5 => 2, 2 => 4, _ => 0 } : 0;
                    if (count == 0 || i + count >= codes.Length) continue;
                    if (code == 48) result = "\x1b[" + string.Join(';', codes[i..(i + count + 1)]) + "m";
                    i += count;
                }
            }
        }
        return result;
    }

    internal static string Encode(IEnumerable<Glyph> glyphs)
    {
        var text = new StringBuilder();
        var style = "";
        foreach (var glyph in glyphs)
        {
            if (glyph.Style != style)
            { if (style != "") text.Append("\x1b[0m"); text.Append(glyph.Style); style = glyph.Style; }
            text.Append(glyph.Value);
        }
        if (style != "") text.Append("\x1b[0m");
        return text.ToString();
    }

    internal static List<Glyph> Parse(string value, string initialStyle = "", bool preserveStyles = true)
    {
        var result = new List<Glyph>();
        var defaults = new Dictionary<int, string>();
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(initialStyle, @"\x1b\[([0-9;]*)m"))
            ApplySgr(defaults, match.Groups[1].Value, new Dictionary<int, string>());
        var active = new Dictionary<int, string>(defaults);
        var style = preserveStyles ? SgrString(active) : "";
        var text = new StringBuilder();
        void Flush()
        {
            if (text.Length > 0) result.Add(new(text.ToString(), 0, style));
            text.Clear();
        }
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c is '\x1b' or '\x9b' or '\x9d' or '\x90' or '\x98' or '\x9e' or '\x9f')
            {
                Flush();
                var csi = c == '\x9b' || c == '\x1b' && i + 1 < value.Length && value[i + 1] == '[';
                var controlString = c is '\x9d' or '\x90' or '\x98' or '\x9e' or '\x9f' ||
                    c == '\x1b' && i + 1 < value.Length && "]PX^_".Contains(value[i + 1]);
                var start = i;
                if (csi)
                {
                    i += c == '\x1b' ? 2 : 1;
                    var parameters = i;
                    while (i < value.Length && value[i] is >= '0' and <= '?' ) i++;
                    var end = i;
                    while (i < value.Length && value[i] is >= ' ' and <= '/') i++;
                    if (i < value.Length && value[i] == 'm' && end == i &&
                        value[parameters..end].All(ch => char.IsAsciiDigit(ch) || ch == ';'))
                    {
                        if (preserveStyles)
                        {
                            ApplySgr(active, value[parameters..end], defaults);
                            style = SgrString(active);
                        }
                    }
                }
                else if (controlString)
                {
                    var osc = c == '\x9d' || c == '\x1b' && value[i + 1] == ']';
                    if (c == '\x1b') i++;
                    while (++i < value.Length && value[i] != '\x9c' && !(osc && value[i] == '\x07'))
                        if (value[i] == '\x1b' && i + 1 < value.Length && value[i + 1] == '\\') { i++; break; }
                }
                else
                {
                    while (++i < value.Length && value[i] is >= ' ' and <= '/') { }
                }
                if (i == start) i++;
                continue;
            }
            if (char.IsControl(c) && c is not ('\n' or '\t')) continue;
            // Tabs are expanded before measurement; terminal tab stops cannot own layout.
            text.Append(c == '\t' ? "    " : c.ToString());
        }
        Flush();
        return Combine(result);
    }

    // Segment after concatenating inline runs: component/style boundaries are not grapheme boundaries.
    internal static List<Glyph> Combine(IEnumerable<Glyph> source)
    {
        var runs = source.Where(run => run.Value.Length != 0).ToArray();
        if (runs.Length == 0) return [];
        var text = string.Concat(runs.Select(run => run.Value));
        var parts = StringInfo.GetTextElementEnumerator(text);
        var result = new List<Glyph>();
        var runIndex = 0;
        var runEnd = runs[0].Value.Length;
        while (parts.MoveNext())
        {
            while (runEnd <= parts.ElementIndex) runEnd += runs[++runIndex].Value.Length;
            var glyph = (string)parts.Current;
            // A grapheme is indivisible; its leading text run owns the cell style.
            result.Add(new(glyph, glyph == "\n" ? 0 : Math.Max(0, UnicodeCalculator.GetWidth(glyph)), runs[runIndex].Style));
        }
        return result;
    }

    private static string SgrString(Dictionary<int, string> active) =>
        string.Concat(active.OrderBy(pair => pair.Key).Select(pair => pair.Value));

    // Store effective style families, never the entire history of SGR commands.
    private static void ApplySgr(Dictionary<int, string> active, string parameters, Dictionary<int, string> defaults)
    {
        var codes = parameters.Split(';').Select(code => code == "" ? 0 : int.TryParse(code, out var n) ? n : -1).ToArray();
        void Reset(int key)
        {
            if (defaults.TryGetValue(key, out var original)) active[key] = original;
            else active.Remove(key);
        }
        for (var i = 0; i < codes.Length; i++)
        {
            var code = codes[i];
            if (code == 0) { active.Clear(); foreach (var pair in defaults) active.Add(pair.Key, pair.Value); }
            else if (code is 38 or 48)
            {
                var count = i + 1 < codes.Length ? codes[i + 1] switch { 5 => 2, 2 => 4, _ => 0 } : 0;
                if (count > 0 && i + count < codes.Length && codes[(i + 2)..(i + count + 1)].All(n => n is >= 0 and <= 255))
                    active[code] = "\x1b[" + string.Join(';', codes[i..(i + count + 1)]) + "m";
                i += count;
            }
            else if (code == 22) { Reset(1); Reset(2); }
            else if (code is 23 or 24 or 25 or 27 or 28 or 29 or 39 or 49)
                Reset(code switch { 23 => 3, 24 => 4, 25 => 5, 27 => 7, 28 => 8, 29 => 9, 39 => 38, _ => 48 });
            else
            {
                var key = code switch
                {
                    1 or 2 or 3 or 4 or 7 or 8 or 9 => code,
                    5 or 6 => 5, 21 => 4,
                    >= 10 and <= 19 => 10,
                    >= 30 and <= 37 or >= 90 and <= 97 => 38,
                    >= 40 and <= 47 or >= 100 and <= 107 => 48,
                    _ => -1
                };
                if (key != -1) active[key] = $"\x1b[{code}m";
            }
        }
    }

    internal static List<List<Glyph>> Lines(List<Glyph> glyphs, int width, TextWrap mode)
    {
        List<List<Glyph>> input = [[]];
        foreach (var glyph in glyphs)
            if (glyph.Value == "\n") input.Add([]); else input[^1].Add(glyph);
        var result = new List<List<Glyph>>();
        foreach (var line in input)
        {
            if (mode is TextWrap.Truncate or TextWrap.TruncateEnd or TextWrap.TruncateStart or TextWrap.TruncateMiddle)
            {
                if (line.Sum(g => g.Width) <= width) { result.Add(line); continue; }
                if (width <= 0) { result.Add([]); continue; }
                var remaining = width - 1;
                var prefix = Take(line, mode == TextWrap.TruncateMiddle ? (remaining + 1) / 2 : remaining);
                var suffix = Take(line.AsEnumerable().Reverse(), mode == TextWrap.TruncateMiddle ? remaining / 2 : remaining);
                suffix.Reverse();
                result.Add(mode switch
                {
                    TextWrap.TruncateStart => [new("…", 1, line.FirstOrDefault().Style ?? ""), .. suffix],
                    TextWrap.TruncateMiddle => [.. prefix, new("…", 1, line.FirstOrDefault().Style ?? ""), .. suffix],
                    _ => [.. prefix, new("…", 1, line.LastOrDefault().Style ?? "")]
                });
                continue;
            }
            if (width <= 0) { result.Add(line); continue; }
            var current = new List<Glyph>();
            var used = 0;
            for (var index = 0; index < line.Count; index++)
            {
                var glyph = line[index];
                if (mode == TextWrap.Wrap && glyph.Value != " " && (index == 0 || line[index - 1].Value == " "))
                {
                    var wordWidth = 0;
                    for (var end = index; end < line.Count && line[end].Value != " "; end++) wordWidth += line[end].Width;
                    if (wordWidth <= width && used > 0 && used + wordWidth > width)
                    { result.Add(current); current = []; used = 0; }
                }
                if (used > 0 && used + glyph.Width > width)
                { result.Add(current); current = []; used = 0; }
                current.Add(glyph);
                used += glyph.Width;
            }
            result.Add(current);
        }
        return result;
    }

    private static List<Glyph> Take(IEnumerable<Glyph> source, int width)
    {
        var result = new List<Glyph>();
        foreach (var g in source)
        {
            if (g.Width > width) break;
            result.Add(g);
            width -= g.Width;
        }
        return result;
    }
}
