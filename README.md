# Blazor.Ink — Razor layout, output sessions and input foundation

[GitHub repository](https://github.com/chandrakanth877/Blazor.Ink) ·
[Windows, Linux, and macOS validation](https://github.com/chandrakanth877/Blazor.Ink/actions/workflows/build.yml)

Razor components rendered into terminal text with Blazor and managed Yoga.
Targets `net8.0` and `net10.0`; .NET 10 is recommended. .NET 8 is a legacy
compatibility target and reaches end of support on November 10, 2026.

**This is an implementation increment, not the completed 20-week plan or an
interactive Ink replacement.** Sessions own live output and cursor visibility.
Input is activated by active subscriptions. Output-only sessions do not read
stdin or change native input modes. No agent execution is started.

## Run

Install the .NET 10 SDK and the .NET 8 and 10 ASP.NET Core runtimes to run
the complete checks. Dependencies are restored from NuGet.org:
`Yoga.Net` 3.2.3 and `Wcwidth` 4.0.1. No vendor source or wrapper package
is included.

```sh
dotnet run --project samples/LayoutDemo -f net10.0
dotnet run --project samples/LayoutDemo -f net10.0 -- --session
dotnet run --project samples/LayoutDemo -f net10.0 -- --input
dotnet run --project samples/LayoutDemo -f net10.0 -- --input --scripted
dotnet run --project samples/ComponentDemo -f net10.0
dotnet run --project samples/ComponentDemo -f net10.0 -- --snapshot --all
bash scripts/verify.sh
```

The check harness throws on failure and exits nonzero. The verification script
runs it, all three layout/input sample modes, and the component showcase checks,
snapshots and scripted walkthrough on both frameworks; it creates **unpublished** local
packages under `artifacts/packages`, and tests a separate PackageReference-only
consumer on both frameworks with an isolated package cache.

### Component showcase

[ComponentDemo](samples/ComponentDemo/README.md) uses a **ProjectReference**, not
NuGet. It has a Lela-inspired local chat screen and gallery pages for all six
implemented components, plus session/input demonstrations. Ctrl+N/P switches
pages; Up/Down scrolls; Esc/Ctrl+C exits. No AI provider or file access is involved.
Running it does not require packing packages or configuring a local feed.
Redirected input/output produces a plain snapshot and exits.

## Input foundation

Live components can inject `InkInput` (also available as `session.Input`).
`Subscribe(Func<InkInputEvent, Task>, bool isActive = true)` and
`SubscribePaste(Func<string, Task>, bool isActive = true)` return
`InkInputSubscription`. Dispose it with the component, or change `IsActive`.
`IsAvailable` identifies a usable source; `IsRawModeSupported` is true only
for supported native TTY input in an interactive session.

```csharp
using var keys = session.Input.Subscribe(async input =>
{
    if (!input.IsPaste && input.Text == "q") session.RequestExit();
    else await session.WriteAsync(input.Text);
});
using var paste = session.Input.SubscribePaste(text => session.WriteAsync(text));
await session.WaitUntilExitAsync();
```

Callbacks broadcast sequentially in registration order on the Blazor dispatcher.
Component callbacks that change component fields must call `StateHasChanged`.
Use `RequestExit`, not an awaited `ExitAsync`/`DisposeAsync`, inside callbacks.
Callback-origin flush covers committed output only; external mount/flush barriers
also wait for admitted input-mode transitions.

`InkInputEvent` exposes `Text`, nullable `InkKey`, and `IsPaste`. Legacy `InkKey`
exposes `Name`, `Shift`, `Ctrl`, and `Meta` (including legacy Alt). Names follow
the pinned Ink parser: `up`, `return`, `backspace`, `f1`, etc. Navigation/function
keys carry empty text; keypad Enter carries `"\r"`. Typed Ctrl+C exits normally
by default; set `InkOptions.ExitOnCtrlC = false` to handle it yourself.
Paste never triggers built-in Ctrl+C or Enter commands. Active paste subscribers
receive complete literal payloads; otherwise key/text subscribers receive
`IsPaste = true` and `Key = null`.

Nullable `InkOptions.Stdin` defaults to native standard input. Supply a readable
`Stream` for scripted/embedded byte input, even with redirected output. Custom
streams remain caller-owned, use cooperative cancellation, and never trigger
native controls. Normal EOF ends reading, not the session; EOF during paste is
an observable failure. Noncooperative custom reads can delay teardown; the host
never closes caller input to force cancellation. Headless rendering provides
no input service and does not read stdin.

The first active subscription acquires an exclusive source lease; the last
deactivation restores native settings. Changes are asynchronous: await an external
`AwaitFlushAsync` before handing the source to another owner. Native drivers cover
Windows, macOS and Linux. POSIX ABIs are x64/arm64 with validated termios layouts;
Windows requires virtual-terminal input support. Native settings and changed
Windows input code pages are restored on exit and startup/runtime failures.
A failed native restoration retains the lease rather than allowing an unsafe owner.

Input uses incremental UTF-8 and CSI/SS3 decoding, a 20 ms Escape timeout, and
bracketed paste without a paste timeout. Unknown complete replies are discarded;
Kitty is not enabled. Pending input is capped at 1,000,000 UTF-16 characters;
overflow fails visibly. Reading awaits callbacks rather than building an event
backlog; mode changes coalesce to one worker.

Native gates run separately:

```sh
bash scripts/check-native-input.sh
# Windows, on each target:
dotnet run --project tests/Blazor.Ink.Checks -c Release -f net10.0 -- --conpty-input
```

The POSIX gate compiles an ABI probe and checks isolated PTYs from child and
parent, including prior non-default settings. macOS sets kernel-owned `PENDIN`
bookkeeping on canonical re-entry; checks normalize only that bit, never drain
pending user input. Prior bracketed-paste state and cursor visibility are not
queried: enable/disable is balanced, not complete terminal-protocol restoration.
Linux/Windows are unverified until their native gates pass on those platforms.
The October 7, 2026 checkpoint passed 455 assertions per framework, all sample
modes, clean local-package consumers, and 22 macOS arm64 PTY runs. See the
[input-foundation ledger](docs/blazor-ink-input-foundation.md).

## Use in a Razor console project

Use `Microsoft.NET.Sdk.Razor` and an `Microsoft.AspNetCore.App` framework reference.
Once published, install version 1.0.0 with:

```sh
dotnet add package Blazor.Ink --version 1.0.0
```

The standalone repository's samples use project references. Package consumers
use the single `Blazor.Ink` package and its upstream NuGet dependencies.
The 1.0.0 version does not imply full Ink parity; the documented implementation
limitations still apply.

```razor
@using Blazor.Ink
@using Blazor.Ink.Components

<Box Width="@((Length)20)" BorderStyle="@BorderStyle.Round">
    <Blazor.Ink.Components.Text Color="cyan" Bold="true">Hello 👩‍💻</Blazor.Ink.Components.Text>
</Box>
```

```csharp
var text = await Blazor.Ink.InkHost.RenderToStringAsync<MyComponent>(columns: 80);
Console.WriteLine(Blazor.Ink.TerminalText.Plain(text)); // safe plain redirected output
```

Use the fully qualified `Blazor.Ink.Components.Text` tag: Razor reserves `<text>` in
some code-block contexts. Formatting whitespace outside Text is ignored;
non-whitespace text outside Text and Box inside Text throw.

`RenderToStringAsync` waits for initialization/parameter lifecycle quiescence,
returns styled text without writing to a terminal, and disposes components.
Parameters can be passed as a Blazor `ParameterView`. Background asynchronous
work after lifecycle quiescence is not awaited indefinitely.
Pass `services: yourProvider` to use application DI; the caller owns that
provider and its scope. An omitted provider is created and disposed by the host.

## Output sessions

```csharp
await using var session = await Blazor.Ink.InkHost.RenderAsync<MyComponent>(
    new Blazor.Ink.InkOptions { Columns = 80, Rows = 24, IncrementalRendering = true });
await session.RerenderAsync(parameters);
await session.WriteAsync("External output is sanitized and coordinated.");
await session.AwaitFlushAsync();
await session.ExitAsync(0);
var exitCode = await session.WaitUntilExitAsync();
```

`Stdout` and `Stderr` are caller-owned `TextWriter`s. Competing ownership of the
same writer instance is rejected. Custom sinks default to noninteractive output;
set `Interactive = true` only for a sink that supports ANSI cursor controls.
Automatic detection uses Console.Out redirection, `CI`, and `TERM=dumb`.
Columns/rows are explicit (defaults 80/24); automatic sizing/resize comes later.
The live region keeps at most `Rows - 1` trailing rows, reserving a cursor row.
Both full redraw and safe whole-line incremental redraw are available.
Interactive startup first writes CRLF to preserve any preexisting partial line
and establish column zero; this can add a blank line on an already fresh terminal.

`Static<TItem>` commits appended items once, releases their components after
commit, and preserves history across live redraw/clear. Items are append-only;
modifying a consumed item does not rewrite scrollback. A keyed remount starts a
new commit history. Redirected output writes Static immediately and only the
final live frame at teardown, without ANSI styling or terminal controls.

`ClearAsync` clears only live output; a later rerender can restore it.
`WriteErrorAsync` shares the output queue; actual Console stdout/stderr are
treated as one terminal. Independent custom sinks stay independent.
Do not bypass the session with direct Console writes while it owns a terminal.
Console redirection/patching is not implemented.

Components can `[Inject] InkSession`. Use `RequestExit(code)` from component
lifecycle/callback code; awaiting `ExitAsync` there can await your own disposal.
`StoppingToken` cancels at shutdown so component background work can stop.
Flush drains previously admitted host operations and queued output; it does not
wait for arbitrary detached background tasks. From component lifecycle code it
flushes already committed output, without awaiting that same lifecycle.
Shutdown drains admitted work,
disposes components, restores cursor visibility, flushes, and releases leases.
Failures remain observable via exit/disposal. Cancellation interrupts cooperative
writers; an uncooperative writer or lifecycle task can delay teardown indefinitely,
because releasing ownership while it could still write would be unsafe.
Host admission and the writer queue are each limited to 256 pending operations;
the writer also limits pending source characters to 16,000,000. Overflow is
observable and stops the session rather than silently discarding output.
Broad OS signal handling remains deferred; native input verification is recorded
separately in the input-foundation ledger.

## Implemented and checked

- Custom Blazor Renderer reading committed snapshots: components, elements,
  text, markup, regions, keyed moves/removals, and asynchronous/event updates.
- Managed Yoga layout with typed `BoxStyle`, explicit Box parameter precedence,
  row/column flow, dimensions/percentages, gap, padding, borders and clipping.
- `Box`, `Text`, `Newline`, `Spacer`, `Transform`, and append-once `Static<TItem>`.
- Persistent terminal-node identity through updates and keyed reconciliation.
- Session mount, rerender, clear, flush, exit, DI and disposal; a bounded ordered
  writer, unchanged-frame suppression, both redraw modes and final piped output.
- Virtual-terminal assertions for Static history, blank rows, clipping,
  shrinking/wide frames, logging, output errors, cancellation and shutdown races.
- `StringInfo` grapheme segmentation and pinned Wcwidth tables, including
  CJK, combining marks, ZWJ emoji, skin tones and flags.
- Wrapping and end/start/middle truncation, trusted semicolon SGR styles,
  Text decorations, Box backgrounds and side-specific border styles.
- Wide-cell overwrite/clipping preserves exposed backgrounds.
- Cursor/mode/clipboard controls stripped from text; XML markup parser rejects
  DTDs and external entity resolution. Literal component text is not markup.
- Cleanup and captured component-disposal exceptions reach the headless caller.
- Resource guards reject depth over 64, trees over 10,000 nodes, dimensions over
  16,384, and canvases over 1,000,000 cells before recursive layout/allocation.
  Markup documents are limited to 1,000,000 characters.
- Sanitizing repeated SGR commands uses bounded effective style state rather
  than retaining the command history. Grapheme segmentation spans inline frames;
  the leading run owns an indivisible grapheme's style, as in Ink's accent fixture.

The harness has selected literal Ink regression expectations, **not a complete
differential conformance suite**. Geometry/Unicode behavior outside those cases
remains subject to the roadmap gates. Layout snapshots are rebuilt in O(n);
this deliberately avoids copying an incomplete RenderBatch edit switch.

## Still required for full release

| Area | Status |
|---|---|
| Session output APIs; serialized writer and repaint modes | Preview implemented; no platform certification |
| Native input, fragmented decoding, bracketed paste and subscriptions | Input foundation implemented; certification is platform-specific |
| Kitty, focus, cursor intent and resize | Not implemented |
| Append-once Static and explicit stdout/stderr writes | Preview implemented |
| Suspension, Console patching, alternate screen, handled signals | Not implemented |
| Accessibility output, metrics and animation scheduler | Not implemented |
| Agent UI package, Markdown/highlighting/editor/approval/picker and scripted interactive demo | Not implemented |
| Full shared Ink corpus, OS PTY matrix, soak/performance gates | Not completed |

Additional preview limitations: no frame-rate coalescing (default 30 FPS remains
a release gate); no element metrics/ref adapter; only preset borders; colors accept
`#RRGGBB` or black/red/green/yellow/blue/magenta/cyan/white/gray/grey; colon SGR
and hyperlinks are stripped rather than normalized/preserved; unsupported SGR
families are ignored. Lengths do not
yet represent `auto` or negative positioned offsets. All 66 style fields are
typed and wired, but not every combination is certified. These are parity gaps,
not React-only exclusions.

Known deferred headless discrepancy: tabs currently expand to four fixed spaces
rather than Ink's eight-column stops. Static blank rows and extracted ancestor
backgrounds now have regression checks.

## Design and delivery documents

In the repository's `docs` directory:

- [What is done and what is left](docs/blazor-ink-status.md)
- [Research/reuse review](docs/blazor-ink-review.md)
- [TRD and complete surface inventory](docs/blazor-ink-trd.md)
- [20-week roadmap, four-week contingency, gates and execution evidence](docs/blazor-ink-roadmap.md)

See `THIRD-PARTY-NOTICES.md` for dependency versions, package source revisions,
and attribution. No RazorConsole or Spectre live/input loop is embedded.

## Packaging and publication

`Blazor.Ink` is the package ID, assembly name, and root namespace.
The previous `Ink` namespace is not retained as an alias.
This repository is a standalone export; the original research workspace is
not modified. The dependency graph is committed in package lock files.

```sh
bash scripts/verify.sh
```

This produces an unsigned `.nupkg` and portable-symbol `.snupkg` under
`artifacts/packages` and runs an isolated PackageReference-only consumer.
The checked-in SDK version is used for reproducible framework selection.

GitHub Actions verifies both frameworks on Windows, Linux, and macOS, including
native input checks. Pushes to `main` or `codex/**`, pull requests, and manual
workflow runs validate without publishing and preserve per-OS validation logs.
Only a published GitHub release with a `v<version>` tag
matching the package version can publish, and only when publishing has been
explicitly enabled. Pull requests and ordinary pushes cannot publish.

Publishing uses NuGet.org Trusted Publishing with GitHub OIDC; there is no
long-lived API key. NuGet.org repository-signs accepted packages. Local build
artifacts are **not author-signed**, and the assembly is not strong-named.
The workflow downloads the published package, verifies its signature, and
preserves it as a separate artifact. Symbol packages are not signed.

Repository setup, the first release, signing verification, and vendor-retention
alternatives are documented in `docs/publishing.md`.
