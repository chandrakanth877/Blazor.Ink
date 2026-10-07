using System.Text;
using System.Text.RegularExpressions;

namespace Blazor.Ink;

public readonly record struct InkKey(string Name, bool Shift = false, bool Ctrl = false, bool Meta = false);
public sealed record InkInputEvent(string Text, InkKey? Key = null, bool IsPaste = false);

// Translated legacy framing/key behavior from the pinned Ink source; see THIRD-PARTY-NOTICES.md.
internal sealed class InputParser
{
    internal const int MaxPending = 1_000_000;
    private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder pending = new();
    private bool pasting;
    private int pasteSearch;
    public bool HasPendingEscape => !pasting && pending.Length > 0 && pending[0] == '\x1b' &&
        pending.ToString() != "\x1b[200";

    public List<InkInputEvent> Push(ReadOnlySpan<byte> bytes, bool complete = false)
    {
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        var count = decoder.GetChars(bytes, chars, complete);
        // ponytail: one-million-character input ceiling; add a streaming paste API if larger payloads are needed.
        if (count > MaxPending - pending.Length) throw new InvalidOperationException("Pending input limit exceeded.");
        pending.Append(chars.AsSpan(0, count));
        List<InkInputEvent> events = [];
        while (pending.Length > 0)
        {
            if (pasting)
            {
                var end = FindPasteEnd();
                if (end < 0) break;
                events.Add(new(pending.ToString(0, end), IsPaste: true));
                pending.Remove(0, end + 6);
                pasting = false;
                pasteSearch = 0;
                continue;
            }
            // ponytail: incomplete controls rescan up to MaxPending; add a scan cursor if large replies become a workload.
            var text = pending.ToString();
            var consumed = 0;
            while (consumed < text.Length)
            {
                if (text[consumed] != '\x1b')
                {
                    var end = text.IndexOf('\x1b', consumed);
                    if (end < 0) end = text.Length;
                    AddText(text[consumed..end], events);
                    consumed = end;
                    continue;
                }
                var length = EscapeLength(text, consumed);
                if (length == 0) break;
                var sequence = text.Substring(consumed, length);
                consumed += length;
                if (sequence == "\x1b[200~")
                {
                    pasting = true;
                    break;
                }
                if (Keypress(sequence, completeControl: IsControl(sequence)) is { } input) events.Add(input);
            }
            pending.Remove(0, consumed);
            if (!pasting) break;
        }
        if (complete && (pasting || pending.ToString() == "\x1b[200"))
            throw new IOException("Input ended during bracketed paste.");
        return events;
    }

    public InkInputEvent? FlushEscape()
    {
        if (!HasPendingEscape) return null;
        var text = pending.ToString();
        pending.Clear();
        return Keypress(text, false);
    }

    private int FindPasteEnd()
    {
        const string end = "\x1b[201~";
        for (var i = pasteSearch; i <= pending.Length - end.Length; i++)
        {
            var matches = true;
            for (var j = 0; j < end.Length; j++)
                if (pending[i + j] != end[j]) { matches = false; break; }
            if (matches) return i;
        }
        pasteSearch = Math.Max(0, pending.Length - end.Length + 1);
        return -1;
    }

    private static void AddText(string text, List<InkInputEvent> events)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('\x7f' or '\b' or '\x03')) continue;
            if (i > start) events.Add(Keypress(text[start..i], false)!);
            events.Add(Keypress(text[i].ToString(), false)!);
            start = i + 1;
        }
        if (start < text.Length) events.Add(Keypress(text[start..], false)!);
    }

    private static bool IsControl(string text) => text.Length > 1 &&
        (text[1] is '[' or 'O' || text.Length > 2 && text[1] == '\x1b' && text[2] is '[' or 'O') &&
        EscapeLength(text, 0) == text.Length;

    private static int EscapeLength(string text, int start)
    {
        var prefix = 1;
        if (start + 1 == text.Length) return 0;
        if (text[start + 1] == '\x1b')
        {
            prefix = 2;
            if (start + 2 == text.Length) return 0;
            if (text[start + 2] is not ('[' or 'O')) return 2;
        }
        var type = text[start + prefix];
        if (type is not ('[' or 'O'))
            return prefix + (char.IsHighSurrogate(type) ? 2 : 1);
        var payload = start + prefix + 1;
        for (var i = payload; i < text.Length; i++)
        {
            var c = text[i];
            if (type == '[' && i == payload && c == '[') continue;
            if (type == '[' && c == '$' && i == payload + 1 && "235678".Contains(text[payload]))
                return i - start + 1;
            if (c is >= '@' and <= '~') return i - start + 1;
            var valid = type == '[' ? c is >= ' ' and <= '?' : c == ';' || char.IsAsciiDigit(c);
            if (!valid) return 2;
        }
        return 0;
    }

    private static readonly Regex functionKey = new(
        @"^\x1b+(O|N|\[|\[\[)(?:(\d+)(?:;(\d+))?([~^$])|(?:1;)?(\d+)?([a-zA-Z]))$",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Dictionary<string, string> names = new()
    {
        ["OP"] = "f1", ["OQ"] = "f2", ["OR"] = "f3", ["OS"] = "f4",
        ["[P"] = "f1", ["[Q"] = "f2", ["[R"] = "f3", ["[S"] = "f4",
        ["[11~"] = "f1", ["[12~"] = "f2", ["[13~"] = "f3", ["[14~"] = "f4",
        ["[[A"] = "f1", ["[[B"] = "f2", ["[[C"] = "f3", ["[[D"] = "f4", ["[[E"] = "f5",
        ["[15~"] = "f5", ["[17~"] = "f6", ["[18~"] = "f7", ["[19~"] = "f8",
        ["[20~"] = "f9", ["[21~"] = "f10", ["[23~"] = "f11", ["[24~"] = "f12",
        ["[A"] = "up", ["[B"] = "down", ["[C"] = "right", ["[D"] = "left", ["[E"] = "clear",
        ["[F"] = "end", ["[H"] = "home", ["OA"] = "up", ["OB"] = "down", ["OC"] = "right",
        ["OD"] = "left", ["OE"] = "clear", ["OF"] = "end", ["OH"] = "home",
        ["[1~"] = "home", ["[2~"] = "insert", ["[3~"] = "delete", ["[4~"] = "end",
        ["[5~"] = "pageup", ["[6~"] = "pagedown", ["[[5~"] = "pageup", ["[[6~"] = "pagedown",
        ["[7~"] = "home", ["[8~"] = "end", ["[a"] = "up", ["[b"] = "down", ["[c"] = "right",
        ["[d"] = "left", ["[e"] = "clear", ["[Z"] = "tab",
        ["[2$"] = "insert", ["[3$"] = "delete", ["[5$"] = "pageup", ["[6$"] = "pagedown",
        ["[7$"] = "home", ["[8$"] = "end", ["Oa"] = "up", ["Ob"] = "down", ["Oc"] = "right",
        ["Od"] = "left", ["Oe"] = "clear", ["[2^"] = "insert", ["[3^"] = "delete",
        ["[5^"] = "pageup", ["[6^"] = "pagedown", ["[7^"] = "home", ["[8^"] = "end"
    };

    private static InkInputEvent? Keypress(string sequence, bool completeControl)
    {
        var text = sequence;
        var name = "";
        var shift = false;
        var ctrl = false;
        var meta = sequence.StartsWith('\x1b') && sequence.Length > 1;
        var character = meta ? sequence[1..] : sequence;
        if (sequence is "\x1bOM" or "\x1b\x1bOM")
        { name = "return"; text = "\r"; meta = sequence.Length == 4; }
        else if (sequence is "\x1b" or "\x1b\x1b")
        { name = "escape"; meta = sequence.Length == 2; }
        else if (character.Length == 1)
        {
            var c = character[0];
            name = c switch
            {
                '\r' => "return", '\n' when !meta => "enter", '\t' => "tab", '\b' or '\x7f' => "backspace",
                ' ' or '\0' => "space", >= '0' and <= '9' when !meta => "number",
                >= '\x01' and <= '\x1f' => ((char)(c + (c <= 26 ? 96 : 64))).ToString(),
                _ when meta && c != '[' || c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' =>
                    char.ToLowerInvariant(c).ToString(),
                _ => ""
            };
            ctrl = c == '\0' || c is >= '\x01' and <= '\x1f' &&
                c is not ('\r' or '\t' or '\b') && (c != '\n' || meta);
            shift = c is >= 'A' and <= 'Z';
            if (meta && c == '[') meta = false;
        }
        else if (meta && !completeControl && character.EnumerateRunes().Count() == 1)
            name = character.ToLowerInvariant();
        else if (completeControl)
        {
            var match = functionKey.Match(sequence);
            if (match.Success)
            {
                var code = match.Groups[1].Value + match.Groups[2].Value + match.Groups[4].Value + match.Groups[6].Value;
                var modText = match.Groups[3].Success ? match.Groups[3].Value : match.Groups[5].Value;
                var modifier = int.TryParse(modText, out var value) ? Math.Max(0, value - 1) : 0;
                name = names.GetValueOrDefault(code, "");
                meta = sequence.StartsWith("\x1b\x1b", StringComparison.Ordinal) || (modifier & 10) != 0;
                shift = (modifier & 1) != 0 || code is "[a" or "[b" or "[c" or "[d" or "[e" or "[Z" ||
                    code.EndsWith('$');
                ctrl = (modifier & 4) != 0 || code is "Oa" or "Ob" or "Oc" or "Od" or "Oe" || code.EndsWith('^');
            }
            if (name == "") return null;
            text = "";
        }
        else meta = false;
        if (ctrl) text = name == "space" ? " " : name;
        if (name == "backspace" || completeControl && name != "return") text = "";
        if (text.StartsWith('\x1b')) text = text[1..];
        return new(text, new(name, shift, ctrl, meta));
    }
}
