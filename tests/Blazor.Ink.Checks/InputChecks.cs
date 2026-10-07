using System.Text;
using Blazor.Ink;

namespace Blazor.Ink.Checks;

internal static class InputChecks
{
    public static int Parser()
    {
        var checks = 0;
        void Equal<T>(T expected, T actual, string name)
        {
            if (!Equals(expected, actual)) throw new Exception($"{name}: expected [{expected}], got [{actual}]");
            checks++;
        }
        List<InkInputEvent> Parse(string text) => new InputParser().Push(Encoding.UTF8.GetBytes(text));
        foreach (var (sequence, name, shift, ctrl, meta) in new[]
        {
            ("\x1b[A", "up", false, false, false), ("\x1bOB", "down", false, false, false),
            ("\x1b[1;5D", "left", false, true, false), ("\x1bO2P", "f1", true, false, false),
            ("\x1b[[C", "f3", false, false, false), ("\x1b[[5~", "pageup", false, false, false),
            ("\x1b[3$", "delete", true, false, false), ("\x1b[6^", "pagedown", false, true, false),
            ("\x1b\x1b[A", "up", false, false, true), ("\x1b[Z", "tab", true, false, false)
        })
        {
            var bytes = Encoding.UTF8.GetBytes(sequence);
            for (var split = 0; split <= bytes.Length; split++)
            {
                var parser = new InputParser();
                var events = parser.Push(bytes.AsSpan(0, split));
                events.AddRange(parser.Push(bytes.AsSpan(split)));
                Equal(1, events.Count, $"key split {split} {name}");
                Equal(new InkKey(name, shift, ctrl, meta), events[0].Key!.Value, $"key metadata {name}");
            }
        }
        foreach (var text in new[] { "你好", "👩‍💻", "é", "🇺🇸" })
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            for (var split = 0; split <= bytes.Length; split++)
            {
                var parser = new InputParser();
                var events = parser.Push(bytes.AsSpan(0, split));
                events.AddRange(parser.Push(bytes.AsSpan(split)));
                Equal(text, string.Concat(events.Select(e => e.Text)), $"UTF-8 split {split}");
            }
        }
        const string paste = "a\r\n\x03\x1b[A👩‍💻";
        var packet = Encoding.UTF8.GetBytes("\x1b[200~" + paste + "\x1b[201~x");
        for (var split = 0; split <= packet.Length; split++)
        {
            var parser = new InputParser();
            var events = parser.Push(packet.AsSpan(0, split));
            events.AddRange(parser.Push(packet.AsSpan(split)));
            Equal(paste, events.Single(e => e.IsPaste).Text, $"paste split {split}");
            Equal<InkKey?>(null, events.Single(e => e.IsPaste).Key, "paste is not a command");
            Equal("x", events.Last().Text, "text after paste");
        }
        var pending = new InputParser();
        Equal(0, pending.Push([27]).Count, "hold Escape");
        Equal(true, pending.HasPendingEscape, "Escape needs timer");
        Equal("escape", pending.FlushEscape()!.Key!.Value.Name, "Escape timeout");
        pending.Push(Encoding.UTF8.GetBytes("\x1b["));
        Equal("[", pending.FlushEscape()!.Text, "incomplete CSI fallback");
        pending.Push(Encoding.UTF8.GetBytes("\x1b[200"));
        Equal(false, pending.HasPendingEscape, "paste prefix has no timeout");
        pending.Push(Encoding.UTF8.GetBytes("~literal"));
        Equal(false, pending.HasPendingEscape, "established paste has no timeout");
        Equal<InkInputEvent?>(null, pending.FlushEscape(), "paste cannot timeout flush");
        Equal("literal", pending.Push(Encoding.UTF8.GetBytes("\x1b[201~")).Single().Text, "finish held paste");
        Equal(0, Parse("\x1b[?1;2c\x1b[12;20R\x1b[I\x1b[97u").Count, "unknown replies and deferred Kitty discarded");
        Equal("😀", Parse("\x1b😀").Single().Text, "supplementary Alt input");
        Equal(true, Parse("\x1b😀").Single().Key!.Value.Meta, "supplementary Alt modifier");
        Equal("\r", Parse("\x1bOM").Single().Text, "keypad Enter");
        Equal("c", Parse("\x03").Single().Text, "Ctrl+C input");
        Equal(5, Parse("a\u0003b\x7f\b").Count, "control bytes split out of buffered text");
        Equal("[\n", string.Concat(Parse("\x1b[\n").Select(input => input.Text)), "invalid CSI continuation falls back without losing text");
        Equal("O", Parse("\x1bO\x1b[A").First().Text, "invalid SS3 does not consume the next key");
        Equal(new InkKey("escape", Meta: true), Parse("\x1b\x1b[\n").First().Key!.Value, "invalid double-Escape CSI preserves the escaped Escape");
        Equal(new InkKey(""), Parse("+").Single().Key!.Value, "legacy plain punctuation has no key name");
        Equal(new InkKey(""), Parse("你").Single().Key!.Value, "legacy plain Unicode has no key name");
        pending.Push(Encoding.UTF8.GetBytes("\x1b["));
        Equal(new InkKey(""), pending.FlushEscape()!.Key!.Value, "legacy partial CSI is not Alt+[");
        var bounded = new InputParser();
        bounded.Push(Encoding.UTF8.GetBytes("\x1b[200~"));
        try
        {
            bounded.Push(Encoding.UTF8.GetBytes(new string('x', 1_000_001)));
            throw new Exception("pending input must be bounded");
        }
        catch (InvalidOperationException) { checks++; }
        return checks;
    }
}
