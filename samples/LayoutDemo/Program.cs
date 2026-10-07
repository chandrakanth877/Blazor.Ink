using Blazor.Ink;
using Microsoft.AspNetCore.Components;

if (args.Contains("--input"))
{
    var scripted = args.Contains("--scripted");
    if (!scripted && (Console.IsInputRedirected || Console.IsOutputRedirected))
        throw new NotSupportedException("Native input demo requires a terminal; use --input --scripted for redirected output.");
    using var source = scripted ? new MemoryStream(System.Text.Encoding.UTF8.GetBytes("\x1b[200~literal paste \u0003\x1b[201~q")) : null;
    await using var session = await InkHost.RenderAsync<LayoutDemo.InputDemo>(new()
        { Columns = 60, Rows = 24, IncrementalRendering = true, Stdin = source });
    await session.WaitUntilExitAsync();
    return;
}

if (args.Contains("--session"))
{
    await using var session = await InkHost.RenderAsync<LayoutDemo.SessionDemo>(new()
        { Columns = 60, Rows = 24, IncrementalRendering = true });
    var steps = new[] { "User: Review the output runtime.", "Assistant: Static history is appended once.",
        "Assistant: Live output and logs share one writer." };
    for (var i = 0; i < steps.Length; i++)
    {
        await session.RerenderAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            ["Transcript"] = steps.Take(i + 1).ToArray(),
            ["Status"] = i == steps.Length - 1 ? "Complete — safe teardown" : $"Step {i + 1}/{steps.Length}"
        }));
        if (!Console.IsOutputRedirected) await Task.Delay(150);
    }
    await session.AwaitFlushAsync();
    return;
}

var frame = await InkHost.RenderToStringAsync<LayoutDemo.Demo>(columns: 60);
Console.WriteLine(Console.IsOutputRedirected ? TerminalText.Plain(frame) : frame);
