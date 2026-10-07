using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.ExceptionServices;

namespace Blazor.Ink;

public sealed record InkOptions
{
    public TextWriter Stdout { get; init; } = Console.Out;
    public TextWriter Stderr { get; init; } = Console.Error;
    public Stream? Stdin { get; init; }
    public bool ExitOnCtrlC { get; init; } = true;
    public int Columns { get; init; } = 80;
    public int Rows { get; init; } = 24;
    public bool? Interactive { get; init; }
    public bool IncrementalRendering { get; init; }
    public bool HideCursor { get; init; } = true;
}

/// <summary>Owns rendering and serialized output until exit. Does not own caller services or writers.</summary>
public sealed class InkSession : IAsyncDisposable
{
    private static readonly HashSet<TextWriter> owners = new(ReferenceEqualityComparer.Instance);
    private readonly object gate = new();
    private readonly InkOptions options;
    private readonly TerminalWriter writer;
    private readonly TerminalRenderer renderer;
    public InkInput Input { get; }
    private readonly ServiceProvider? ownedServices;
    private readonly TaskCompletionSource<int> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationToken cancellation;
    private readonly CancellationTokenSource stopping = new();
    private readonly List<Task> operations = [];
    public CancellationToken StoppingToken { get; }
    private CancellationTokenRegistration registration;
    private Task? stop;
    private Exception? failure;

    internal InkSession(InkOptions options, IServiceProvider? services, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(options.Stdout);
        ArgumentNullException.ThrowIfNull(options.Stderr);
        if (options.Stdin is { CanRead: false }) throw new ArgumentException("Stdin must be readable.", nameof(options.Stdin));
        if (options.Columns is < 1 or > Canvas.MaxDimension) throw new ArgumentOutOfRangeException(nameof(options.Columns));
        if (options.Rows is < 2 or > Canvas.MaxDimension) throw new ArgumentOutOfRangeException(nameof(options.Rows));
        cancellation.ThrowIfCancellationRequested();
        this.options = options;
        this.cancellation = cancellation;
        StoppingToken = stopping.Token;
        lock (owners)
        {
            if (owners.Contains(options.Stdout) || owners.Contains(options.Stderr))
                throw new InvalidOperationException("A terminal writer is already owned by another Ink session.");
            owners.Add(options.Stdout); owners.Add(options.Stderr);
        }
        try
        {
            ownedServices = services is null ? new ServiceCollection().BuildServiceProvider() : null;
            var interactive = options.Interactive ?? (ReferenceEquals(options.Stdout, Console.Out) &&
                !Console.IsOutputRedirected && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) &&
                Environment.GetEnvironmentVariable("TERM") != "dumb");
            writer = new(options, interactive, cancellation);
            renderer = new(new SessionServices(this, services ?? ownedServices!), options.Columns, writer.DisplayAsync);
            Input = new(options, interactive, DispatchInputAsync,
                writer.ControlAsync, error => _ = StopAsync(1, error), () => RequestExit());
            renderer.Faulted = error => _ = StopAsync(1, error);
        }
        catch
        {
            ownedServices?.Dispose();
            stopping.Dispose();
            Release();
            throw;
        }
    }

    private sealed class SessionServices(InkSession session, IServiceProvider services) : IServiceProvider
    {
        public object? GetService(Type type) => type == typeof(InkSession) ? session :
            type == typeof(InkInput) ? session.Input : services.GetService(type);
    }

    internal async Task MountAsync<TComponent>(ParameterView parameters) where TComponent : IComponent
    {
        try
        {
            var copied = Copy(parameters);
            Task mounting;
            lock (gate)
            {
                mounting = InitializeAsync();
                operations.Add(mounting);
                registration = cancellation.Register(() => _ = StopAsync(1, new OperationCanceledException(cancellation)));
            }
            await mounting;
            if (stop is { } stopping) await stopping;
            async Task InitializeAsync()
            {
                await renderer.MountAsync<TComponent>(copied);
                ThrowRendererError();
                await Input.AwaitTransitionsAsync();
                await writer.FlushAsync();
            }
        }
        catch (Exception error)
        {
            try { await StopAsync(1, error); } catch { }
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    private static ParameterView Copy(ParameterView parameters) =>
        ParameterView.FromDictionary(parameters.ToDictionary().ToDictionary(pair => pair.Key, pair => pair.Value));
    private void ThrowRendererError()
    {
        if (renderer.Error is { } error) ExceptionDispatchInfo.Capture(error).Throw();
    }
    private Task Invoke(Func<Task> action)
    {
        lock (gate)
        {
            if (stop is not null) return Task.FromException(new ObjectDisposedException(nameof(InkSession)));
            operations.RemoveAll(task => task.IsCompleted);
            if (operations.Count >= 256)
            {
                var error = new InvalidOperationException("Host operation backlog limit exceeded.");
                _ = StopAsync(1, error);
                return Task.FromException(error);
            }
            var operation = Observe(action());
            operations.Add(operation);
            return operation;
        }
    }
    private async Task Observe(Task operation)
    {
        try { await operation; ThrowRendererError(); }
        catch (Exception error)
        {
            _ = StopAsync(1, error);
            throw;
        }
    }

    private Task DispatchInputAsync(Func<Task> callback)
    {
        lock (gate)
            return stop is not null ? Task.CompletedTask : Invoke(() => renderer.InvokeCallbackAsync(callback));
    }

    public Task RerenderAsync(ParameterView parameters)
    {
        var copied = Copy(parameters);
        return Invoke(async () => { await renderer.RerenderAsync(copied); ThrowRendererError(); await writer.FlushAsync(); });
    }
    public Task ClearAsync() => Invoke(writer.ClearAsync);
    public Task WriteAsync(string text) => Invoke(() => writer.LogAsync(text, false));
    public Task WriteErrorAsync(string text) => Invoke(() => writer.LogAsync(text, true));
    public Task AwaitFlushAsync()
    {
        lock (gate)
        {
            // ponytail: dependency snapshots are capped at 256 admissions; share a sequence barrier if that ceiling grows.
            // Lifecycle code cannot wait for the host operation that is awaiting that lifecycle.
            // It can still flush already committed frames/logs through the writer barrier.
            var componentOrigin = renderer.IsLifecycleContext;
            var admitted = componentOrigin ? [] : operations.ToArray();
            return Invoke(async () =>
            {
                await Task.WhenAll(admitted);
                if (!componentOrigin) await Input.AwaitTransitionsAsync();
                await renderer.Dispatcher.InvokeAsync(() => { });
                await writer.FlushAsync();
            });
        }
    }
    public Task<int> WaitUntilExitAsync() => exited.Task;
    /// <summary>Signal exit from component lifecycle/callback code without awaiting its own disposal.</summary>
    public void RequestExit(int code = 0) => _ = StopAsync(code);
    /// <summary>Await complete teardown. Component lifecycle code should use RequestExit instead.</summary>
    public Task ExitAsync(int code = 0) => StopAsync(code);
    public ValueTask DisposeAsync() => new(StopAsync(0));

    private Task StopAsync(int code, Exception? error = null)
    {
        lock (gate)
        {
            failure ??= error;
            // Cleanup must not recursively dispose the renderer from UpdateDisplay/HandleException.
            if (stop is null)
            {
                stop = Task.Run(() => StopCoreAsync(code));
                // Automatic fault/cancellation cleanup can have no caller awaiting the stop task.
                _ = stop.ContinueWith(task => _ = task.Exception, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            return stop;
        }
    }
    private async Task StopCoreAsync(int code)
    {
        try
        {
            Task[] admitted;
            lock (gate) admitted = operations.ToArray();
            try { stopping.Cancel(); } catch (Exception error) { failure ??= error; }
            try { Input.BeginStop(); } catch (Exception error) { failure ??= error; }
            foreach (var operation in admitted)
                try { await operation; } catch (Exception error) { failure ??= error; }
            try { await renderer.Dispatcher.InvokeAsync(async () => await renderer.DisposeAsync()); }
            catch (Exception error) { failure ??= error; }
            failure ??= renderer.Error;
            try { await Input.CloseAsync(); } catch (Exception error) { failure ??= error; }
            try { await writer.CloseAsync(); } catch (Exception error) { failure ??= error; }
            if (ownedServices is not null)
                try { await ownedServices.DisposeAsync(); } catch (Exception error) { failure ??= error; }
        }
        finally
        {
            await registration.DisposeAsync();
            stopping.Dispose();
            Release();
            if (failure is { } error) { exited.TrySetException(error); _ = exited.Task.Exception; }
            else exited.TrySetResult(code);
        }
        if (failure is { } failureError) ExceptionDispatchInfo.Capture(failureError).Throw();
    }
    private void Release()
    {
        lock (owners) { owners.Remove(options.Stdout); owners.Remove(options.Stderr); }
    }
}
