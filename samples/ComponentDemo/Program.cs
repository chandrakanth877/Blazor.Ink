using ComponentDemo;
using Blazor.Ink;
using Microsoft.AspNetCore.Components;
using System.Text;

try
{
    var options = CommandLine.Parse(args);
    if (options.Help)
    {
        Console.WriteLine("Blazor.Ink showcase: [--page Chat|Text|Box|Static|Transform|Newline|Spacer|Runtime]\n" +
            "  [--columns N] [--rows N] [--snapshot [--all] | --scripted | --check]\n" +
            "Ctrl+N/P pages · Up/Down scroll · Esc/Ctrl+C exit. See the sample README for page controls.");
        return 0;
    }
    if (options.Check) { await Checks.RunAsync(); return 0; }
    var terminal = !Console.IsInputRedirected && !Console.IsOutputRedirected &&
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) &&
        Environment.GetEnvironmentVariable("TERM") != "dumb";
    var columns = options.Columns ?? 80;
    var rows = options.Rows ?? 24;
    CommandLine.ValidateSize(columns, rows);
    if (options.Snapshot || (!options.Scripted && !terminal))
    {
        foreach (var page in options.All ? ShowcaseState.Pages : [options.Page])
        {
            var preview = new ShowcaseState(columns, rows, page);
            if (page == "Static") preview.Tasks.Add("✓ Completed task 1");
            if (options.All) Console.WriteLine($"=== {page} ===");
            var frame = await InkHost.RenderToStringAsync<App>(columns,
                ParameterView.FromDictionary(new Dictionary<string, object?> { ["State"] = preview, ["Snapshot"] = true }));
            Console.WriteLine(TerminalText.Plain(frame));
        }
        return 0;
    }
    using var source = options.Scripted ? new MemoryStream(Encoding.UTF8.GetBytes(
        "/add demo.cs\rHello Blazor.Ink\r\u001b[200~Literal paste\r\u0003\u001b[201~\r\u0014" +
        "\u000e\u000e\u000e\r\u000e\u000e\u000e\u000eofrce\u000e\u0003")) : null;
    using var timeout = options.Scripted ? new CancellationTokenSource(TimeSpan.FromSeconds(15)) : null;
    await using var session = await InkHost.RenderAsync<LiveApp>(new()
    {
        Columns = options.Scripted ? columns : options.Columns,
        Rows = options.Scripted ? rows : options.Rows, Stdin = source, Interactive = !options.Scripted,
        IncrementalRendering = true, ExitOnCtrlC = false
    }, ParameterView.FromDictionary(new Dictionary<string, object?> { ["State"] = new ShowcaseState(columns, rows, options.Page) }),
        cancellationToken: timeout?.Token ?? default);
    return await session.WaitUntilExitAsync();
}
catch (Exception error)
{
    Console.Error.WriteLine($"ComponentDemo: {TerminalText.Plain(error.Message)}");
    return 1;
}

namespace ComponentDemo
{
    internal sealed record DemoOptions(string Page = "Chat", int? Columns = null, int? Rows = null,
        bool Snapshot = false, bool All = false, bool Scripted = false, bool Check = false, bool Help = false);
    internal static class CommandLine
    {
        internal static DemoOptions Parse(string[] args)
        {
            var options = new DemoOptions();
            for (var i = 0; i < args.Length; i++)
            {
                var flag = args[i];
                string Value() => ++i < args.Length ? args[i] : throw new ArgumentException($"Missing value for {flag}");
                int Number() => int.TryParse(Value(), out var value) ? value :
                    throw new ArgumentException($"{flag} requires an integer");
                options = flag switch
                {
                    "--page" => options with { Page = Value() },
                    "--columns" => options with { Columns = Number() },
                    "--rows" => options with { Rows = Number() },
                    "--snapshot" => options with { Snapshot = true },
                    "--all" => options with { All = true },
                    "--scripted" => options with { Scripted = true },
                    "--check" => options with { Check = true },
                    "--help" or "-h" => options with { Help = true },
                    _ => throw new ArgumentException($"Unknown argument '{flag}'. Use --help.")
                };
            }
            var page = ShowcaseState.Pages.FirstOrDefault(name => name.Equals(options.Page, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Unknown page '{options.Page}'. Choose: {string.Join(", ", ShowcaseState.Pages)}");
            if (options.All && !options.Snapshot) throw new ArgumentException("--all requires --snapshot");
            if ((options.Snapshot ? 1 : 0) + (options.Scripted ? 1 : 0) + (options.Check ? 1 : 0) > 1)
                throw new ArgumentException("Choose only one of --snapshot, --scripted, and --check");
            ValidateSize(options.Columns ?? 80, options.Rows ?? 24);
            return options with { Page = page };
        }

        internal static void ValidateSize(int columns, int rows)
        {
            if (columns is < 1 or > 16_384 || rows is < 2 or > 16_384 || (long)columns * rows > 1_000_000)
                throw new ArgumentException("Dimensions must fit Blazor.Ink limits: 1–16384 columns, 2–16384 rows, at most 1000000 cells.");
        }
    }
}
