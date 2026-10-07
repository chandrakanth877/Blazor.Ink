using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging.Abstractions;
using System.Xml;
using System.Xml.Linq;

namespace Blazor.Ink;

// ponytail: rebuild committed snapshots in O(n); apply RenderBatch edits only if profiling warrants it.
internal sealed class TerminalRenderer(IServiceProvider services, int columns, Func<TerminalUpdate, Task>? display = null)
    : Renderer(services, NullLoggerFactory.Instance)
{
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
    public string Output { get; private set; } = "";
    public Exception? Error { get; private set; }
    public Action<Exception>? Faulted { get; set; }
    public TerminalNode? CommittedRoot { get; private set; }
    private readonly AsyncLocal<bool> lifecycleOrigin = new();
    public bool IsLifecycleContext => lifecycleOrigin.Value || Dispatcher.CheckAccess();
    private Dictionary<object, TerminalNode> nodes = [];
    private sealed record FrameIdentity(int Component, object? Parent, int Sequence, int Occurrence, object? Key);
    private int rootId = -1;

    public Task MountAsync<TComponent>(ParameterView parameters) where TComponent : IComponent =>
        Dispatcher.InvokeAsync(async () =>
        {
            if (rootId != -1) throw new InvalidOperationException("Renderer is already mounted.");
            rootId = AssignRootComponentId(InstantiateComponent(typeof(TComponent)));
            await RenderRootAsync(parameters);
        });

    public Task RerenderAsync(ParameterView parameters) =>
        Dispatcher.InvokeAsync(() => RenderRootAsync(parameters));

    internal Task InvokeCallbackAsync(Func<Task> callback) => Dispatcher.InvokeAsync(async () =>
    {
        var previous = lifecycleOrigin.Value;
        lifecycleOrigin.Value = true;
        try { await callback(); }
        finally { lifecycleOrigin.Value = previous; }
    });

    private async Task RenderRootAsync(ParameterView parameters)
    {
        var previous = lifecycleOrigin.Value;
        lifecycleOrigin.Value = true;
        try { await RenderRootComponentAsync(rootId, parameters); }
        finally { lifecycleOrigin.Value = previous; }
    }

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        var root = new TerminalNode("ink-root") { Identity = "root" };
        Read(rootId, root.Children);
        TerminalTree.Validate(root);
        var staticNodes = new List<TerminalNode>();
        ExtractStatic(root, staticNodes);
        var staticOutput = new List<TerminalFrame>();
        var appended = new List<TerminalFrame>();
        foreach (var node in staticNodes)
        {
            var staticRoot = new TerminalNode("ink-root");
            staticRoot.Children.Add(node);
            staticOutput.Add(TerminalTree.PaintFrame(staticRoot, columns));
            if (display is not null)
            {
                var pending = new TerminalNode("ink-box");
                foreach (var attribute in node.Attributes) pending.Attributes.Add(attribute.Key, attribute.Value);
                // Static already renders only its unconsumed items; do not duplicate its append index.
                pending.Children.AddRange(node.Children);
                if (pending.Children.Count > 0)
                {
                    var pendingRoot = new TerminalNode("ink-root");
                    pendingRoot.Children.Add(pending);
                    appended.Add(TerminalTree.PaintFrame(pendingRoot, columns));
                }
            }
        }
        var dynamicOutput = TerminalTree.PaintFrame(root, columns);
        staticOutput.Add(dynamicOutput);
        Output = string.Join('\n', staticOutput.Where(frame => frame.Height > 0).Select(frame => frame.Text));
        var next = new Dictionary<object, TerminalNode>();
        CommittedRoot = Commit(root, next);
        foreach (var node in staticNodes) Commit(node, next);
        nodes = next;
        foreach (var node in staticNodes)
        {
            if (display is null) continue;
            var count = int.Parse(node.Attributes["staticCount"]!.ToString()!);
            if (node.Attributes.TryGetValue("staticCommit", out var callback) && callback is Action<int> committed) committed(count);
        }
        return display?.Invoke(new(dynamicOutput, appended)) ?? Task.CompletedTask;
    }

    private TerminalNode Commit(TerminalNode snapshot, Dictionary<object, TerminalNode> next)
    {
        var children = snapshot.Children.Select(child => Commit(child, next)).ToArray();
        var node = nodes.TryGetValue(snapshot.Identity!, out var existing) && existing.Name == snapshot.Name ? existing : snapshot;
        if (!ReferenceEquals(node, snapshot)) node.CommitFrom(snapshot, children);
        else { node.Children.Clear(); node.Children.AddRange(children); node.Layout = null; }
        next.Add(snapshot.Identity!, node);
        return node;
    }

    private static void ExtractStatic(TerminalNode node, List<TerminalNode> statics, string? inheritedBackground = null)
    {
        var style = BoxStyle.Read(node.Attributes);
        if (style.Display == Facebook.Yoga.YGDisplay.None) return;
        var background = style.BackgroundColor ?? inheritedBackground;
        foreach (var child in node.Children.ToArray())
        {
            if (BoxStyle.Read(child.Attributes).Display == Facebook.Yoga.YGDisplay.None) continue;
            if (child.Attributes.TryGetValue("static", out var value) && value is true)
            {
                if (!child.Attributes.ContainsKey("backgroundColor") && background is not null)
                    child.Attributes["backgroundColor"] = background;
                statics.Add(child); node.Children.Remove(child);
            }
            else ExtractStatic(child, statics, background);
        }
    }

    private void Read(int componentId, List<TerminalNode> output, int depth = 0)
    {
        TerminalTree.CheckDepth(depth);
        var frames = GetCurrentRenderTreeFrames(componentId);
        ReadFrames(frames.Array, 0, frames.Count, output, depth, componentId, null);
    }

    private void ReadFrames(RenderTreeFrame[] frames, int start, int end, List<TerminalNode> output, int depth,
        int componentId, object? parent)
    {
        TerminalTree.CheckDepth(depth);
        var occurrences = new Dictionary<int, int>();
        for (var i = start; i < end; i++)
        {
            var frame = frames[i];
            var occurrence = occurrences.GetValueOrDefault(frame.Sequence);
            occurrences[frame.Sequence] = occurrence + 1;
            var identity = new FrameIdentity(componentId, parent, frame.Sequence, occurrence, null);
            if (frame.FrameType == RenderTreeFrameType.Component)
            {
                Read(frame.ComponentId, output, depth + 1);
                i += frame.ComponentSubtreeLength - 1;
            }
            else if (frame.FrameType == RenderTreeFrameType.Element)
            {
                if (frame.ElementKey is { } key) identity = new(componentId, parent, 0, 0, key);
                var node = new TerminalNode(frame.ElementName) { Identity = identity };
                var finish = i + frame.ElementSubtreeLength;
                var childStart = i + 1;
                while (childStart < finish && frames[childStart].FrameType == RenderTreeFrameType.Attribute)
                {
                    node.Attributes[frames[childStart].AttributeName] = frames[childStart].AttributeValue;
                    childStart++;
                }
                ReadFrames(frames, childStart, finish, node.Children, depth + 1, componentId, identity);
                output.Add(node);
                i = finish - 1;
            }
            else if (frame.FrameType == RenderTreeFrameType.Region)
            {
                ReadFrames(frames, i + 1, i + frame.RegionSubtreeLength, output, depth + 1, componentId, identity);
                i += frame.RegionSubtreeLength - 1;
            }
            else if (frame.FrameType == RenderTreeFrameType.Text)
                output.Add(new("#text") { Value = frame.TextContent, Identity = identity });
            else if (frame.FrameType == RenderTreeFrameType.Markup)
            {
                using var reader = XmlReader.Create(new StringReader("<root>" + frame.MarkupContent.Replace("&nbsp;", "&#160;") + "</root>"),
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_000_000 });
                var index = 0;
                foreach (var node in XElement.Load(reader).Nodes()) output.Add(ReadMarkup(node, depth + 1, identity, index++));
            }
        }
    }

    private static TerminalNode ReadMarkup(XNode source, int depth, object parent, int index)
    {
        TerminalTree.CheckDepth(depth);
        var identity = new FrameIdentity(-1, parent, 0, index, null);
        if (source is XText text) return new("#text") { Value = text.Value, Identity = identity };
        if (source is not XElement element) return new("#text") { Identity = identity };
        var node = new TerminalNode(element.Name.LocalName) { Identity = identity };
        foreach (var attribute in element.Attributes()) node.Attributes[attribute.Name.LocalName] = attribute.Value;
        var childIndex = 0;
        foreach (var child in element.Nodes()) node.Children.Add(ReadMarkup(child, depth + 1, identity, childIndex++));
        return node;
    }

    protected override void HandleException(Exception exception)
    {
        // Blazor wraps failed display tasks; preserve the original sink/cancellation error.
        while (exception is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
            exception = aggregate.InnerExceptions[0];
        if (Error is not null) return;
        Error = exception;
        Faulted?.Invoke(exception);
    }
}
