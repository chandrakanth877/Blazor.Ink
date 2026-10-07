using System.Runtime.InteropServices;
using Blazor.Ink;

namespace Blazor.Ink.Checks;

internal static class NativeInputChecks
{
    [DllImport("libc", SetLastError = true)] private static extern int tcgetattr(int fd, [Out] byte[] state);
    [DllImport("libc", SetLastError = true)] private static extern int fcntl(int fd, int command);
    [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int handle);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetConsoleMode(IntPtr handle, uint mode);
    [DllImport("kernel32.dll")] private static extern uint GetConsoleCP();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetConsoleCP(uint codePage);

    internal static string Snapshot()
    {
        if (OperatingSystem.IsWindows())
        {
            if (!GetConsoleMode(GetStdHandle(-10), out var mode)) throw new Exception("Native gate needs console stdin.");
            return $"{mode}:{GetConsoleCP()}";
        }
        var bytes = new byte[OperatingSystem.IsMacOS() ? 72 : 60];
        if (tcgetattr(0, bytes) != 0) throw new Exception("Native gate needs PTY stdin.");
        // macOS automatically sets read-only PENDIN bookkeeping on canonical re-entry.
        // Do not read/drain user input merely to clear it; compare every configurable bit.
        if (OperatingSystem.IsMacOS()) bytes[27] &= 0xdf;
        var flags = fcntl(0, 3); // F_GETFL, independently checked by the native ABI probe.
        if (flags < 0) throw new Exception("Cannot inspect stdin descriptor flags.");
        return Convert.ToHexString(bytes) + ":" + flags;
    }

    public static async Task ChildAsync(string scenario)
    {
        if (OperatingSystem.IsWindows() && scenario == "preconfigured")
        {
            if (!GetConsoleMode(GetStdHandle(-10), out var mode) ||
                !SetConsoleMode(GetStdHandle(-10), mode & ~4u) || !SetConsoleCP(437))
                throw new Exception("Cannot configure prior console input settings.");
        }
        var original = Snapshot();
        using var cancellation = new CancellationTokenSource();
        var options = new InkOptions { Interactive = true, HideCursor = false, Columns = 40, Rows = 8 };
        if (scenario == "startup-failure")
            options = options with { Stdout = new ModeFailingWriter(), Stderr = TextWriter.Null };
        var session = scenario is "mount-input" or "component-failure"
            ? await InkHost.RenderAsync<NativeInputOnMount>(options, cancellationToken: cancellation.Token)
            : await InkHost.RenderAsync<Hello>(options, cancellationToken: cancellation.Token);
        // Compare the state immediately before input activation. The parent also compares the original PTY.
        var before = scenario is "mount-input" or "component-failure" ? NativeInputOnMount.Before : Snapshot();
        if (scenario == "output-only")
        {
            await session.DisposeAsync();
            await session.DisposeAsync();
            if (Snapshot() != original) throw new Exception("Output-only session changed input settings.");
            Console.Out.WriteLine("NATIVE_PASS " + scenario);
            return;
        }
        var pasteReceived = false;
        using var paste = session.Input.SubscribePaste(text =>
        {
            if (text != "p\r\u0003") throw new Exception("Native paste was not literal.");
            pasteReceived = true;
            return Task.CompletedTask;
        });
        using var key = session.Input.Subscribe(input =>
        {
            if (scenario == "callback-failure") throw new IOException("native callback failure");
            if (input.Text == "q")
            {
                if (!pasteReceived) throw new Exception("Native paste channel did not run.");
                session.RequestExit(4);
            }
            return Task.CompletedTask;
        });
        var expectedFailure = scenario is "startup-failure" or "callback-failure" or "component-failure" or "cancel";
        try
        {
            await session.AwaitFlushAsync();
            if (scenario == "component-failure")
                await session.RerenderAsync(Microsoft.AspNetCore.Components.ParameterView.FromDictionary(
                    new Dictionary<string, object?> { ["Fail"] = true }));
            if (scenario == "competing")
            {
                using var sink = new StringWriter();
                var other = await InkHost.RenderAsync<Hello>(options with { Stdout = sink, Stderr = sink });
                using var otherKeys = other.Input.Subscribe(_ => Task.CompletedTask);
                try
                {
                    await other.AwaitFlushAsync();
                    throw new Exception("Competing native input was allowed.");
                }
                catch (InvalidOperationException) { }
                finally
                {
                    try { await other.DisposeAsync(); } catch (InvalidOperationException) { }
                }
            }
            if (scenario == "release")
            {
                key.IsActive = paste.IsActive = false;
                await session.AwaitFlushAsync();
                if (Snapshot() != before) throw new Exception("Last subscriber did not restore native state.");
                key.IsActive = paste.IsActive = true;
                await session.AwaitFlushAsync();
            }
            Console.Out.WriteLine("NATIVE_READY");
            if (scenario == "cancel") cancellation.Cancel();
            await session.WaitUntilExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            if (expectedFailure) throw new Exception("Expected native scenario failure.");
        }
        catch (IOException) when (scenario is "startup-failure" or "callback-failure" or "component-failure") { }
        catch (OperationCanceledException) when (scenario == "cancel") { }
        finally
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try { await session.DisposeAsync(); }
                catch (IOException) when (scenario is "startup-failure" or "callback-failure" or "component-failure") { }
                catch (OperationCanceledException) when (scenario == "cancel") { }
            }
        }
        var after = Snapshot();
        if (after != before) throw new Exception($"Native settings were not exactly restored ({scenario}). Before={before} After={after}");
        Console.Out.WriteLine("NATIVE_PASS " + scenario);
    }

    private sealed class ModeFailingWriter : StringWriter
    {
        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) =>
            buffer.Span.IndexOf("\x1b[?2004h".AsSpan()) >= 0
                ? Task.FromException(new IOException("native mode startup failure"))
                : base.WriteAsync(buffer, cancellationToken);
    }
}

public sealed class NativeInputOnMount : Microsoft.AspNetCore.Components.ComponentBase, IDisposable
{
    [Microsoft.AspNetCore.Components.Inject] public InkInput Input { get; set; } = null!;
    [Microsoft.AspNetCore.Components.Parameter] public bool Fail { get; set; }
    internal static string Before = "";
    private InkInputSubscription? subscription;
    protected override void OnInitialized()
    {
        Before = NativeInputChecks.Snapshot();
        subscription = Input.Subscribe(_ => Task.CompletedTask);
    }
    protected override void OnParametersSet()
    {
        if (Fail) throw new IOException("native component failure");
    }
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "ink-text");
        builder.AddContent(1, "Native mount");
        builder.CloseElement();
    }
    public void Dispose() => subscription?.Dispose();
}
