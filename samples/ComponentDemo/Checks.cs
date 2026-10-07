using Blazor.Ink;
using Microsoft.AspNetCore.Components;
using System.Diagnostics;
using System.Text;

namespace ComponentDemo;

internal static class Checks
{
    private static int count;
    private static void Equal<T>(T expected, T actual, string name)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{name}: expected [{expected}], got [{actual}]");
        count++;
    }

    internal static async Task RunAsync()
    {
        var state = new ShowcaseState();
        InkInputEvent Key(string name, bool ctrl = false) => new("", new(name, Ctrl: ctrl));
        state.Handle(Key("n", ctrl: true));
        Equal("Text", state.Page, "next page");
        state.Handle(Key("p", ctrl: true));
        Equal("Chat", state.Page, "previous page");
        state.Handle(Key("p", ctrl: true));
        Equal("Runtime", state.Page, "navigation wraps");
        state.Handle(Key("n", ctrl: true));
        state.Handle(new("hello 👩‍💻é"));
        state.Handle(Key("backspace"));
        Equal("hello 👩‍💻", state.Draft, "delete combining grapheme");
        state.Handle(Key("delete"));
        Equal("hello ", state.Draft, "delete joined emoji");
        state.Handle(Key("return"));
        Equal("hello", state.Messages.Single(), "trim and submit message");
        Equal("", state.Draft, "submit clears composer");
        state.Handle(Key("return"));
        Equal(1, state.Messages.Count, "empty submit is ignored");
        state.Handle(new("/add demo.cs"));
        state.Handle(Key("return"));
        state.Handle(new("/add demo.cs"));
        state.Handle(Key("return"));
        Equal("demo.cs", state.Documents.Single(), "attachments are deduplicated labels");
        state.Handle(Key("t", ctrl: true));
        Equal(false, state.Thinking, "toggle thinking");
        state.MaxScroll = 20;
        state.Handle(Key("up"));
        Equal(3, state.Scroll, "chat scroll moves toward older messages");
        state.Handle(Key("down"));
        Equal(0, state.Scroll, "chat scroll returns toward newest messages");
        state.Paste("a\r\nb\tc\u0003\u001b]52;c;blocked\u0007");
        Equal("a b c", state.Draft, "paste is normalized and sanitized");
        Equal(1, state.Messages.Count, "paste does not submit or clear");
        state.Handle(Key("l", ctrl: true));
        Equal(0, state.Messages.Count + state.Documents.Count + state.Draft.Length, "clear conversation");
        Equal(true, state.Thinking, "clear resets thinking");
        state.Paste("/clear");
        Equal("/clear", state.Draft, "pasted commands remain draft text");
        state.Handle(Key("return"));
        Equal("", state.Draft, "explicit Enter runs pasted command");
        Equal(DemoAction.Exit, state.Handle(Key("escape")), "Escape exits");
        Equal(DemoAction.Exit, state.Handle(Key("c", ctrl: true)), "typed Ctrl+C exits");

        foreach (var columns in new[] { 40, 100 })
        foreach (var (page, expected) in new[] {
            ("Chat", "Ask Blazor.Ink"), ("Text", "green and bold"), ("Box", "SingleDouble"),
            ("Static", "Enter appends"), ("Transform", "2. Second rendered line"),
            ("Newline", "First\nSecond\n\nThird"), ("Spacer", "Bottom"), ("Runtime", "WriteErrorAsync")
        })
        {
            var preview = new ShowcaseState(columns, 24, page);
            if (page == "Static") preview.Tasks.Add("✓ Completed task 1");
            var styled = await Render(preview, snapshot: true);
            var plain = TerminalText.Plain(styled);
            Equal(true, string.Join('\n', plain.Split('\n').Select(line => line.Trim()))
                .Contains(expected, StringComparison.Ordinal), $"{page} example at {columns} columns");
            Equal(true, plain.Split('\n').All(line => TerminalText.Width(line) <= columns), $"{page} snapshot width");
            if (page == "Text")
                Equal(true, styled.Contains("\u001b[38;2;184;161;255m", StringComparison.Ordinal), "hex styling is rendered");
            var live = TerminalText.Plain(await Render(preview));
            Equal(true, live.Split('\n').Skip(preview.Tasks.Count).Count() <= 23, $"{page} reserves terminal cursor row");
            Equal(true, live.Contains("Esc / Ctrl+C exit", StringComparison.Ordinal), $"{page} help remains visible");
            if (page == "Chat") Equal(true, live.Contains("Ask Blazor.Ink", StringComparison.Ordinal), "composer remains visible");
        }
        foreach (var snapshot in new[] { false, true })
            Equal(true, TerminalText.Plain(await Render(new(20, 24, "Runtime"), snapshot))
                .Contains("[Runtime]", StringComparison.Ordinal), "wrapped navigation keeps the selected page visible");

        var chat = new ShowcaseState(40, 16);
        var chatOutput = await RunScript(chat, "/add demo.cs\rhello\r\u0014\u0003");
        Equal(1, chat.Messages.Count, "coalesced native text and Enter submits");
        Equal("hello", chat.Messages[0], "scripted input uses the real composer");
        Equal("demo.cs", chat.Documents.Single(), "scripted attachment command");
        Equal(false, chat.Thinking, "coalesced Ctrl+T toggles thinking");
        Equal(true, chat.InputAvailable && !chat.RawModeSupported, "scripted input is available without native modes");
        Equal(false, chatOutput.Stdout.Contains('\u001b'), "redirected session output has no ANSI");
        Equal(true, chatOutput.Stdout.Contains("❯ Ask Blazor.Ink", StringComparison.Ordinal), "short terminal retains composer");
        Equal(true, chatOutput.Stdout.Contains("Esc / Ctrl+C exit", StringComparison.Ordinal), "short terminal retains help");

        var pasteState = new ShowcaseState();
        await RunScript(pasteState, "prefix \u001b[200~a\r\nb\tc\u0003\u001b]52;c;blocked\u0007\u001b[201~\u0003");
        Equal("prefix a b c", pasteState.Draft, "bracketed paste is literal sanitized draft text");
        Equal(0, pasteState.Messages.Count, "pasted Enter does not submit");
        var unicode = new ShowcaseState();
        await RunScript(unicode, "x👩‍💻é\u007f\u007f\u0003");
        Equal("x", unicode.Draft, "real input deletes complete Unicode graphemes");

        var tasks = new ShowcaseState(80, 24, "Static");
        var taskOutput = await RunScript(tasks, "\r\u000e\u0010\r\u000e\u000e\u000e\u000eofrce\u0003");
        Equal(2, tasks.Tasks.Count, "tasks append across page changes");
        Equal(1, taskOutput.Stdout.Split("✓ Completed task 1", StringSplitOptions.None).Length - 1, "first Static item printed once");
        Equal(1, taskOutput.Stdout.Split("✓ Completed task 2", StringSplitOptions.None).Length - 1, "second Static item printed once");
        Equal(true, taskOutput.Stdout.Contains("Demo stdout", StringComparison.Ordinal), "stdout action uses session writer");
        Equal("Demo stderr\n", taskOutput.Stderr, "stderr action uses session writer");
        Equal(5, tasks.Updates, "all runtime actions execute from a coalesced read");
        Equal("Runtime", tasks.Page, "real input navigation reaches Runtime");

        var scrolled = new ShowcaseState(40, 16, "Text");
        await RunScript(scrolled, "\u001b[B\u001b[B\u0003");
        Equal(6, scrolled.Scroll, "real arrow keys scroll long gallery");
        Equal(true, scrolled.MaxScroll >= scrolled.Scroll, "scroll is bounded by measured content");
        var clipped = TerminalText.Plain(await Render(scrolled));
        Equal(false, clipped.Contains("Bold ·", StringComparison.Ordinal), "scroll offset changes visible content");
        Equal(true, clipped.Contains("Esc / Ctrl+C exit", StringComparison.Ordinal), "scroll does not move help");

        var options = CommandLine.Parse(["--page", "text", "--columns", "40", "--rows", "16", "--snapshot", "--all"]);
        Equal("Text", options.Page, "CLI accepts case-insensitive page names");
        Equal<int?>(40, options.Columns, "CLI width");
        Equal<int?>(16, options.Rows, "CLI height");
        Equal(true, options.Snapshot && options.All && !options.Scripted, "CLI snapshot mode");
        Equal(true, CommandLine.Parse(["--scripted"]).Scripted, "CLI scripted mode");
        Equal(true, CommandLine.Parse(["--check"]).Check, "CLI check mode");
        Equal(true, CommandLine.Parse(["--help"]).Help, "CLI help mode");
        foreach (var invalid in new string[][] {
            ["--page", "missing"], ["--page"], ["--unknown"], ["--all"],
            ["--columns", "0"], ["--columns", "not-a-number"], ["--rows", "1"],
            ["--columns", "16385"], ["--columns", "4096", "--rows", "4096"],
            ["--snapshot", "--scripted"], ["--check", "--snapshot"]
        })
        {
            try { CommandLine.Parse(invalid); }
            catch (ArgumentException) { count++; continue; }
            throw new Exception($"CLI accepted invalid arguments: {string.Join(" ", invalid)}");
        }
        var inputFailure = false;
        try { await RunScript(new(), "\u001b[200~unterminated"); }
        catch (IOException) { inputFailure = true; }
        Equal(true, inputFailure, "truncated paste fails observably");

        using var child = new Process
        {
            StartInfo = new ProcessStartInfo(Environment.ProcessPath!)
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true }
        };
        child.StartInfo.ArgumentList.Add(typeof(Checks).Assembly.Location);
        child.StartInfo.ArgumentList.Add("--page");
        child.StartInfo.ArgumentList.Add("\u001b]52;c;blocked\u0007missing");
        child.Start();
        var diagnostic = await child.StandardError.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Equal(1, child.ExitCode, "invalid CLI exits nonzero");
        Equal(false, diagnostic.Contains('\u001b'), "CLI diagnostics cannot inject terminal controls");
        Equal(true, diagnostic.Contains("missing", StringComparison.Ordinal), "sanitized diagnostic retains useful text");
        Console.WriteLine($"PASS {count} showcase checks");
    }

    private static Task<string> Render(ShowcaseState state, bool snapshot = false) =>
        InkHost.RenderToStringAsync<App>(state.Columns, ParameterView.FromDictionary(new Dictionary<string, object?>
            { ["State"] = state, ["Snapshot"] = snapshot }));

    private static async Task<(string Stdout, string Stderr)> RunScript(ShowcaseState state, string script)
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(script));
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (var session = await InkHost.RenderAsync<LiveApp>(new()
        {
            Columns = state.Columns, Rows = state.Rows, Stdin = source, Stdout = stdout, Stderr = stderr,
            Interactive = false, ExitOnCtrlC = false
        }, ParameterView.FromDictionary(new Dictionary<string, object?> { ["State"] = state }),
            cancellationToken: timeout.Token))
            Equal(0, await session.WaitUntilExitAsync().WaitAsync(TimeSpan.FromSeconds(5)), "scripted session exits normally");
        Equal(true, source.CanRead, "session retains caller ownership of input");
        return (stdout.ToString(), stderr.ToString());
    }
}
