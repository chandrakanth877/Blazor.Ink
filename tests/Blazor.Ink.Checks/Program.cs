using Facebook.Yoga;
using Microsoft.Extensions.DependencyInjection;
using static Facebook.Yoga.YGNodeAPI;
using static Facebook.Yoga.YGNodeStyleAPI;
using static Facebook.Yoga.YGNodeLayoutAPI;

if (args.Length == 2 && args[0] == "--native-input-child")
{
    await Blazor.Ink.Checks.NativeInputChecks.ChildAsync(args[1]);
    return;
}
if (args.Contains("--conpty-input"))
{
    await Blazor.Ink.Checks.ConPtyInputChecks.RunAsync();
    return;
}

var checks = 0;
void Equal<T>(T expected, T actual, string name)
{
    if (!Equals(expected, actual))
        throw new Exception($"{name}: expected [{expected}], got [{actual}]");
    checks++;
}

var root = YGNodeNew();
var first = YGNodeNew();
var second = YGNodeNew();
YGNodeInsertChild(root, first, 0);
YGNodeInsertChild(root, second, 1);
YGNodeStyleSetWidth(root, 30);
YGNodeStyleSetFlexDirection(root, YGFlexDirection.Row);
YGNodeStyleSetFlexGrow(first, 1);
YGNodeStyleSetFlexGrow(second, 2);
YGNodeCalculateLayout(root, float.NaN, float.NaN, YGDirection.LTR);
Equal(10f, YGNodeLayoutGetWidth(first), "Yoga grow 1");
Equal(20f, YGNodeLayoutGetWidth(second), "Yoga grow 2");
YGNodeStyleSetFlexGrow(first, 0);
YGNodeStyleSetFlexGrow(second, 0);
YGNodeStyleSetFlexShrink(first, 1);
YGNodeStyleSetFlexShrink(second, 1);
YGNodeStyleSetWidth(first, 20);
YGNodeStyleSetWidth(second, 20);
YGNodeCalculateLayout(root, float.NaN, float.NaN, YGDirection.LTR);
Equal(15f, YGNodeLayoutGetWidth(first), "Yoga shrink first");
Equal(15f, YGNodeLayoutGetWidth(second), "Yoga shrink second");
YGNodeStyleSetGap(root, YGGutter.Column, 2);
YGNodeCalculateLayout(root, float.NaN, float.NaN, YGDirection.LTR);
Equal(14f, YGNodeLayoutGetWidth(first), "Yoga gap participates in shrink");
Equal(16f, YGNodeLayoutGetLeft(second), "Yoga gap position");
YGNodeStyleSetWidthPercent(first, 50);
YGNodeStyleSetWidth(second, 10);
YGNodeCalculateLayout(root, float.NaN, float.NaN, YGDirection.LTR);
Equal(15f, YGNodeLayoutGetWidth(first), "Yoga percentage width");
YGNodeFreeRecursive(root);

var leaf = YGNodeNew();
var text = "hello";
YGNodeSetMeasureFunc(leaf, (_, width, mode, _, _) =>
    new YGSize { Width = mode == MeasureMode.Undefined ? text.Length : Math.Min(text.Length, width),
        Height = mode == MeasureMode.Undefined ? 1 : MathF.Ceiling(text.Length / Math.Max(width, 1)) });
YGNodeStyleSetWidth(leaf, 10);
YGNodeCalculateLayout(leaf, float.NaN, float.NaN, YGDirection.LTR);
Equal(10f, YGNodeLayoutGetWidth(leaf), "Yoga measured leaf constrained width");
YGNodeStyleSetWidthAuto(leaf);
YGNodeCalculateLayout(leaf, float.NaN, float.NaN, YGDirection.LTR);
Equal(5f, YGNodeLayoutGetWidth(leaf), "Yoga unconstrained text width");
text = "hello terminal";
YGNodeMarkDirty(leaf);
YGNodeCalculateLayout(leaf, float.NaN, float.NaN, YGDirection.LTR);
Equal(14f, YGNodeLayoutGetWidth(leaf), "Yoga dirty text measurement");
YGNodeStyleSetWidth(leaf, 10);
YGNodeCalculateLayout(leaf, float.NaN, float.NaN, YGDirection.LTR);
Equal(2f, YGNodeLayoutGetHeight(leaf), "Yoga wrapped height measurement");
YGNodeFree(leaf);
Console.WriteLine($"PASS {checks} checks ({System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription})");
Equal("Hello terminal", await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.Hello>(), "Razor terminal render");
foreach (var (name, expected) in new[] {
    ("border", "┌───┐\n│é👩‍💻│\n└───┘"),
    ("row", "你好|"), ("column", "A\n\nB"),
    ("wrap", "hello\nworld"), ("truncate", "abc…"),
    ("clip", "abc"), ("padding", "\n x\n"),
    ("percent", "abcde|"), ("hidden", "ok"), ("nested", "ABC")
})
{
    var parameters = Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?> { ["Case"] = name });
    Equal(expected, await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.LayoutCases>(parameters: parameters), name);
}
Console.WriteLine($"PASS {checks} checks");
await Throws<InvalidOperationException>(async () =>
    await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.AsyncLifecycle>(parameters:
        Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?> { ["FailDispose"] = true })),
    "component disposal errors are propagated");

async Task Throws<T>(Func<Task> action, string name) where T : Exception
{
    try { await action(); }
    catch (T) { checks++; return; }
    throw new Exception($"{name}: expected {typeof(T).Name}");
}
foreach (var (name, expected) in new[] {
    ("components", "┌─────┐\n│hello│\n└─────┘"),
    ("spacer", "A    B"), ("newline", "A\n\nB"),
    ("transform", "0:ONE\n1:TWO"), ("static", "first\nsecond\nlive"),
    ("word", "hello \nworld")
})
{
    var parameters = Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?> { ["Case"] = name });
    Equal(expected, await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.ComponentCases>(parameters: parameters), name);
}
Equal(2, Blazor.Ink.TerminalText.Width("👩‍💻"), "joined emoji width");
Equal(2, Blazor.Ink.TerminalText.Width("👍🏽"), "skin-tone emoji width");
Equal(2, Blazor.Ink.TerminalText.Width("🇺🇸"), "flag width");
Equal(1, Blazor.Ink.TerminalText.Width("é"), "combining mark width");
Equal("safe", Blazor.Ink.TerminalText.Plain("\x1b]52;c;clipboard\x07\x1b[2Jsafe"), "terminal injection removed");
Equal(4, Blazor.Ink.TerminalText.Width("ab\ncdef"), "multiline width is widest line");
Equal("safe", Blazor.Ink.TerminalText.Plain("\x1bPtmux;\x1b\x1b]52;c;ignored\x07hidden\x1b\\safe"),
    "DCS payload cannot escape at BEL");
Equal("\x1b[48;2;0;0;255mx\x1b[0m",
    await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.ComponentCases>(parameters:
        Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?> { ["Case"] = "background" })),
    "Text background");
Equal("你… |", await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.LayoutCases>(parameters:
    Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?> { ["Case"] = "wide-truncate" })),
    "wide truncation reserves adjacent column (Ink truncate-width fixture)");
Equal("\x1b[48;2;0;0;255mx   \x1b[0m\n\x1b[48;2;0;0;255m    \x1b[0m",
    await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.LayoutCases>(parameters: Parameters(("Case", "box-background"))),
    "Box fills background and Text inherits it");
Equal("\x1b[38;2;255;0;0m┌─┐\x1b[0m\n\x1b[38;2;255;0;0m│\x1b[0mx\x1b[38;2;255;0;0m│\x1b[0m\n\x1b[38;2;255;0;0m└─┘\x1b[0m",
    await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.LayoutCases>(parameters: Parameters(("Case", "border-color"))),
    "border foreground does not color its contents");
Equal("\x1b[48;2;0;0;255m  \x1b[0m",
    await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.LayoutCases>(parameters: Parameters(("Case", "clip-wide"))),
    "clipped wide halves keep their backgrounds (Ink clip-wide-background fixture)");
var overlap = new Blazor.Ink.Canvas(3, 1);
overlap.Put(0, 0, new("你", 2, "\x1b[48;2;0;0;255m"));
overlap.Put(2, 0, new("Z", 1, "\x1b[48;2;0;0;255m"));
overlap.Put(1, 0, new("X", 1, ""));
Equal("\x1b[48;2;0;0;255m \x1b[0mX\x1b[48;2;0;0;255mZ\x1b[0m",
    overlap.ToString(), "overwritten wide half preserves exposed background (Ink overlap-wide-background fixture)");
using (var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider())
{
    var renderer = new Blazor.Ink.TerminalRenderer(services, 80);
    try
    {
        await renderer.MountAsync<Blazor.Ink.Checks.KeyedList>(Parameters(("Items", new[] { "A", "B", "C" })));
        Equal("A:1\nB:2\nC:3", renderer.Output, "initial keyed region");
        await Rerender(renderer, Parameters(("Items", new[] { "C", "A", "B" })));
        Equal("C:3\nA:1\nB:2", renderer.Output, "keyed permutation preserves component identity");
        await Rerender(renderer, Parameters(("Items", new[] { "C", "D" }), ("Markup", "<ink-text>markup</ink-text>")));
        Equal("C:3\nD:4\nmarkup", renderer.Output, "keyed insert/remove and markup update");
        Equal("A,B", string.Join(',', Blazor.Ink.Checks.KeyedItem.Disposed.Order()), "removed components disposed");
        await Rerender(renderer, Parameters(("Items", Array.Empty<string>()), ("Markup", "<ink-text>changed</ink-text>")));
        Equal("changed", renderer.Output, "markup replacement");
        Equal<Exception?>(null, renderer.Error, "renderer updates do not fail");
    }
    finally { await renderer.DisposeAsync(); }
    Equal("A,B,C,D", string.Join(',', Blazor.Ink.Checks.KeyedItem.Disposed.Order()), "all components disposed");

    await using var styles = new Blazor.Ink.TerminalRenderer(services, 80);
    await styles.MountAsync<Blazor.Ink.Checks.ComponentCases>(Parameters(("Case", "remove-style"), ("Narrow", true)));
    Equal("abc\ndef", styles.Output, "initial constrained width");
    await Rerender(styles, Parameters(("Case", "remove-style"), ("Narrow", false)));
    Equal("abcdef", styles.Output, "removed width restores default");

    await using var events = new Blazor.Ink.TerminalRenderer(services, 80);
    await events.MountAsync<Blazor.Ink.Checks.AsyncLifecycle>(Microsoft.AspNetCore.Components.ParameterView.Empty);
    Equal("ready", events.Output, "asynchronous lifecycle reaches quiescence");
    await events.Dispatcher.InvokeAsync(async () =>
    {
        var frames = Frames(events, RootId(events));
        var handler = frames.Array.Take(frames.Count).Single(f =>
            f.FrameType == Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameType.Attribute).AttributeEventHandlerId;
        await events.DispatchEventAsync(handler, null, EventArgs.Empty);
    });
    Equal("clicked", events.Output, "Blazor event dispatch schedules repaint");
}
Equal("ready", await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.AsyncLifecycle>(), "headless async state");
Equal(true, Blazor.Ink.Checks.AsyncLifecycle.Disposed, "headless disposes component");
await Throws<InvalidOperationException>(async () => await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.ComponentCases>(
    parameters: Parameters(("Case", "invalid"))), "Box inside Text rejected");
var repeatedSgr = string.Concat(Enumerable.Repeat("\x1b[31ma", 8000));
Blazor.Ink.TerminalText.Plain("warmup");
var allocated = GC.GetAllocatedBytesForCurrentThread();
Equal(8000, Blazor.Ink.TerminalText.Plain(repeatedSgr).Length, "plain styled stress text retained");
Equal(true, GC.GetAllocatedBytesForCurrentThread() - allocated < 16_000_000, "plain sanitizer allocation is bounded linearly");
allocated = GC.GetAllocatedBytesForCurrentThread();
Equal(8000, Blazor.Ink.TerminalText.Parse(repeatedSgr).Count, "styled stress glyphs retained");
Equal(true, GC.GetAllocatedBytesForCurrentThread() - allocated < 16_000_000, "styled parser allocation is bounded linearly");
await Throws<NotSupportedException>(async () => await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.KeyedList>(parameters:
    Parameters(("Markup", string.Concat(Enumerable.Repeat("<ink-box>", 256)) + "<ink-text>x</ink-text>" +
        string.Concat(Enumerable.Repeat("</ink-box>", 256))))), "excessive markup depth is rejected before recursion");
await Throws<ArgumentOutOfRangeException>(async () => await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.Hello>(columns: int.MaxValue),
    "columns bounded before layout/allocation");
await Throws<ArgumentOutOfRangeException>(async () => await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.LayoutCases>(
    parameters: Parameters(("Case", "huge-height"))), "rendered height bounded before allocation");
await Throws<ArgumentOutOfRangeException>(() => { _ = new Blazor.Ink.Canvas(1024, 1024); return Task.CompletedTask; },
    "canvas product bounded before allocation");
await Throws<System.Xml.XmlException>(async () => await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.KeyedList>(parameters:
    Parameters(("Markup", "<!DOCTYPE x [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><ink-text>&x;</ink-text>"))),
    "DTD and external entities prohibited");
Equal("", Blazor.Ink.TerminalText.Parse("\x1b[1ma\x1b[22mb")[1].Style, "SGR intensity reset clears active state");
Equal("\x1b[38;5;196m\x1b[48;2;1;2;3m", Blazor.Ink.TerminalText.Parse("\x1b[38;5;196;48;2;1;2;3mx")[0].Style,
    "SGR 256/RGB colors retain effective state");
Equal("👩‍💻", await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.SplitGrapheme>(
    parameters: Parameters(("Parts", new[] { "👩", "\u200d", "💻" }))), "joined emoji across text frames");
Equal("你́", await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.SplitGrapheme>(
    parameters: Parameters(("Parts", new[] { "你", "\u0301" }))), "combining mark after wide text frame");
Equal("☀️\n|", await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.SplitGrapheme>(columns: 2,
    parameters: Parameters(("Parts", new[] { "☀", "\ufe0f", "|" }))), "variation selector updates measured width");
Equal("👩‍💻", Blazor.Ink.TerminalText.Plain(await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.SplitGrapheme>(
    parameters: Parameters(("Parts", new[] { "👩", "\u200d", "💻" }), ("Styled", true)))),
    "grapheme survives nested style boundaries");
Equal("\x1b[38;2;0;0;255mé\x1b[0m\x1b[38;2;255;0;0mX\x1b[0m",
    await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.LayoutCases>(parameters: Parameters(("Case", "styled-accent"))),
    "combining mark uses base style; following text keeps its color (Ink styled-combining-marks fixture)");
Equal("A\nB", await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.LayoutCases>(
    parameters: Parameters(("Case", "root-fragment"))), "implicit root flows vertically");
Equal("你… |", await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.LayoutCases>(
    parameters: Parameters(("Case", "max-truncate"))), "max-width truncation reserves constrained width");
await Throws<InvalidOperationException>(async () => await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.ComponentCases>(
    parameters: Parameters(("Case", "invalid-static"))), "Static cannot bypass Box-inside-Text validation");
using (var applicationServices = new ServiceCollection().AddSingleton<Blazor.Ink.Checks.ApplicationService>().BuildServiceProvider())
{
    Equal("application DI", await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.InjectedComponent>(services: applicationServices),
        "application dependency injection");
    var service = applicationServices.GetRequiredService<Blazor.Ink.Checks.ApplicationService>();
    Equal(false, service.Disposed, "caller retains provider ownership");
    applicationServices.Dispose();
    Equal(true, service.Disposed, "caller can dispose application services");
}
Equal("first\n", await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.StaticCases>(
    parameters: Parameters(("BlankDynamic", 1))), "Static retains blank dynamic row");
Equal("\nlive", await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.StaticCases>(
    parameters: Parameters(("Items", new[] { "blank" }))), "Static retains blank committed row");
Equal(true, (await Blazor.Ink.InkHost.RenderToStringAsync<Blazor.Ink.Checks.StaticCases>(columns: 6,
    parameters: Parameters(("Background", "blue")))).StartsWith("\x1b[48;2;0;0;255mfirst "),
    "Static inherits ancestor background");
using (var identityServices = new ServiceCollection().BuildServiceProvider())
{
    await using var identityRenderer = new Blazor.Ink.TerminalRenderer(identityServices, 80);
    await identityRenderer.MountAsync<Blazor.Ink.Checks.ComponentCases>(Parameters(("Case", "remove-style"), ("Narrow", true)));
    var node = identityRenderer.CommittedRoot!.Children.Single();
    await Rerender(identityRenderer, Parameters(("Case", "remove-style"), ("Narrow", false)));
    Equal(true, ReferenceEquals(node, identityRenderer.CommittedRoot!.Children.Single()), "committed terminal node identity survives update");
    await using var keyedRenderer = new Blazor.Ink.TerminalRenderer(identityServices, 80);
    await keyedRenderer.MountAsync<Blazor.Ink.Checks.KeyedList>(Parameters(("Items", new[] { "A", "B" })));
    var componentNodes = keyedRenderer.CommittedRoot!.Children.Single().Children.ToArray();
    await Rerender(keyedRenderer, Parameters(("Items", new[] { "B", "C", "A" })));
    var movedComponents = keyedRenderer.CommittedRoot!.Children.Single().Children;
    Equal(true, ReferenceEquals(componentNodes[0], movedComponents[2]) && ReferenceEquals(componentNodes[1], movedComponents[0]),
        "committed terminal nodes follow keyed component moves");
    await using var elementRenderer = new Blazor.Ink.TerminalRenderer(identityServices, 80);
    await elementRenderer.MountAsync<Blazor.Ink.Checks.KeyedElements>(Parameters(("Items", new[] { "A", "B" })));
    var elementNodes = elementRenderer.CommittedRoot!.Children.Single().Children.ToArray();
    await Rerender(elementRenderer, Parameters(("Items", new[] { "B", "C", "A" })));
    var movedElements = elementRenderer.CommittedRoot!.Children.Single().Children;
    Equal(true, ReferenceEquals(elementNodes[0], movedElements[2]) && ReferenceEquals(elementNodes[1], movedElements[0]),
        "committed terminal nodes follow keyed raw-element moves");
}
Console.WriteLine($"PASS {checks} checks");

Microsoft.AspNetCore.Components.ParameterView Parameters(params (string Name, object? Value)[] values) =>
    Microsoft.AspNetCore.Components.ParameterView.FromDictionary(values.ToDictionary(pair => pair.Name, pair => pair.Value));

// Test the framework-sensitive adapter without adding a public live-session API prematurely.
int RootId(Blazor.Ink.TerminalRenderer renderer) => (int)typeof(Blazor.Ink.TerminalRenderer)
    .GetField("rootId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(renderer)!;
Task Rerender(Blazor.Ink.TerminalRenderer renderer, Microsoft.AspNetCore.Components.ParameterView parameters) =>
    renderer.Dispatcher.InvokeAsync(() => (Task)typeof(Microsoft.AspNetCore.Components.RenderTree.Renderer)
        .GetMethod("RenderRootComponentAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            [typeof(int), typeof(Microsoft.AspNetCore.Components.ParameterView)])!.Invoke(renderer, [RootId(renderer), parameters])!);
Microsoft.AspNetCore.Components.RenderTree.ArrayRange<Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrame>
    Frames(Blazor.Ink.TerminalRenderer renderer, int id) =>
    (Microsoft.AspNetCore.Components.RenderTree.ArrayRange<Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrame>)
    typeof(Microsoft.AspNetCore.Components.RenderTree.Renderer)
        .GetMethod("GetCurrentRenderTreeFrames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
        .Invoke(renderer, [id])!;

checks += await Blazor.Ink.Checks.SessionChecks.RunAsync();
checks += Blazor.Ink.Checks.InputChecks.Parser();
checks += await Blazor.Ink.Checks.InputSessionChecks.RunAsync();
Console.WriteLine($"PASS {checks} checks including sessions");
