namespace Blazor.Ink;

// ponytail: bounded ordered task chain; coalesce live frames only if measured throughput requires it.
internal sealed class TerminalWriter(InkOptions options, bool interactive, CancellationToken cancellation)
{
    private readonly object gate = new();
    private Task tail = Task.CompletedTask;
    private int pending, characters;
    private TerminalFrame live = TerminalFrame.Empty;
    private bool started;
    private (int Columns, int Rows) dimensions;
    public Exception? Error { get; private set; }

    private Task Queue(Func<Task> operation, int size = 0, bool cleanup = false)
    {
        lock (gate)
        {
            if (!cleanup && (pending >= 256 || size > 16_000_000 - characters))
                return Task.FromException(new InvalidOperationException("Terminal output backlog limit exceeded."));
            var previous = tail;
            pending++; characters += size;
            return tail = Run();
            async Task Run()
            {
                // Do not call arbitrary TextWriter implementations under the queue lock.
                await Task.Yield();
                try
                {
                    try { await previous.ConfigureAwait(false); } catch { }
                    if (!cleanup && Error is { } error) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
                    await operation().ConfigureAwait(false);
                }
                catch (Exception error) { Error ??= error; throw; }
                finally { lock (gate) { pending--; characters -= size; } }
            }
        }
    }

    private Task Write(TextWriter sink, string text, CancellationToken token) =>
        text.Length == 0 ? Task.CompletedTask : sink.WriteAsync(text.AsMemory(), token);
    private static string Rows(TerminalFrame frame, bool ansi) =>
        frame.Height == 0 ? "" : (ansi ? frame.Text.Replace("\n", "\r\n") : TerminalText.Plain(frame.Text)) + (ansi ? "\r\n" : "\n");
    private string Erase() => live.Height == 0 ? "" : $"\x1b[{live.Height}A\r\x1b[0J";

    public Task DisplayAsync(TerminalUpdate update) => Queue(async () =>
    {
        var next = update.Live;
        if (interactive && next.Height >= update.Rows)
        {
            var lines = next.Lines.TakeLast(update.Rows - 1).ToArray();
            next = new(string.Join('\n', lines), lines.Length);
        }
        var text = "";
        var resized = started && dimensions != (update.Columns, update.Rows);
        if (!started)
        {
            started = true;
            // No input/cursor query yet: preserve any partial history and establish column zero.
            if (interactive) text = "\r\n" + (options.HideCursor ? "\x1b[?25l" : "");
        }
        dimensions = (update.Columns, update.Rows);
        if (interactive)
        {
            if (resized)
            {
                // ponytail: repaint after reflow; track live-region cursors if visible history must survive.
                text += "\x1b[2J\x1b[H";
                live = TerminalFrame.Empty;
            }
            var appended = string.Concat(update.Static.Select(frame => Rows(frame, true)));
            if (!resized && appended.Length == 0 && next == live) { await Write(options.Stdout, text, cancellation); return; }
            if (!resized && options.IncrementalRendering && appended.Length == 0 && next.Height == live.Height)
            {
                var oldLines = live.Lines;
                var newLines = next.Lines;
                for (var i = 0; i < newLines.Length; i++)
                    if (oldLines[i] != newLines[i])
                    {
                        var distance = next.Height - i;
                        text += $"\x1b[{distance}A\r\x1b[2K{newLines[i]}\x1b[{distance}B\r";
                    }
            }
            else text += Erase() + appended + Rows(next, true);
        }
        else text += string.Concat(update.Static.Select(frame => Rows(frame, false)));
        await Write(options.Stdout, text, cancellation);
        live = next;
    }, update.Live.Text.Length + update.Static.Sum(frame => frame.Text.Length));

    public Task ClearAsync() => Queue(async () =>
    {
        if (interactive) await Write(options.Stdout, Erase(), cancellation);
        live = TerminalFrame.Empty;
    });

    public Task LogAsync(string text, bool stderr)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > 1_000_000) return Task.FromException(new ArgumentOutOfRangeException(nameof(text)));
        var plain = TerminalText.Plain(text);
        return Queue(async () =>
        {
            var sink = stderr ? options.Stderr : options.Stdout;
            // Console stdout/stderr share a terminal even though they are distinct writers.
            var shared = ReferenceEquals(sink, options.Stdout) ||
                ReferenceEquals(options.Stdout, Console.Out) && ReferenceEquals(sink, Console.Error);
            if (interactive && shared) await Write(options.Stdout, Erase(), cancellation);
            var line = plain.EndsWith('\n') ? plain : plain + "\n";
            await Write(sink, interactive ? line.Replace("\n", "\r\n") : line, cancellation);
            if (interactive && shared) await Write(options.Stdout, Rows(live, true), cancellation);
        }, plain.Length);
    }

    public Task FlushAsync() => Queue(() => Flush(cancellation));
    internal Task ControlAsync(string text, bool cleanup) =>
        Queue(async () =>
        {
            var token = cleanup ? CancellationToken.None : cancellation;
            await Write(options.Stdout, text, token);
            await options.Stdout.FlushAsync(token);
        }, text.Length, cleanup);
    private async Task Flush(CancellationToken token)
    {
        await options.Stdout.FlushAsync(token);
        if (!ReferenceEquals(options.Stdout, options.Stderr)) await options.Stderr.FlushAsync(token);
    }

    public Task CloseAsync() => Queue(async () =>
    {
        Exception? error = Error;
        try
        {
            if (interactive && options.HideCursor) await Write(options.Stdout, "\x1b[?25h", CancellationToken.None);
            else if (!interactive && error is null) await Write(options.Stdout, Rows(live, false), CancellationToken.None);
        }
        catch (Exception failure) { error ??= failure; }
        try { await options.Stdout.FlushAsync(CancellationToken.None); } catch (Exception failure) { error ??= failure; }
        if (!ReferenceEquals(options.Stdout, options.Stderr))
            try { await options.Stderr.FlushAsync(CancellationToken.None); } catch (Exception failure) { error ??= failure; }
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }, cleanup: true);
}
