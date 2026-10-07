namespace Blazor.Ink;

/// <summary>A disposable key/text or paste registration. Change IsActive to suspend delivery and input ownership.</summary>
public sealed class InkInputSubscription : IDisposable
{
    private readonly InkInput owner;
    internal readonly Func<InkInputEvent, Task>? Key;
    internal readonly Func<string, Task>? Paste;
    internal bool Active, Disposed;
    internal InkInputSubscription(InkInput owner, Func<InkInputEvent, Task>? key, Func<string, Task>? paste, bool active)
    { this.owner = owner; Key = key; Paste = paste; Active = active; }
    public bool IsActive { get => owner.IsActive(this); set => owner.Change(this, value, false); }
    public void Dispose() => owner.Change(this, false, true);
}

/// <summary>Session-owned input. Callbacks are serialized on the component dispatcher; streams remain caller-owned.</summary>
public sealed class InkInput
{
    private static readonly object ownership = new();
    private static readonly HashSet<Stream> streams = new(ReferenceEqualityComparer.Instance);
    private static bool nativeOwned;
    private readonly object gate = new();
    private readonly List<InkInputSubscription> subscriptions = [];
    private readonly InkOptions options;
    private readonly Func<NativeInputDriver> captureNative;
    private readonly Func<Func<Task>, Task> dispatch;
    private readonly Func<string, bool, Task> control;
    private readonly Action<Exception> fault;
    private readonly Action exit;
    private Task transitions = Task.CompletedTask;
    private Task? reader;
    private CancellationTokenSource? reading;
    private Task? cancelling;
    private NativeInputDriver? native;
    private bool closed, leased, pasteEnabled, dirty, reconciling;

    internal InkInput(InkOptions options, bool interactive, Func<Func<Task>, Task> dispatch,
        Func<string, bool, Task> control, Action<Exception> fault, Action exit, Func<NativeInputDriver>? captureNative = null)
    {
        this.options = options; this.dispatch = dispatch; this.control = control; this.fault = fault; this.exit = exit;
        this.captureNative = captureNative ?? NativeInputDriver.Capture;
        IsRawModeSupported = options.Stdin is null && interactive && (captureNative is not null || NativeInputDriver.IsSupported);
    }
    public bool IsRawModeSupported { get; }
    public bool IsAvailable => options.Stdin is not null || IsRawModeSupported;

    public InkInputSubscription Subscribe(Func<InkInputEvent, Task> callback, bool isActive = true)
    { ArgumentNullException.ThrowIfNull(callback); return Add(callback, null, isActive); }
    public InkInputSubscription SubscribePaste(Func<string, Task> callback, bool isActive = true)
    { ArgumentNullException.ThrowIfNull(callback); return Add(null, callback, isActive); }

    private InkInputSubscription Add(Func<InkInputEvent, Task>? key, Func<string, Task>? paste, bool active)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (active && !IsAvailable) throw new NotSupportedException("Input requires terminal stdin or a caller-supplied byte stream.");
            var subscription = new InkInputSubscription(this, key, paste, active);
            subscriptions.Add(subscription);
            Schedule();
            return subscription;
        }
    }
    internal bool IsActive(InkInputSubscription subscription)
    { lock (gate) return subscription.Active && !subscription.Disposed && !closed; }
    internal void Change(InkInputSubscription subscription, bool active, bool dispose)
    {
        lock (gate)
        {
            if (subscription.Disposed || closed)
            {
                if (dispose) { subscription.Disposed = true; subscriptions.Remove(subscription); return; }
                ObjectDisposedException.ThrowIf(true, subscription);
            }
            if (active && !IsAvailable) throw new NotSupportedException("Raw input is not available.");
            if (active == subscription.Active && !dispose) return;
            subscription.Active = active;
            subscription.Disposed = dispose;
            if (dispose) subscriptions.Remove(subscription);
            if (!subscriptions.Any(s => s.Active)) CancelReader();
            Schedule();
        }
    }
    private Task CancelReader()
    {
        lock (gate)
            // Custom streams may register arbitrary code: never execute it under our subscription lock.
            return reading is null ? Task.CompletedTask : cancelling ??= reading.CancelAsync();
    }
    private void Schedule()
    {
        dirty = true;
        if (reconciling) return;
        reconciling = true;
        var previous = transitions;
        transitions = Task.Run(async () =>
        {
            try
            {
                await previous.ConfigureAwait(false);
                while (true)
                {
                    lock (gate) dirty = false;
                    await ReconcileAsync().ConfigureAwait(false);
                    lock (gate)
                    {
                        if (dirty) continue;
                        reconciling = false;
                        return;
                    }
                }
            }
            catch (Exception error)
            {
                lock (gate) reconciling = false;
                fault(error);
                throw;
            }
        });
        _ = transitions.ContinueWith(task => _ = task.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private async Task ReconcileAsync()
    {
        bool active;
        lock (gate) active = !closed && subscriptions.Any(s => s.Active);
        if (reading is { IsCancellationRequested: true } || !active)
        {
            await StopReaderAsync().ConfigureAwait(false);
            if (!active) return;
        }
        if (reading is not null) return;
        lock (ownership)
        {
            if (options.Stdin is { } stream)
            {
                if (!streams.Add(stream)) throw new InvalidOperationException("An input stream is already owned by another Ink session.");
            }
            else
            {
                if (nativeOwned) throw new InvalidOperationException("Native terminal input is already owned by another Ink session.");
                nativeOwned = true;
            }
            leased = true;
        }
        try
        {
            if (options.Stdin is null)
            {
                native = captureNative();
                // Retain the snapshot before changing OS settings, including when setup/rollback both fail.
                native.EnableRawMode();
                // Mark before admission: cleanup must balance even a partially failed mode write.
                pasteEnabled = true;
                await control("\x1b[?2004h", false).ConfigureAwait(false);
            }
            lock (gate)
            {
                reading = new CancellationTokenSource();
                if (closed || !subscriptions.Any(s => s.Active)) CancelReader();
                var token = reading.Token;
                reader = Task.Run(() => PumpAsync(token));
            }
        }
        catch
        {
            try { await StopReaderAsync().ConfigureAwait(false); } catch { /* Preserve the setup failure and retained native lease. */ }
            throw;
        }
    }

    private async Task PumpAsync(CancellationToken token)
    {
        var parser = new InputParser();
        var buffer = new byte[4096];
        Task<int>? read = null;
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                read ??= options.Stdin is { } stream
                    ? stream.ReadAsync(buffer.AsMemory(), token).AsTask() : native!.ReadAsync(buffer, token);
                if (parser.HasPendingEscape)
                {
                    if (await Task.WhenAny(read, Task.Delay(20, token)).ConfigureAwait(false) != read)
                    {
                        token.ThrowIfCancellationRequested();
                        if (parser.FlushEscape() is { } escape) await DeliverAsync(escape, token).ConfigureAwait(false);
                        continue;
                    }
                }
                var count = await read.ConfigureAwait(false);
                read = null;
                foreach (var input in parser.Push(buffer.AsSpan(0, count), complete: count == 0))
                    await DeliverAsync(input, token).ConfigureAwait(false);
                if (count == 0)
                {
                    if (parser.FlushEscape() is { } escape) await DeliverAsync(escape, token).ConfigureAwait(false);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { fault(error); }
        finally
        {
            // A timed Escape can be delivered while a read is pending. Never release its source early.
            if (read is not null)
                try { await read.ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception error) { fault(error); }
        }
    }

    private Task DeliverAsync(InkInputEvent input, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return dispatch(async () =>
        {
            if (token.IsCancellationRequested) return;
            if (!input.IsPaste && options.ExitOnCtrlC && input.Key is { Ctrl: true, Name: "c" })
            { exit(); return; }
            InkInputSubscription[] targets;
            lock (gate)
            {
                var paste = input.IsPaste && subscriptions.Any(s => s.Active && s.Paste is not null);
                targets = subscriptions.Where(s => s.Active && (paste ? s.Paste is not null : s.Key is not null)).ToArray();
            }
            foreach (var target in targets)
            {
                if (!IsActive(target)) continue;
                if (target.Paste is { } paste) await paste(input.Text);
                else await target.Key!(input);
            }
        });
    }

    internal Task AwaitTransitionsAsync() { lock (gate) return transitions; }
    internal void BeginStop()
    {
        lock (gate) { closed = true; CancelReader(); }
    }
    internal async Task CloseAsync()
    {
        BeginStop();
        Exception? error = null;
        try { await AwaitTransitionsAsync().ConfigureAwait(false); } catch (Exception failure) { error = failure; }
        try { await StopReaderAsync().ConfigureAwait(false); } catch (Exception failure) { error ??= failure; }
        lock (gate) { foreach (var s in subscriptions) s.Disposed = true; subscriptions.Clear(); }
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    private async Task StopReaderAsync()
    {
        Exception? error = null;
        try { await CancelReader().ConfigureAwait(false); } catch (Exception failure) { error = failure; }
        if (reader is not null)
            try { await reader.ConfigureAwait(false); } catch (Exception failure) { error ??= failure; }
        reader = null;
        lock (gate) { reading?.Dispose(); reading = null; cancelling = null; }
        if (pasteEnabled)
        {
            pasteEnabled = false;
            try { await control("\x1b[?2004l", true).ConfigureAwait(false); } catch (Exception failure) { error ??= failure; }
        }
        // Do not release a native lease if exact OS restoration failed.
        native?.Dispose();
        native = null;
        if (leased)
        {
            lock (ownership)
            {
                if (options.Stdin is { } stream) streams.Remove(stream);
                else nativeOwned = false;
                leased = false;
            }
        }
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
