using Blazor.Ink;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ComponentDemo;

internal enum DemoAction { None, Exit, Stdout, Stderr, Clear, Flush, Rerender }

public sealed class ShowcaseState
{
    public const string Accent = "#B8A1FF";
    public const string Muted = "#8B91A0";
    public static readonly string[] Pages = ["Chat", "Text", "Box", "Static", "Transform", "Newline", "Spacer", "Runtime"];
    private int page;
    public ShowcaseState(int columns = 80, int rows = 24, string initialPage = "Chat")
    {
        Columns = columns;
        Rows = rows;
        page = Array.FindIndex(Pages, name => name.Equals(initialPage, StringComparison.OrdinalIgnoreCase));
        if (page < 0) throw new ArgumentException($"Unknown page '{initialPage}'. Choose: {string.Join(", ", Pages)}");
    }

    public int Columns { get; }
    public int Rows { get; }
    public int Padding => Columns < 60 ? 1 : 2;
    public int InnerColumns => Math.Max(1, Columns - 2 * Padding);
    public string Page => Pages[page];
    public string Draft { get; private set; } = "";
    public bool Thinking { get; private set; } = true;
    public List<string> Messages { get; } = [];
    public List<string> Documents { get; } = [];
    public List<string> Tasks { get; } = [];
    public int Scroll { get; private set; }
    public int MaxScroll { get; set; }
    public int ContentRows { get; set; }
    public int Offset => Page == "Chat" ? MaxScroll - Scroll : Scroll;
    public bool InputAvailable { get; set; }
    public bool RawModeSupported { get; set; }
    public int Updates { get; private set; }
    public string Status { get; set; } = "Ready";
    public string LastInput { get; private set; } = "No input received";

    public int NavigationRows
    {
        get
        {
            var rows = 1;
            var used = 0;
            foreach (var name in Pages)
            {
                var width = name.Length + (name == Page ? 2 : 0);
                if (used > 0 && used + 1 + width > InnerColumns) { rows++; used = 0; }
                used += width + (used > 0 ? 1 : 0);
            }
            return rows;
        }
    }
    public int ViewportRows => Rows - 1 - (NavigationRows + 5 +
        (Page == "Chat" ? 6 + (Documents.Count > 0 ? 1 : 0) : 0));

    internal void ClampScroll() => Scroll = Math.Clamp(Scroll, 0, MaxScroll);

    // Legacy input can coalesce text and shortcuts into one read; paste never takes this path.
    internal IEnumerable<InkInputEvent> Expand(InkInputEvent input)
    {
        if (input.IsPaste || input.Key is { Ctrl: true } or { Meta: true } ||
            !string.IsNullOrEmpty(input.Key?.Name))
        {
            yield return input;
            yield break;
        }
        foreach (var part in Regex.Split(input.Text, "([\r\n\u000e\u0010\u000c\u0014])"))
        {
            if (part.Length == 0) continue;
            var key = part switch
            {
                "\r" or "\n" => new InkKey("return"),
                "\u000e" => new InkKey("n", Ctrl: true),
                "\u0010" => new InkKey("p", Ctrl: true),
                "\u000c" => new InkKey("l", Ctrl: true),
                "\u0014" => new InkKey("t", Ctrl: true),
                _ => new InkKey("")
            };
            if (Page == "Runtime" && key.Name == "")
                foreach (var rune in part.EnumerateRunes())
                    yield return new(rune.ToString(), key);
            else yield return new(part, key);
        }
    }

    internal DemoAction Handle(InkInputEvent input)
    {
        if (input.IsPaste) { Paste(input.Text); return DemoAction.None; }
        var key = input.Key ?? new InkKey("");
        LastInput = $"Key: {key.Name} · shift={key.Shift} ctrl={key.Ctrl} meta={key.Meta} · {Clean(input.Text)}";
        if (key.Name == "escape" || key is { Ctrl: true, Name: "c" }) return DemoAction.Exit;
        if (key.Ctrl && key.Name is "n" or "p")
        {
            page = (page + (key.Name == "n" ? 1 : Pages.Length - 1)) % Pages.Length;
            Scroll = MaxScroll = 0;
        }
        else if (key.Name is "up" or "down")
        {
            var direction = key.Name == "down" ? 1 : -1;
            Scroll = Math.Clamp(Scroll + direction * (Page == "Chat" ? -3 : 3), 0, MaxScroll);
        }
        else if (Page == "Static" && key.Name is "return" or "enter")
            Tasks.Add($"✓ Completed task {Tasks.Count + 1}");
        else if (Page == "Chat")
        {
            if (key.Ctrl && key.Name == "l") ClearChat();
            else if (key.Ctrl && key.Name == "t") Thinking = !Thinking;
            else if (key.Name is "return" or "enter") Submit();
            else if (key.Name is "backspace" or "delete")
            {
                if (Draft.Length > 0) Draft = Draft[..StringInfo.ParseCombiningCharacters(Draft)[^1]];
            }
            else if (!key.Ctrl && !key.Meta && key.Name != "tab") Draft += Clean(input.Text);
        }
        else if (Page == "Runtime" && !key.Ctrl && !key.Meta)
        {
            var action = input.Text switch
            {
                "o" => DemoAction.Stdout, "e" => DemoAction.Stderr, "c" => DemoAction.Clear,
                "f" => DemoAction.Flush, "r" => DemoAction.Rerender, _ => DemoAction.None
            };
            if (action != DemoAction.None) { Updates++; Status = $"{action} · update {Updates}"; }
            return action;
        }
        return DemoAction.None;
    }

    internal void Paste(string text)
    {
        var clean = Clean(text);
        LastInput = $"Paste: {clean}";
        if (Page == "Chat") Draft += clean;
    }

    private static string Clean(string text) => TerminalText.Plain(text
        .Replace("\r\n", "\n").Replace('\r', '\n').Replace('\n', ' ').Replace('\t', ' '));

    private void Submit()
    {
        var value = Draft.Trim();
        if (value.Length == 0) return;
        if (value == "/clear") ClearChat();
        else if (value.StartsWith("/add ", StringComparison.Ordinal))
        {
            var document = value[5..].Trim();
            if (document.Length > 0 && !Documents.Contains(document)) Documents.Add(document);
        }
        else Messages.Add(value);
        Draft = "";
        Scroll = 0;
    }

    private void ClearChat()
    {
        Draft = "";
        Messages.Clear();
        Documents.Clear();
        Thinking = true;
        Scroll = 0;
    }
}
