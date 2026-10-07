using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazor.Ink.Checks;

public sealed class KeyedItem : ComponentBase, IDisposable
{
    internal static int Created;
    internal static readonly List<string> Disposed = [];
    [Parameter] public string Name { get; set; } = "";
    private readonly int identity = ++Created;
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "ink-text");
        builder.AddContent(1, $"{Name}:{identity}");
        builder.CloseElement();
    }
    public void Dispose() => Disposed.Add(Name);
}

public sealed class KeyedList : ComponentBase
{
    [Parameter] public string[] Items { get; set; } = [];
    [Parameter] public string Markup { get; set; } = "";
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "ink-box");
        builder.AddAttribute(1, "flexDirection", "column");
        builder.AddContent(2, (RenderFragment)(content =>
        {
            foreach (var item in Items)
            {
                content.OpenComponent<KeyedItem>(0);
                content.SetKey(item);
                content.AddAttribute(1, "Name", item);
                content.CloseComponent();
            }
        }));
        builder.AddMarkupContent(3, Markup);
        builder.CloseElement();
    }
}

public sealed class AsyncLifecycle : ComponentBase, IAsyncDisposable
{
    internal static bool Disposed;
    [Parameter] public bool FailDispose { get; set; }
    private string text = "loading";
    protected override async Task OnInitializedAsync()
    {
        await Task.Yield();
        text = "ready";
    }
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "ink-text");
        builder.AddAttribute(1, "onclick", EventCallback.Factory.Create(this, () => text = "clicked"));
        builder.AddContent(2, text);
        builder.CloseElement();
    }
    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return FailDispose ? ValueTask.FromException(new InvalidOperationException("dispose failure")) : ValueTask.CompletedTask;
    }
}

public sealed class SplitGrapheme : ComponentBase
{
    [Parameter] public string[] Parts { get; set; } = [];
    [Parameter] public bool Styled { get; set; }
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "ink-text");
        foreach (var part in Parts)
        {
            if (Styled)
            {
                builder.OpenElement(1, "ink-text");
                builder.AddAttribute(2, "color", "red");
                builder.AddContent(3, part);
                builder.CloseElement();
            }
            else builder.AddContent(4, part);
        }
        builder.CloseElement();
    }
}

public sealed class ApplicationService : IDisposable
{
    public bool Disposed { get; private set; }
    public string Message => "application DI";
    public void Dispose() => Disposed = true;
}

public sealed class InjectedComponent : ComponentBase
{
    [Inject] public ApplicationService Service { get; set; } = null!;
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "ink-text");
        builder.AddContent(1, Service.Message);
        builder.CloseElement();
    }
}

public sealed class SessionComponent : ComponentBase, IDisposable
{
    [Inject] public InkSession Session { get; set; } = null!;
    [Inject] public ApplicationService Service { get; set; } = null!;
    [Parameter] public bool Fail { get; set; }
    internal static InkSession? MountedSession;
    internal static bool Disposed;
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        MountedSession = Session;
        if (Fail) throw new InvalidOperationException("scripted component failure");
        builder.OpenElement(0, "ink-text");
        builder.AddContent(1, Service.Message);
        builder.CloseElement();
    }
    public void Dispose() => Disposed = true;
}

public sealed class StaticLifecycle : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<Blazor.Ink.Components.Static<string>>(0);
        builder.AddAttribute(1, "Items", new[] { "consumed" });
        builder.AddAttribute(2, "ChildContent", (RenderFragment<string>)(item => child =>
        {
            child.OpenComponent<KeyedItem>(0);
            child.AddAttribute(1, "Name", item);
            child.CloseComponent();
        }));
        builder.CloseComponent();
    }
}

public sealed class GatedParameters : ComponentBase
{
    [Parameter] public Task? Gate { get; set; }
    internal static TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override async Task OnParametersSetAsync()
    {
        if (Gate is null) return;
        Entered.TrySetResult();
        await Gate;
    }
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "ink-text");
        builder.AddContent(1, "ready");
        builder.CloseElement();
    }
}

public sealed class ExitingComponent : ComponentBase
{
    [Inject] public InkSession Session { get; set; } = null!;
    protected override void OnInitialized() => Session.RequestExit(3);
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "ink-text");
        builder.AddContent(1, "done");
        builder.CloseElement();
    }
}

public sealed class FlushingComponent : ComponentBase
{
    [Parameter] public bool OffDispatcher { get; set; }
    [Inject] public InkSession Session { get; set; } = null!;
    protected override async Task OnInitializedAsync()
    {
        if (OffDispatcher) await Task.Delay(50).ConfigureAwait(false);
        else await Task.Yield();
        await Session.WriteAsync("initialization log");
        await Session.AwaitFlushAsync();
    }
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "ink-text");
        builder.AddContent(1, "initialized");
        builder.CloseElement();
    }
}

public sealed class KeyedElements : ComponentBase
{
    [Parameter] public string[] Items { get; set; } = [];
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "ink-box");
        foreach (var item in Items)
        {
            builder.OpenElement(1, "ink-text");
            builder.SetKey(item);
            builder.AddContent(2, item);
            builder.CloseElement();
        }
        builder.CloseElement();
    }
}
