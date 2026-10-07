using System.Text;
using System.Threading.Channels;
using Blazor.Ink;

namespace Blazor.Ink.Checks;

internal static class InputSessionChecks
{
    public static async Task<int> RunAsync()
    {
        var count = 0;
        void Equal<T>(T expected, T actual, string name)
        {
            if (!Equals(expected, actual)) throw new Exception($"{name}: expected [{expected}], got [{actual}]");
            count++;
        }
        async Task Throws<T>(Func<Task> action, string name) where T : Exception
        {
            try { await action().WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (T) { count++; return; }
            throw new Exception($"{name}: expected {typeof(T).Name}");
        }
        var source = new PacketStream();
        var sink = new StringWriter();
        var session = await InkHost.RenderAsync<Hello>(new() { Stdin = source, Stdout = sink, Stderr = sink });
        Equal(0, source.Reads, "output-only session never reads supplied stdin");
        Equal(false, session.Input.IsRawModeSupported, "custom streams do not use native modes");
        var received = new List<InkInputEvent>();
        var changed = new SemaphoreSlim(0);
        using var first = session.Input.Subscribe(async input =>
        {
            await Task.Delay(1).ConfigureAwait(false);
            await session.AwaitFlushAsync().ConfigureAwait(false);
            received.Add(input);
            changed.Release();
        }, isActive: false);
        await session.AwaitFlushAsync();
        Equal(0, source.Reads, "inactive subscription does not start reads");
        first.IsActive = true;
        await session.AwaitFlushAsync();
        var broadcasts = new List<string>();
        using var second = session.Input.Subscribe(input =>
        {
            broadcasts.Add(input.Text);
            return Task.CompletedTask;
        });
        await source.Send("hi");
        await changed.WaitAsync(TimeSpan.FromSeconds(5));
        await session.AwaitFlushAsync();
        Equal("hi", received.Single().Text, "async input callback receives text");
        Equal("hi", broadcasts.Single(), "global input broadcasts in registration order");
        var pastes = new List<string>();
        using var paste = session.Input.SubscribePaste(text =>
        {
            pastes.Add(text);
            changed.Release();
            return Task.CompletedTask;
        });
        const string literal = "p\r\x03\x1b[A";
        await source.Send("\x1b[200~" + literal);
        await Task.Delay(80);
        Equal(0, pastes.Count, "paste survives beyond escape timeout");
        await source.Send("\x1b[201~");
        await changed.WaitAsync(TimeSpan.FromSeconds(5));
        Equal(literal, pastes.Single(), "paste remains literal");
        Equal(1, received.Count, "paste channel bypasses key subscribers");
        paste.IsActive = false;
        await source.Send("\x1b[200~\x03\r\x1b[201~");
        await changed.WaitAsync(TimeSpan.FromSeconds(5));
        Equal(true, received.Last().IsPaste && received.Last().Key is null, "paste fallback retains origin without command metadata");
        Equal(false, session.WaitUntilExitAsync().IsCompleted, "pasted Ctrl+C does not exit");
        await source.Send("\x1b");
        await changed.WaitAsync(TimeSpan.FromSeconds(5));
        Equal("escape", received.Last().Key!.Value.Name, "20ms runtime Escape timer");
        first.IsActive = false;
        second.IsActive = false;
        await session.AwaitFlushAsync();
        var reads = source.Reads;
        await Task.Delay(50);
        Equal(reads, source.Reads, "last deactivation stops reading");
        first.IsActive = true;
        await session.AwaitFlushAsync();
        await source.Send("again");
        await changed.WaitAsync(TimeSpan.FromSeconds(5));
        Equal("again", received.Last().Text, "reactivation reacquires source");
        await source.Send("\x03");
        Equal(0, await session.WaitUntilExitAsync().WaitAsync(TimeSpan.FromSeconds(5)), "typed Ctrl+C exits normally");
        await session.DisposeAsync();
        Equal(false, source.WasDisposed, "stdin remains caller-owned");
        Equal(false, sink.ToString().Contains("\x1b[?2004", StringComparison.Ordinal), "custom streams never negotiate paste");
        await Throws<ObjectDisposedException>(() => { session.Input.Subscribe(_ => Task.CompletedTask); return Task.CompletedTask; },
            "subscriptions rejected after exit");

        var failingSource = new PacketStream();
        using var failureSink = new StringWriter();
        var failing = await InkHost.RenderAsync<Hello>(new() { Stdin = failingSource, Stdout = failureSink, Stderr = failureSink });
        using var bad = failing.Input.Subscribe(_ => throw new IOException("callback failure"));
        await failing.AwaitFlushAsync();
        await failingSource.Send("fail");
        await Throws<IOException>(async () => await failing.WaitUntilExitAsync(), "callback failure settles exit");
        await Throws<IOException>(async () => await failing.DisposeAsync(), "callback failure remains observable");
        Equal(false, failingSource.WasDisposed, "failure does not close caller stdin");

        var shared = new PacketStream();
        using var aSink = new StringWriter();
        using var bSink = new StringWriter();
        await using var a = await InkHost.RenderAsync<Hello>(new() { Stdin = shared, Stdout = aSink, Stderr = aSink });
        var b = await InkHost.RenderAsync<Hello>(new() { Stdin = shared, Stdout = bSink, Stderr = bSink });
        using var aSubscription = a.Input.Subscribe(_ => Task.CompletedTask);
        await a.AwaitFlushAsync();
        using var bSubscription = b.Input.Subscribe(_ => Task.CompletedTask);
        await Throws<InvalidOperationException>(() => b.AwaitFlushAsync(), "competing input ownership fails visibly");
        await Throws<InvalidOperationException>(async () => await b.DisposeAsync(), "competing owner cleans up");
        aSubscription.IsActive = false;
        await a.AwaitFlushAsync();
        using var cSink = new StringWriter();
        await using var c = await InkHost.RenderAsync<Hello>(new() { Stdin = shared, Stdout = cSink, Stderr = cSink });
        using var cSubscription = c.Input.Subscribe(_ => Task.CompletedTask);
        await c.AwaitFlushAsync();
        Equal(true, shared.Reads > 0, "released input source can be reacquired");

        var quitSource = new PacketStream();
        using var quitSink = new StringWriter();
        var quit = await InkHost.RenderAsync<Hello>(new() { Stdin = quitSource, Stdout = quitSink, Stderr = quitSink, ExitOnCtrlC = false });
        using var quitSubscription = quit.Input.Subscribe(input =>
        {
            if (input.Key is { Ctrl: true, Name: "c" }) quit.RequestExit(9);
            return Task.CompletedTask;
        });
        await quit.AwaitFlushAsync();
        await quitSource.Send("\x03");
        Equal(9, await quit.WaitUntilExitAsync().WaitAsync(TimeSpan.FromSeconds(5)), "callback requests exit without awaiting itself");
        await quit.DisposeAsync();

        var overflowSource = new PacketStream();
        using var overflowSink = new StringWriter();
        var overflow = await InkHost.RenderAsync<Hello>(new() { Stdin = overflowSource, Stdout = overflowSink, Stderr = overflowSink });
        using var overflowSubscription = overflow.Input.Subscribe(_ => Task.CompletedTask);
        await overflow.AwaitFlushAsync();
        await overflowSource.Send("\x1b[200~" + new string('x', 1_000_001));
        await Throws<InvalidOperationException>(async () => await overflow.WaitUntilExitAsync(), "pending paste overflow stops session");
        await Throws<InvalidOperationException>(async () => await overflow.DisposeAsync(), "overflow cleanup remains observable");

        using var cancellation = new CancellationTokenSource();
        var cancelSource = new PacketStream();
        using var cancelSink = new StringWriter();
        var cancelled = await InkHost.RenderAsync<Hello>(new() { Stdin = cancelSource, Stdout = cancelSink, Stderr = cancelSink },
            cancellationToken: cancellation.Token);
        using var cancelSubscription = cancelled.Input.Subscribe(_ => Task.CompletedTask);
        await cancelled.AwaitFlushAsync();
        cancellation.Cancel();
        await Throws<OperationCanceledException>(async () => await cancelled.WaitUntilExitAsync(), "cancellation interrupts blocked input");
        await Throws<OperationCanceledException>(async () => await cancelled.DisposeAsync(), "cancelled input teardown remains observable");
        Equal(false, cancelSource.WasDisposed, "cancellation retains caller stdin ownership");

        var selfSource = new PacketStream();
        using var selfSink = new StringWriter();
        await using var self = await InkHost.RenderAsync<Hello>(new() { Stdin = selfSource, Stdout = selfSink, Stderr = selfSink });
        var selfFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        InkInputSubscription? selfSubscription = null;
        selfSubscription = self.Input.Subscribe(async _ =>
        {
            selfSubscription!.Dispose();
            await Task.Delay(1).ConfigureAwait(false);
            await self.AwaitFlushAsync();
            selfFinished.TrySetResult();
        });
        await self.AwaitFlushAsync();
        await selfSource.Send("off");
        await selfFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await self.AwaitFlushAsync();
        Equal(false, selfSubscription.IsActive, "callback can release itself and flush without a mode-transition cycle");

        var toggledSource = new PacketStream();
        using var toggledSink = new StringWriter();
        await using var toggled = await InkHost.RenderAsync<Hello>(new() { Stdin = toggledSource, Stdout = toggledSink, Stderr = toggledSink });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var toggledSubscription = toggled.Input.Subscribe(async _ => { entered.TrySetResult(); await release.Task; });
        await toggled.AwaitFlushAsync();
        await toggledSource.Send("hold");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; i++) toggledSubscription.IsActive = (i & 1) != 0;
        var allocation = GC.GetAllocatedBytesForCurrentThread() - allocated;
        release.TrySetResult();
        await toggled.AwaitFlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Equal(true, allocation < 1_000_000, "mode toggles coalesce rather than allocate an unbounded task chain");

        using var truncatedSource = new MemoryStream(Encoding.UTF8.GetBytes("\x1b[200~unfinished"));
        using var truncatedSink = new StringWriter();
        var truncated = await InkHost.RenderAsync<Hello>(new() { Stdin = truncatedSource, Stdout = truncatedSink, Stderr = truncatedSink });
        using var truncatedSubscription = truncated.Input.Subscribe(_ => Task.CompletedTask);
        await Throws<IOException>(async () => await truncated.WaitUntilExitAsync(), "EOF during paste is observable rather than silently dropped");
        await Throws<IOException>(async () => await truncated.DisposeAsync(), "truncated paste teardown remains observable");

        var bufferedSource = new PacketStream();
        using var bufferedSink = new StringWriter();
        var buffered = await InkHost.RenderAsync<Hello>(new() { Stdin = bufferedSource, Stdout = bufferedSink, Stderr = bufferedSink });
        using var bufferedSubscription = buffered.Input.Subscribe(_ => Task.CompletedTask);
        using var stopRelease = new ManualResetEventSlim();
        var stoppingEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stoppingRegistration = buffered.StoppingToken.Register(() =>
        {
            stoppingEntered.TrySetResult();
            stopRelease.Wait(TimeSpan.FromSeconds(5));
        });
        await buffered.AwaitFlushAsync();
        await bufferedSource.Send("\u0003x\x1b[A");
        try
        {
            await stoppingEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(50);
        }
        finally { stopRelease.Set(); }
        Equal(0, await buffered.WaitUntilExitAsync().WaitAsync(TimeSpan.FromSeconds(5)), "buffered keys after Ctrl+C do not turn normal exit into a disposal failure");
        await buffered.DisposeAsync();

        var reentrantSource = new PacketStream();
        using var reentrantSink = new StringWriter();
        await using var reentrant = await InkHost.RenderAsync<Hello>(new() { Stdin = reentrantSource, Stdout = reentrantSink, Stderr = reentrantSink });
        using var reentrantSubscription = reentrant.Input.Subscribe(_ => Task.CompletedTask);
        var cancellationUnlocked = false;
        reentrantSource.Cancelling = () =>
            cancellationUnlocked = Task.Run(() => reentrantSubscription.IsActive).Wait(TimeSpan.FromSeconds(1));
        await reentrant.AwaitFlushAsync();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (reentrantSource.Reads == 0 && DateTime.UtcNow < deadline) await Task.Delay(1);
        Equal(true, reentrantSource.Reads > 0, "cancellation probe has entered a read");
        reentrantSubscription.IsActive = false;
        await reentrant.AwaitFlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Equal(true, cancellationUnlocked, "caller cancellation callbacks run outside subscription locks");

        var throwingSource = new PacketStream { Cancelling = () => throw new IOException("stream cancellation failure") };
        using var throwingSink = new StringWriter();
        var throwing = await InkHost.RenderAsync<Hello>(new() { Stdin = throwingSource, Stdout = throwingSink, Stderr = throwingSink });
        using var throwingSubscription = throwing.Input.Subscribe(_ => Task.CompletedTask);
        await throwing.AwaitFlushAsync();
        deadline = DateTime.UtcNow.AddSeconds(5);
        while (throwingSource.Reads == 0 && DateTime.UtcNow < deadline) await Task.Delay(1);
        Equal(true, throwingSource.Reads > 0, "throwing cancellation probe has entered a read");
        throwingSubscription.Dispose();
        await Throws<AggregateException>(async () => await throwing.WaitUntilExitAsync(), "cancellation callback failure settles session");
        await Throws<AggregateException>(async () => await throwing.DisposeAsync(), "cancellation callback failure remains observable after cleanup");
        throwingSource.Cancelling = null;
        using var reacquiredSink = new StringWriter();
        await using var reacquired = await InkHost.RenderAsync<Hello>(new() { Stdin = throwingSource, Stdout = reacquiredSink, Stderr = reacquiredSink });
        using var reacquiredSubscription = reacquired.Input.Subscribe(_ => Task.CompletedTask);
        await reacquired.AwaitFlushAsync();
        Equal(true, reacquiredSubscription.IsActive, "throwing cancellation does not strand the input lease");

        var queuedSource = new PacketStream();
        var queued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var staleExit = false;
        var queuedInput = new InkInput(new() { Stdin = queuedSource }, false,
            async callback => { queued.TrySetResult(); await allowDispatch.Task; await callback(); },
            (_, _) => Task.CompletedTask, error => throw new Exception("Unexpected queued input failure", error), () => staleExit = true);
        using var queuedSubscription = queuedInput.Subscribe(_ => Task.CompletedTask);
        await queuedInput.AwaitTransitionsAsync();
        await queuedSource.Send("\x03");
        await queued.Task.WaitAsync(TimeSpan.FromSeconds(5));
        queuedSubscription.IsActive = false;
        allowDispatch.TrySetResult();
        await queuedInput.AwaitTransitionsAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Equal(false, staleExit, "deactivation cancels queued Ctrl+C before dispatcher execution");
        await queuedInput.CloseAsync();

        var failedDriver = new ProbeNativeDriver { FailSetup = true, FailRestore = true };
        Exception? setupError = null;
        var failedInput = new InkInput(new(), true, callback => callback(), (_, _) => Task.CompletedTask,
            error => setupError = error, () => { }, () => failedDriver);
        using var failedSubscription = failedInput.Subscribe(_ => Task.CompletedTask);
        await Throws<IOException>(() => failedInput.AwaitTransitionsAsync(), "failed native setup is observable");
        Equal("native setup failure", setupError?.Message, "failed rollback preserves the first startup error");
        var competitor = new InkInput(new(), true, callback => callback(), (_, _) => Task.CompletedTask,
            _ => { }, () => { }, () => new ProbeNativeDriver());
        using var competitorSubscription = competitor.Subscribe(_ => Task.CompletedTask);
        await Throws<InvalidOperationException>(() => competitor.AwaitTransitionsAsync(), "failed rollback retains native ownership");
        await Throws<InvalidOperationException>(() => competitor.CloseAsync(), "failed competitor cleanup is observable");
        failedDriver.FailRestore = false;
        await Throws<IOException>(() => failedInput.CloseAsync(), "successful rollback retry preserves original setup failure");
        var recoveredInput = new InkInput(new(), true, callback => callback(), (_, _) => Task.CompletedTask,
            _ => { }, () => { }, () => new ProbeNativeDriver());
        using var recoveredSubscription = recoveredInput.Subscribe(_ => Task.CompletedTask);
        await recoveredInput.AwaitTransitionsAsync();
        await recoveredInput.CloseAsync();
        Equal(true, failedDriver.Restores >= 2, "native restoration is retried before releasing ownership");
        using var protocolSink = new ProtocolWriter();
        var protocolWriter = new TerminalWriter(new() { Stdout = protocolSink, Stderr = protocolSink }, true, CancellationToken.None);
        await protocolWriter.ControlAsync("\x1b[?2004l", true);
        Equal(1, protocolSink.Flushes, "protocol transitions flush before releasing input ownership");
        return count;
    }
}

internal sealed class ProbeNativeDriver : NativeInputDriver
{
    public bool FailSetup, FailRestore;
    public int Restores;
    internal override void EnableRawMode()
    {
        if (FailSetup) throw new IOException("native setup failure");
    }
    public override async Task<int> ReadAsync(byte[] buffer, CancellationToken cancellation)
    {
        await Task.Delay(Timeout.Infinite, cancellation);
        return 0;
    }
    public override void Dispose()
    {
        Restores++;
        if (FailRestore) throw new IOException("native restoration failure");
    }
}

internal sealed class ProtocolWriter : StringWriter
{
    public int Flushes;
    public override Task FlushAsync(CancellationToken cancellationToken) { Flushes++; return Task.CompletedTask; }
}

// Cooperative byte-packet source with no native terminal side effects.
internal sealed class PacketStream : Stream
{
    private readonly Channel<byte[]> packets = Channel.CreateBounded<byte[]>(64);
    private byte[] current = [];
    private int offset;
    public int Reads;
    public bool WasDisposed;
    public Action? Cancelling;
    public Task Send(string text) => packets.Writer.WriteAsync(Encoding.UTF8.GetBytes(text)).AsTask();
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using var registration = Cancelling is { } callback ? cancellationToken.Register(callback) : default;
        Interlocked.Increment(ref Reads);
        if (offset == current.Length) { current = await packets.Reader.ReadAsync(cancellationToken); offset = 0; }
        var count = Math.Min(buffer.Length, current.Length - offset);
        current.AsMemory(offset, count).CopyTo(buffer);
        offset += count;
        return count;
    }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
}
