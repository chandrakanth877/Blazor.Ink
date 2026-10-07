# Blazor.Ink component showcase

A Razor terminal app inspired by the Lela JavaScript UI. It references the local
`Blazor.Ink.csproj` directly: **no NuGet package, local feed, new dependency, provider
credentials, or file access is required**. The solution builds this sample on
`net8.0` and `net10.0`; .NET 10 is recommended.

## Build and run

From the repository root, with the .NET SDK and framework targeting packs installed:

```sh
dotnet build Blazor.Ink.sln -c Release
dotnet run --project samples/ComponentDemo -c Release -f net10.0 --no-build
```

On the development machine, `/usr/local/share/dotnet/dotnet` has both runtimes
and targeting packs. Use that executable instead of `dotnet` to run the complete
two-framework validation.

## Pages and controls

Ctrl+N/P changes pages, Up/Down scrolls, and Esc/Ctrl+C exits. A highlighted page
name and visible keyboard help do not rely on color alone. Terminal size is read
once at startup; restart after resizing, or supply `--columns` and `--rows`.

| Page | Demonstrates | Extra controls |
|---|---|---|
| Chat | Lela-style conversation, purple composer, local replies, thinking text and attachment labels | Enter sends; Ctrl+T toggles thinking; Ctrl+L or `/clear` clears; `/add path` adds a label |
| Text | Decorations, nested styling, colors/backgrounds, every supported wrapping/truncation mode, CJK and emoji | Up/Down scrolls |
| Box | All seven borders, flex layouts, percentages, spacing, backgrounds, clipping and content offsets | Up/Down scrolls |
| Static | Append-once completed tasks, retained across navigation and live clears | Enter appends a task |
| Transform | Uppercase and per-line numbering | |
| Newline | Default and multiple line breaks inside Text | |
| Spacer | Horizontal and vertical distribution of free space | |
| Runtime | Input/paste events and availability, coordinated output, clearing, flushing, rerender and exit | `o` stdout, `e` stderr, `c` clear live, `f` flush, `r` rerender |

Chat is a component demo, **not an AI connection**. Attachments are deduplicated
labels; their paths are never opened. Bracketed paste becomes literal draft text,
with newlines/tabs normalized to spaces and terminal controls stripped. Pasted
Enter/Ctrl+C cannot submit or exit. Backspace/Delete removes a complete Unicode
grapheme, including joined emoji and combining accents.

Static remains mounted outside page selection. Completed items print once above
the live UI; clearing chat or live output does not erase/replay scrollback.
Runtime `c` clears the live region until the next redraw. Logs go through the
session writer, never direct Console writes while a session owns the terminal.

## Snapshots and scripted input

```sh
dotnet run --project samples/ComponentDemo -c Release -f net10.0 --no-build -- --snapshot --all
dotnet run --project samples/ComponentDemo -c Release -f net10.0 --no-build -- --snapshot --page Text --columns 40
dotnet run --project samples/ComponentDemo -c Release -f net10.0 --no-build -- --page Runtime --columns 100 --rows 24
dotnet run --project samples/ComponentDemo -c Release -f net10.0 --no-build -- --scripted
```

Without usable terminal input/output (including `CI` and `TERM=dumb`), the default
is a plain, full-content snapshot that exits without subscribing to input.
`--snapshot --all` renders every page; `--all` requires `--snapshot`.
`--scripted` feeds a finite byte stream through the real input subscriptions,
including chat commands, literal paste, Static tasks and runtime actions. Its
default walkthrough starts on Chat and exits without credentials or a TTY.
`--check`, `--scripted` and `--snapshot` are mutually exclusive.
`--help` lists the command-line options. Invalid options/dimensions and runtime
failures produce a diagnostic on stderr and a nonzero exit code.

## Validation

```sh
dotnet run --project samples/ComponentDemo -c Release -f net10.0 -- --check
bash scripts/verify.sh
bash scripts/check-native-input.sh
```

The framework-free checks render every page at 40 and 100 columns and exercise
navigation, scrolling, chat commands, Unicode deletion, literal paste, Static
history, runtime actions, short-terminal layout, CLI validation and input failure.
Full verification runs both frameworks and preserves the existing independent
NuGet consumer gate. Snapshots and scripted output are saved under
`artifacts/component-demo/`.

Input subscriptions are disposed with the component; sessions restore terminal
settings and cursor visibility. The native gate is platform-specific:
Windows/Linux remain unverified unless their own gates run.

This sample covers the currently implemented components and runtime APIs, not
every combination of the 66 style fields. It adds no focus manager, cursor intent,
animation scheduler, resize service, accessibility API, Markdown renderer, tool
execution or provider integration. Scroll bounds use a small headless render of
the content because the preview does not yet expose element metrics.
