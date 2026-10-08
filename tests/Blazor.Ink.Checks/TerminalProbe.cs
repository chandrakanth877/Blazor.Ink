using System.Text;
using Blazor.Ink;

namespace Blazor.Ink.Checks;

// Output oracle for the subset emitted by this preview; not a general terminal emulator.
internal class TerminalProbe(int columns = 80, int rows = 24) : TextWriter
{
    public override Encoding Encoding => Encoding.UTF8;
    public StringBuilder Bytes { get; } = new();
    public int Flushes { get; private set; }
    public bool WasDisposed { get; private set; }
    public bool CursorVisible { get; private set; } = true;
    private readonly List<string?[]> screen = Enumerable.Range(0, rows).Select(_ => new string?[columns]).ToList();
    private readonly List<string> scrollback = [];
    private int x, y;
    private bool wrapPending;
    public string ScreenText => string.Join('\n', screen.Select(Line)).TrimEnd('\n');
    public string VisibleText => string.Join('\n', scrollback.Concat(screen.Select(Line)).Reverse()
        .SkipWhile(line => line == "").Reverse());
    private static string Line(string?[] cells) => string.Concat(cells.Select(cell => cell ?? " ")).TrimEnd(' ');

    public void Resize(int newColumns, int newRows)
    {
        // This oracle crops cells; PTY checks separately exercise real terminal resize notifications.
        while (screen.Count > newRows)
        {
            scrollback.Add(Line(screen[0]));
            screen.RemoveAt(0);
            y = Math.Max(0, y - 1);
        }
        for (var row = 0; row < screen.Count; row++)
        {
            var cells = screen[row];
            Array.Resize(ref cells, newColumns);
            screen[row] = cells;
        }
        while (screen.Count < newRows) screen.Add(new string?[newColumns]);
        columns = newColumns;
        rows = newRows;
        x = Math.Min(x, columns - 1);
        y = Math.Min(y, rows - 1);
        wrapPending = false;
    }

    public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = buffer.ToString();
        Bytes.Append(text);
        for (var i = 0; i < text.Length;)
        {
            if (text[i] == '\x1b')
            {
                if (++i >= text.Length || text[i++] != '[') throw new Exception("Oracle received unexpected escape.");
                var start = i;
                while (i < text.Length && text[i] is >= '0' and <= '?') i++;
                if (i == text.Length) throw new Exception("Oracle received fragmented CSI.");
                var parameters = text[start..i];
                var command = text[i++];
                if (command == 'm') continue;
                if (parameters == "?25" && command is 'h' or 'l') { CursorVisible = command == 'h'; continue; }
                var value = parameters == "" ? 1 : int.Parse(parameters);
                switch (command)
                {
                    case 'A': y = Math.Max(0, y - value); break;
                    case 'B': y = Math.Min(rows - 1, y + value); break;
                    case 'H' when parameters == "": x = y = 0; break;
                    case 'K' when value == 2: Array.Clear(screen[y]); break;
                    case 'J' when value == 2:
                        foreach (var row in screen) Array.Clear(row);
                        break;
                    case 'J' when value == 0:
                        Array.Clear(screen[y], x, columns - x);
                        for (var row = y + 1; row < rows; row++) Array.Clear(screen[row]);
                        break;
                    default: throw new Exception($"Oracle received unsupported CSI {parameters}{command}.");
                }
                wrapPending = false;
            }
            else if (text[i] == '\r') { x = 0; wrapPending = false; i++; }
            // Plain redirected output uses LF line separators; interactive writes supply CRLF.
            else if (text[i] == '\n') { x = 0; Feed(); wrapPending = false; i++; }
            else
            {
                var start = i;
                while (i < text.Length && text[i] is not ('\x1b' or '\r' or '\n')) i++;
                foreach (var glyph in TerminalText.Parse(text[start..i]))
                {
                    if (wrapPending || x + glyph.Width > columns) { x = 0; Feed(); wrapPending = false; }
                    if (glyph.Width == 0) continue;
                    if (x > 0 && screen[y][x] == "") screen[y][x - 1] = " ";
                    if (x + 1 < columns && screen[y][x + 1] == "") screen[y][x + 1] = " ";
                    screen[y][x] = glyph.Value;
                    if (glyph.Width == 2) screen[y][x + 1] = "";
                    x += glyph.Width;
                    wrapPending = x == columns;
                }
            }
        }
        return Task.CompletedTask;
    }
    private void Feed()
    {
        if (++y < rows) return;
        scrollback.Add(Line(screen[0]));
        screen.RemoveAt(0);
        screen.Add(new string?[columns]);
        y = rows - 1;
    }
    public override Task FlushAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); Flushes++; return Task.CompletedTask; }
    protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
}

internal sealed class GatedProbe(int columns = 80, int rows = 24) : TerminalProbe(columns, rows)
{
    public TaskCompletionSource Entered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool GateNext { get; set; } = true;
    public void ResetGate()
    {
        Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        GateNext = true;
    }
    public override async Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        if (GateNext)
        {
            GateNext = false;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
        await base.WriteAsync(buffer, cancellationToken);
    }
}

internal sealed class FailingProbe : TerminalProbe
{
    public bool FailNext { get; set; } = true;
    public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        if (FailNext) { FailNext = false; return Task.FromException(new IOException("scripted output failure")); }
        return base.WriteAsync(buffer, cancellationToken);
    }
}
