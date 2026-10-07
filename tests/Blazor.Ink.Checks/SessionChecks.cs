using Blazor.Ink;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Blazor.Ink.Checks;

internal static class SessionChecks
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
            try { await action().WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (T) { count++; return; }
            throw new Exception($"{name}: expected {typeof(T).Name}");
        }
        ParameterView P(params (string Name, object? Value)[] args) => ParameterView.FromDictionary(args.ToDictionary(p => p.Name, p => p.Value));

        foreach (var incremental in new[] { false, true })
        {
            var output = new TerminalProbe(12, 6);
            var error = new TerminalProbe(12, 6);
            var options = new InkOptions { Stdout = output, Stderr = error, Columns = 12, Rows = 6,
                Interactive = true, IncrementalRendering = incremental, HideCursor = true };
            await using var session = await InkHost.RenderAsync<StaticCases>(options);
            Equal("\nfirst\nlive", output.VisibleText, "initial Static above live frame");
            Equal(false, output.CursorVisible, "cursor hidden while owned");
            await session.RerenderAsync(P(("Items", new[] { "first", "blank", "last" }), ("Live", "updated")));
            Equal("\nfirst\n\nlast\nupdated", output.VisibleText, "Static append preserves blank row");
            var before = output.Bytes.ToString();
            await session.RerenderAsync(P(("Items", new[] { "first", "blank", "last" }), ("Live", "updated")));
            Equal(before, output.Bytes.ToString(), "unchanged render emits no bytes");
            await session.RerenderAsync(P(("Generation", 1), ("Items", new[] { "remount" }), ("Live", "next")));
            Equal("\nfirst\n\nlast\nremount\nnext", output.VisibleText, "Static remount does not erase history");
            await session.ClearAsync();
            Equal("\nfirst\n\nlast\nremount", output.VisibleText, "clear removes only live output");
            await session.RerenderAsync(P(("ShowStatic", false), ("Live", "returned")));
            Equal("\nfirst\n\nlast\nremount\nreturned", output.VisibleText, "rerender after clear");
            await session.WriteAsync("log\x1b]52;c;blocked\x07");
            Equal("\nfirst\n\nlast\nremount\nlog\nreturned", output.VisibleText, "stdout clears and restores live region");
            await session.WriteErrorAsync("problem");
            Equal("problem", error.VisibleText, "stderr coordinated separately");
            await session.AwaitFlushAsync();
            Equal(true, output.Flushes > 0 && error.Flushes > 0, "flush reaches both sinks");
            await session.ExitAsync(7);
            Equal(7, await session.WaitUntilExitAsync(), "exit result delivered");
            Equal(true, output.CursorVisible, "cursor restored on exit");
            Equal(false, output.WasDisposed || error.WasDisposed, "caller retains stream ownership");
            await session.DisposeAsync();
            Equal(true, output.CursorVisible, "repeated teardown is safe");
            await Throws<ObjectDisposedException>(() => session.RerenderAsync(P(("Live", "late"))), "post-exit updates rejected");
            await using var replacement = await InkHost.RenderAsync<Hello>(options); // ownership released
        }

        var pipe = new TerminalProbe();
        await using (var session = await InkHost.RenderAsync<StaticCases>(new()
            { Stdout = pipe, Stderr = pipe, Interactive = false }))
        {
            Equal("first", pipe.VisibleText, "redirected Static commits immediately");
            await session.RerenderAsync(P(("Live", "final")));
            Equal("first", pipe.VisibleText, "redirected live frames deferred");
        }
        Equal("first\nfinal", pipe.VisibleText, "redirected output contains only final live frame");
        Equal(false, pipe.Bytes.ToString().Contains('\x1b'), "redirected output has no terminal controls");

        var small = new TerminalProbe(4, 3);
        await using (var session = await InkHost.RenderAsync<SplitGrapheme>(new()
            { Stdout = small, Stderr = small, Interactive = true, Columns = 4, Rows = 3, HideCursor = false },
            P(("Parts", new[] { "abcdefghijk" }))))
        {
            Equal("\nefgh\nijk", small.VisibleText, "oversized live frame keeps viewport-safe tail");
            await session.RerenderAsync(P(("Parts", new[] { "👩‍💻x" })));
            Equal("\n👩‍💻x", small.VisibleText, "shrinking redraw and wide glyphs");
        }
        var leased = new TerminalProbe();
        await using (var first = await InkHost.RenderAsync<Hello>(new() { Stdout = leased, Stderr = leased, Interactive = false }))
            await Throws<InvalidOperationException>(async () => await InkHost.RenderAsync<Hello>(
                new() { Stdout = leased, Stderr = leased, Interactive = false }), "competing stream ownership rejected");

        var slow = new GatedProbe();
        var mounting = InkHost.RenderAsync<Hello>(new() { Stdout = slow, Stderr = slow, Interactive = true, HideCursor = false });
        await slow.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Equal(false, mounting.IsCompleted, "mount waits for actual output");
        slow.Release.TrySetResult();
        await using (var session = await mounting)
        {
            slow.ResetGate();
            var render = session.RerenderAsync(ParameterView.Empty);
            // Identical renders do not write; force an external write instead.
            await render;
            var logging = session.WriteAsync("slow log");
            await slow.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var flushing = session.AwaitFlushAsync();
            Equal(false, logging.IsCompleted || flushing.IsCompleted, "flush waits behind slow output");
            slow.Release.TrySetResult();
            await logging;
            await flushing;
        }

        var broken = new FailingProbe();
        await Throws<IOException>(async () => await InkHost.RenderAsync<Hello>(new()
            { Stdout = broken, Stderr = broken, Interactive = true, HideCursor = true }), "broken mount fails after cleanup");
        Equal(true, broken.CursorVisible, "failed mount attempts cursor restoration");
        var recovered = await InkHost.RenderAsync<Hello>(new()
            { Stdout = broken, Stderr = broken, Interactive = true });
        {
            broken.FailNext = true;
            await Throws<IOException>(() => recovered.WriteAsync("fail"), "broken runtime output propagates");
            await Throws<IOException>(async () => await recovered.WaitUntilExitAsync(), "broken output settles exit");
            await Throws<IOException>(async () => await recovered.DisposeAsync(), "failed teardown remains observable");
        }

        using var cancellation = new CancellationTokenSource();
        var waiting = new GatedProbe();
        var cancelledMount = InkHost.RenderAsync<Hello>(new()
            { Stdout = waiting, Stderr = waiting, Interactive = true, HideCursor = false }, cancellationToken: cancellation.Token);
        await waiting.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Throws<OperationCanceledException>(async () => await cancelledMount, "cancellation interrupts cooperative output and cleans up");
        await using var afterCancellation = await InkHost.RenderAsync<Hello>(new()
            { Stdout = waiting, Stderr = waiting, Interactive = false });

        using (var services = new ServiceCollection().AddSingleton<ApplicationService>().BuildServiceProvider())
        {
            var injectedOutput = new TerminalProbe();
            SessionComponent.Disposed = false;
            var injected = await InkHost.RenderAsync<SessionComponent>(new()
                { Stdout = injectedOutput, Stderr = injectedOutput, Interactive = true }, services: services);
            Equal(injected, SessionComponent.MountedSession, "session can be injected into Razor components");
            await Throws<InvalidOperationException>(() => injected.RerenderAsync(P(("Fail", true))), "component failure propagates");
            await Throws<InvalidOperationException>(async () => await injected.WaitUntilExitAsync(), "component failure settles exit");
            await Throws<InvalidOperationException>(async () => await injected.DisposeAsync(), "component failure remains observable");
            Equal(true, SessionComponent.Disposed && injectedOutput.CursorVisible, "component failure disposes and restores cursor");
            Equal(false, services.GetRequiredService<ApplicationService>().Disposed, "session retains caller DI ownership");
        }
        KeyedItem.Disposed.Clear();
        var staticOutput = new TerminalProbe();
        await using (var staticSession = await InkHost.RenderAsync<StaticLifecycle>(new()
            { Stdout = staticOutput, Stderr = staticOutput, Interactive = false }))
            Equal(true, KeyedItem.Disposed.Contains("consumed"), "committed Static item components are released");

        // Exit must not release ownership while an admitted operation still accesses its writer.
        var racedOutput = new TerminalProbe();
        var raced = await InkHost.RenderAsync<GatedParameters>(new()
            { Stdout = racedOutput, Stderr = racedOutput, Interactive = true });
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = raced.RerenderAsync(P(("Gate", barrier.Task)));
        await GatedParameters.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var flushBarrier = raced.AwaitFlushAsync();
        await Task.Delay(100);
        Equal(false, flushBarrier.IsCompleted, "flush includes previously admitted async lifecycle work");
        var exit = raced.ExitAsync();
        await Task.Delay(100);
        Equal(false, exit.IsCompleted, "exit retains ownership until admitted asynchronous render finishes");
        await Throws<InvalidOperationException>(async () => await InkHost.RenderAsync<Hello>(new()
            { Stdout = racedOutput, Stderr = racedOutput, Interactive = false }), "exiting session retains stream lease");
        barrier.TrySetResult();
        await Task.WhenAll(operation, flushBarrier, exit).WaitAsync(TimeSpan.FromSeconds(10));
        Equal(true, racedOutput.CursorVisible, "exit drains admitted render/flush before restoration");
        Equal(true, raced.StoppingToken.IsCancellationRequested, "component lifetime token cancelled on exit");
        await using var nextOwner = await InkHost.RenderAsync<Hello>(new()
            { Stdout = racedOutput, Stderr = racedOutput, Interactive = false });

        await Throws<ArgumentOutOfRangeException>(async () => await InkHost.RenderAsync<Hello>(new() { Rows = 1 }), "viewport needs cursor row");
        await Throws<ArgumentNullException>(async () => await InkHost.RenderAsync<Hello>(new() { Stdout = null! }), "null output rejected");
        var componentExit = new TerminalProbe();
        await using (var selfExiting = await InkHost.RenderAsync<ExitingComponent>(new()
            { Stdout = componentExit, Stderr = componentExit, Interactive = false }))
            Equal(3, await selfExiting.WaitUntilExitAsync(), "component can request exit without awaiting its own disposal");

        var hiddenOutput = new TerminalProbe();
        await using (var hiddenSession = await InkHost.RenderAsync<StaticCases>(new()
            { Stdout = hiddenOutput, Stderr = hiddenOutput }, P(("Hidden", true))))
        {
            Equal("", hiddenOutput.VisibleText, "hidden Static is not committed");
            await hiddenSession.RerenderAsync(P(("Hidden", false)));
            Equal("first", hiddenOutput.VisibleText, "showing Static commits previously hidden items");
            await hiddenSession.RerenderAsync(P(("Items", Array.Empty<string>())));
            await hiddenSession.RerenderAsync(P(("Items", new[] { "after shrink" })));
            Equal("first\nafter shrink", hiddenOutput.VisibleText, "shrinking Static resets append index without erasing history");
            Equal(false, hiddenOutput.Bytes.ToString().Contains('\x1b'), "custom sinks default to plain output");
        }

        var backlog = new GatedProbe();
        var bounded = new TerminalWriter(new() { Stdout = backlog, Stderr = backlog }, false, default);
        var queued = Enumerable.Range(0, 257).Select(i => bounded.LogAsync(i.ToString(), false)).ToArray();
        await backlog.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Throws<InvalidOperationException>(() => queued[^1], "writer backlog has a finite operation limit");
        backlog.Release.TrySetResult();
        await Task.WhenAll(queued.Take(256)).WaitAsync(TimeSpan.FromSeconds(10));
        await bounded.CloseAsync();
        Equal(string.Join('\n', Enumerable.Range(0, 256)) + "\n", backlog.Bytes.ToString(),
            "bounded writer keeps accepted output in exact order");
        await Throws<ArgumentOutOfRangeException>(() => bounded.LogAsync(new string('x', 1_000_001), false),
            "oversized external log rejected before parsing");
        var lifecycleOutput = new TerminalProbe();
        await using (var lifecycleFlush = await InkHost.RenderAsync<FlushingComponent>(new()
            { Stdout = lifecycleOutput, Stderr = lifecycleOutput, Interactive = false }).WaitAsync(TimeSpan.FromSeconds(10)))
            Equal("initialization log", lifecycleOutput.VisibleText, "component flush does not wait for its own lifecycle completion");
        var reviewFailures = new List<Exception>();
        async Task ReviewCheck(Func<Task> check)
        {
            try { await check(); }
            catch (Exception error) { reviewFailures.Add(error); }
        }
        await ReviewCheck(async () =>
        {
            var sink = new TerminalProbe();
            await using var session = await InkHost.RenderAsync<FlushingComponent>(new()
                { Stdout = sink, Stderr = sink, Interactive = false }, P(("OffDispatcher", true)))
                .WaitAsync(TimeSpan.FromSeconds(3));
            Equal("initialization log", sink.VisibleText, "off-dispatcher lifecycle flush preserves origin");
        });
        await ReviewCheck(async () =>
        {
            var sink = new TerminalProbe();
            await using var session = await InkHost.RenderAsync<StaticCases>(new()
                { Stdout = sink, Stderr = sink, Interactive = false }, P(("StaticHidden", true)));
            Equal("", sink.VisibleText, "own hidden Static style emits nothing");
            await session.RerenderAsync(P(("StaticHidden", false)));
            Equal("first", sink.VisibleText, "own hidden Static style does not consume items");
        });
        await ReviewCheck(async () =>
        {
            var sink = new GatedProbe { GateNext = false };
            var session = await InkHost.RenderAsync<Hello>(new()
                { Stdout = sink, Stderr = sink, Interactive = true, HideCursor = false });
            sink.ResetGate();
            var log = session.WriteAsync("blocked");
            await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var barriers = Enumerable.Range(0, 512).Select(_ => session.AwaitFlushAsync()).ToArray();
            var rejected = barriers.Any(task => task.IsFaulted);
            sink.Release.TrySetResult();
            await log;
            foreach (var task in barriers) try { await task; } catch (InvalidOperationException) { }
            try { await session.DisposeAsync(); } catch (InvalidOperationException) { }
            Equal(true, rejected, "waiting host barriers cannot bypass backlog bound");
        });
        await ReviewCheck(async () =>
        {
            var sink = new TerminalProbe(8, 6);
            await sink.WriteAsync("HISTORY\r\npre".AsMemory());
            await using var session = await InkHost.RenderAsync<SplitGrapheme>(new()
                { Stdout = sink, Stderr = sink, Columns = 8, Rows = 6, Interactive = true },
                P(("Parts", new[] { "ABCDEFGH" })));
            Equal("HISTORY\npre\nABCDEFGH", sink.VisibleText, "initial live region preserves partial history");
            await session.RerenderAsync(P(("Parts", new[] { "next" })));
            await session.ClearAsync();
            Equal("HISTORY\npre", sink.VisibleText, "clear leaves no wrapped live residue");
        });
        if (reviewFailures.Count > 0) throw new AggregateException("Review regressions failed", reviewFailures);
        return count;
    }
}
