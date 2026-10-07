# Blazor.Ink technical requirements document

> Historical research/design document carried into the standalone export.
> Current package identity, dependency acquisition, and release policy are in
> [Publishing Blazor.Ink](publishing.md); earlier vendor acquisition notes do not
> describe the dependencies shipped by this repository.

## Product and release contract

Reusable Razor/Blazor terminal components with Ink 8 terminal behavior.
Core and agent UI are separate packages. Agent execution, providers, MCP,
persistence, browser rendering, and NativeAOT certification are excluded.
React hooks, React concurrent scheduling, Suspense, and React DevTools are
replaced by Blazor-native mechanisms, not reproduced.

Targets: `net8.0;net10.0`; Windows, macOS, Linux. .NET 10 is recommended.
.NET 8 is legacy compatibility after its official 2026-11-10 end-of-support date.
The package working names are `Blazor.Ink` and `Blazor.Ink.AgentUI`; availability is
checked before publication. Initial builds are explicitly preview builds.

## Architecture

`Razor → Blazor Renderer → committed terminal tree → Yoga → styled cells → single serialized writer`.

Blazor owns component lifecycle, DI, component state, callbacks, and keyed
reconciliation. Consume RenderBatch data synchronously: its arrays must not be
retained beyond UpdateDisplayAsync. Isolate RenderTree-sensitive code in one
adapter tested on both targets. Handle insert/remove, step-in/out, attributes,
text, markup, permutations, component regions, and disposal.

Each host owns its services, layout, output state, subscriptions, and dispatcher.
Do not reuse session-scoped services across live hosts. Each output stream has
one owner. Native terminal settings and Console redirection are process-global;
reject competing owners and restore the exact previous state.

Use Yoga's one-column point scale, LTR layout, and Ink defaults:
Box direction row, grow 0, shrink 1, nowrap, stretch items, start justification.
Nested Text is inline content, not an independent Yoga child.
Text/transform changes invalidate enclosing text measurements.
Style removal must restore defaults rather than retain previous values.

## Public API contract

Namespace `Ink`; components in `Blazor.Ink.Components`.

- `InkHost.RenderAsync<TComponent>(InkOptions? options = null,
  ParameterView? parameters = null, IServiceProvider? services = null,
  CancellationToken cancellationToken = default)`
  returns an `InkSession`, without an HTTP server or browser.
- `InkHost.RenderToStringAsync<TComponent>(int columns = 80, ParameterView)`
  renders headlessly and disposes all resources. No terminal side effects.
  The preview also accepts a caller-supplied `IServiceProvider`; it does not
  dispose that provider or its scope. Without one, the host owns an empty
  provider. Component disposal remains the renderer's responsibility.
- `InkSession`: rerender root parameters, clear, await output flush, exit with
  result/error, await exit, write stdout/stderr, suspend, and async disposal.
- `InkInput`: disposable subscriptions for key/text and paste events; active
  flags prevent unwanted input. Global subscriptions broadcast as Ink does;
  widgets apply their own focus filter.
- `InkFocus`: registration, active state, explicit focus, next/previous,
  enable/disable. Duplicate IDs are one navigation slot.
- `InkTerminal`: cursor intent, dimensions, resize notifications, input/output
  access, raw-mode capability, and accessibility mode.
- Element/box metrics expose dimensions, parent-relative left/top,
  root-relative x/y, border-excluded client sizes, and first-measure status.
- Animation exposes frame, elapsed time, delta, reset, interval, active state.
  Animations share a scheduler and stop on disposal.

Components: `Box`, `Text`, `Newline`, `Spacer`, `Static<TItem>`, `Transform`.
Box uses typed style properties and optional style object; explicit properties
override object values. Text owns literal content, nested styling, wrapping,
color, background, decorations, and accessibility label/hidden state.
Static uses a typed render template with item/index and stable Blazor keys.
Transform accepts `(string content, int lineIndex) → string`.

Razor formatting whitespace outside Text is ignored. Non-whitespace raw text
outside Text and Box inside Text are errors. Plain model text is never interpreted
as markup. Unsupported preview capabilities must be documented or rejected;
silent no-op APIs do not satisfy this TRD.

## Output and lifecycle invariants

- Primary screen is default. Alternate-screen teardown discards alternate
  content and restores the original screen.
- Static items are emitted only after commit, once per committed append.
  Remount/removal changes cached static identity; abandoned work never emits.
- Preserve grapheme boundaries in widths, wrapping, truncation, clipping,
  incremental prefixes, and editing. Wide cells cannot leave half glyphs.
- Support all Ink border presets, custom border characters, side-specific
  colors/backgrounds/dimming, background inheritance, and content offsets.
- Default frame limit 30 FPS; animation interval 100 ms. Coalesce dynamic
  updates; never lose static commits or input. Debug/accessibility behavior
  follows the compatibility fixtures.
- CI/redirected output does not emit cursor, erase, mode, or negotiation
  sequences. Non-debug mode writes the final frame at shutdown.
- AwaitFlush completes after queued render and output work reaches the writer,
  not merely after component rendering. Failed writes settle pending tasks.
- External output temporarily clears and restores live output without
  duplicating static history.
- Suspend drains pending output, releases input/protocols/screen ownership, and
  restores state plus a full redraw even when its callback throws.
- Cleanup is idempotent. Normal exit, cancellation, component errors, Ctrl+C,
  and handled shutdown signals restore modes/cursor and release subscriptions.
  Uncatchable process termination cannot guarantee restoration.

### Output-session preview contract (implemented subset)

`InkOptions` currently exposes `Stdout`, `Stderr`, `Columns` (80), `Rows` (24),
nullable `Interactive`, `IncrementalRendering`, and `HideCursor` (true).
The input foundation adds nullable `Stdin`, `ExitOnCtrlC` (true), session-scoped
`InkInput`, typed events/legacy keys, and disposable subscriptions with mutable
`IsActive`. Raw input starts only while subscribed; custom byte streams never
change native modes. Remaining render-option counterparts below are release
requirements, not inert preview properties. Output ownership is by TextWriter
reference, not underlying
OS handle; wrappers around the same terminal must not be used for concurrent
sessions. Input ownership is exclusive by custom stream reference or native stdin
lease. Console redirection remains unimplemented.

The committed adapter retains node identity by Blazor component ID and scoped
frame/key identity, with O(n) snapshots and rebuilt Yoga geometry. Full-line
incremental output falls back to full live-region redraw on height changes or
Static append. At most `Rows - 1` trailing live rows are shown; this avoids
cursor-up erasure of scrolled Static. Explicit geometry is required until resize.
Static consumes append indices, preserving blank rows and ancestor backgrounds;
consumed children are removed/disposed. A remount is a new commit identity.

Writes, clears, flushes, Static and live output use one ordered queue. Limits:
256 pending operations, 16,000,000 pending source characters, and 1,000,000
characters per external log call. Overflow is a visible session failure, not
silent Static loss. No dynamic frame coalescing/FPS cap is implemented yet.
Noninteractive output is plain Static plus final live, with LF line separators.
Interactive rows use CRLF and safe whole-line erase. Virtual-terminal checks
cover these controls; real OS PTY certification remains open.
Interactive startup establishes a fresh CRLF boundary, preserving any partial
existing history without needing an input-driven cursor query. This can add a
blank line when the terminal was already at column zero. Host admission is also
capped at 256 pending operations, including waiting flush/lifecycle barriers;
bounded writer admission alone does not bound those dependencies.

`AwaitFlushAsync` includes previously admitted host operations and an output/
dispatcher barrier, not arbitrary future background tasks.
From component lifecycle code, flush includes already committed output only,
avoiding a cycle with the host operation awaiting that lifecycle.
Lifecycle origin flows through `ExecutionContext`/`AsyncLocal`, including
`ConfigureAwait(false)` continuations, rather than relying only on dispatcher access.
`RequestExit` signals shutdown from component code without awaiting its own disposal; `ExitAsync`
and `DisposeAsync` await complete cleanup. `StoppingToken` lets components
cooperatively stop background work. Teardown waits for admitted lifecycle work
and in-flight output before releasing writer leases. Uncooperative user code can
therefore delay exit indefinitely; abandoning a writer while it might still write
is explicitly forbidden. Caller services/writers are never disposed by the host.
Cursor hiding currently restores visible on exit, not a queried prior visibility
state. Native input configuration restoration has platform-specific gates; macOS
normalizes kernel-owned PENDIN bookkeeping. Broad signals, prior protocol state
and cursor visibility restoration remain separate gates.

## Input and trust boundaries

Incrementally decode UTF-8, CSI/SS3, Alt keys, and paste across arbitrary chunks.
Escape timeout follows the pinned Ink fixture; bracketed paste is never timeout
flushed. Paste is a separate channel when subscribed, otherwise follows Ink's
input fallback. Pasted Ctrl+C/Enter are literal data, not commands.

Support Kitty optional negotiation, 200 ms auto-query timeout, all five request
flags, all eight modifier bits, and press/repeat/release. Associated-text requests
also enable all-key reporting. Unknown terminal replies are discarded, not
printed as typed text. Suspension cancels pending negotiation.

Untrusted agent text has control sequences removed before rendering, including
OSC clipboard operations, cursor movement, and terminal mode changes. Styled
author text may use the documented SGR/link API; only the terminal owner emits
mode controls. Validate dimensions, percentages, intervals, and callback inputs.
No widget executes a command or grants a permission itself.

Preview resource limits are enforced before recursive layout/allocation:
64 levels, 10,000 terminal nodes, 1,000,000 markup characters, 16,384 columns/
rows, and 1,000,000 canvas cells. Excess nesting throws NotSupportedException;
invalid dimensions/cell counts throw ArgumentOutOfRangeException. Raising
these limits requires solver stack and memory evidence, not an unchecked option.

## Agent UI contract

Separate package: chat transcript, streaming Markdown, code highlighting,
multiline grapheme-safe prompt editor, tool progress/status, approval dialog,
and command picker. Widgets expose state and callbacks rather than own an agent.
Markdown is parsed with Markdig and rendered as terminal content, never HTML.
Reuse Spectre renderables offline; do not start its live/input loop.

Scripted demo is deterministic and credential-free. Provider integration, if
added, belongs in a sample. Cancellation, bounded displayed history, focus
transitions, and malicious generated text require executable checks.

## Acceptance and evidence

Port selected Ink fixtures with literal expected output, then grow the shared
corpus until every terminal requirement has a passing differential test.
Compare geometry, styled cells, and reconstructed terminal state—not just final
plain text. Raw ANSI may differ only if reconstructed style/cursor state agrees.

Renderer acceptance: keyed reorder, regions, async lifecycle, event dispatch,
markup changes, references, errors, and disposal on both frameworks.
Text acceptance: ASCII/CJK/emoji/combining marks/ZWJ/variation selectors,
truncation at zero/one columns, clipping intersections, styling and overlaps.
Input acceptance: every chunk split, paste/control literals, replies, Kitty.
Lifecycle acceptance: static/remount, resize, external writes, slow/broken
writers, suspension failure, repeated disposal, pipe/CI.
Platform acceptance requires native PTY/ConPTY checks on Windows, macOS, Linux.

Record benchmark machine and workload. Measure default 30 FPS and input
responsiveness before selecting an optimization. No publication, AOT, or full
parity claim without evidence for the respective release gate.

## Compatibility inventory

The tables below enumerate the pinned Ink source surface. Test identifiers are
acceptance groups, not claims that tests already exist. Implementation status is
maintained in the roadmap and package README.

### Every exported symbol

| Ink export | .NET mapping | Acceptance group |
|---|---|---|
| `RenderOptions` | `InkOptions` | API: exported counterpart and behavior |
| `Instance` | `InkSession` | API: exported counterpart and behavior |
| `render` | `InkHost.RenderAsync` | API: exported counterpart and behavior |
| `RenderToStringOptions` | `headless columns/parameters` | API: exported counterpart and behavior |
| `renderToString` | `InkHost.RenderToStringAsync` | RENDER: exported counterpart and behavior |
| `BoxProps` | `Box component parameters` | API: exported counterpart and behavior |
| `Box` | `Box` | RENDER: exported counterpart and behavior |
| `TextProps` | `Text component parameters` | API: exported counterpart and behavior |
| `Text` | `Text` | RENDER: exported counterpart and behavior |
| `AppProps` | `InkSession` context service contract; React context type excluded | API: service exit/suspend instead of context |
| `StdinProps` | `InkInput/InkTerminal` context service contract | INPUT: subscriptions/raw capability instead of context |
| `StdoutProps` | `InkSession/InkTerminal` context service contract | API: coordinated stdout/size instead of context |
| `StderrProps` | `InkSession` context service contract | API: coordinated stderr instead of context |
| `StaticProps` | `Static component parameters` | API: exported counterpart and behavior |
| `Static` | `Static` | API: exported counterpart and behavior |
| `TransformProps` | `Transform component parameters` | API: exported counterpart and behavior |
| `Transform` | `Transform` | RENDER: exported counterpart and behavior |
| `NewlineProps` | `Newline component parameters` | API: exported counterpart and behavior |
| `Newline` | `Newline` | API: exported counterpart and behavior |
| `Spacer` | `Spacer` | API: exported counterpart and behavior |
| `Key` | `Key` | INPUT: exported counterpart and behavior |
| `useInput` | `InkInput.Subscribe` | INPUT: exported counterpart and behavior |
| `usePaste` | `InkInput.SubscribePaste` | INPUT: exported counterpart and behavior |
| `SuspendTerminal` | `InkSession.SuspendAsync` | API: exported counterpart and behavior |
| `TerminalSuspension` | `IAsyncDisposable suspension` | API: exported counterpart and behavior |
| `useApp` | `InkSession` | API: exported counterpart and behavior |
| `useStdin` | `InkInput/InkTerminal` | INPUT: exported counterpart and behavior |
| `useStdout` | `InkSession.WriteAsync` | API: exported counterpart and behavior |
| `useStderr` | `InkSession.WriteErrorAsync` | API: exported counterpart and behavior |
| `useFocus` | `InkFocus.Register` | API: exported counterpart and behavior |
| `useFocusManager` | `InkFocus` | API: exported counterpart and behavior |
| `useIsScreenReaderEnabled` | `InkOptions.ScreenReader` | API: exported counterpart and behavior |
| `useCursor` | `InkTerminal.Cursor` | API: exported counterpart and behavior |
| `AnimationResult` | `AnimationResult` | API: exported counterpart and behavior |
| `useAnimation` | `InkAnimation` | API: exported counterpart and behavior |
| `WindowSize` | `WindowSize` | API: exported counterpart and behavior |
| `useWindowSize` | `InkTerminal.Size` | API: exported counterpart and behavior |
| `BoxMetrics` | `BoxMetrics` | API: exported counterpart and behavior |
| `UseBoxMetricsResult` | `UseBoxMetricsResult` | API: exported counterpart and behavior |
| `useBoxMetrics` | `Box.Metrics` | API: exported counterpart and behavior |
| `CursorPosition` | `CursorPosition` | API: exported counterpart and behavior |
| `measureElement` | `TerminalElement.Measure` | API: exported counterpart and behavior |
| `ElementMetrics` | `ElementMetrics` | API: exported counterpart and behavior |
| `DOMElement` | `TerminalElement` | RENDER: exported counterpart and behavior |
| `kittyFlags` | `KittyFlags` | INPUT: exported counterpart and behavior |
| `kittyModifiers` | `KeyModifiers` | INPUT: exported counterpart and behavior |
| `KittyKeyboardOptions` | `KittyOptions` | INPUT: exported counterpart and behavior |
| `KittyFlagName` | `KittyFlags` | INPUT: exported counterpart and behavior |

### Every style property

| Ink property | Typed .NET counterpart | Acceptance group |
|---|---|---|
| `textWrap` | `TextWrap` | TEXT: set/update/remove, Ink default and precedence |
| `position` | `Position` | LAYOUT: set/update/remove, Ink default and precedence |
| `top` | `Top` | LAYOUT: set/update/remove, Ink default and precedence |
| `right` | `Right` | LAYOUT: set/update/remove, Ink default and precedence |
| `bottom` | `Bottom` | LAYOUT: set/update/remove, Ink default and precedence |
| `left` | `Left` | LAYOUT: set/update/remove, Ink default and precedence |
| `columnGap` | `ColumnGap` | LAYOUT: set/update/remove, Ink default and precedence |
| `rowGap` | `RowGap` | LAYOUT: set/update/remove, Ink default and precedence |
| `gap` | `Gap` | LAYOUT: set/update/remove, Ink default and precedence |
| `margin` | `Margin` | LAYOUT: set/update/remove, Ink default and precedence |
| `marginX` | `MarginX` | LAYOUT: set/update/remove, Ink default and precedence |
| `marginY` | `MarginY` | LAYOUT: set/update/remove, Ink default and precedence |
| `marginTop` | `MarginTop` | LAYOUT: set/update/remove, Ink default and precedence |
| `marginBottom` | `MarginBottom` | LAYOUT: set/update/remove, Ink default and precedence |
| `marginLeft` | `MarginLeft` | LAYOUT: set/update/remove, Ink default and precedence |
| `marginRight` | `MarginRight` | LAYOUT: set/update/remove, Ink default and precedence |
| `padding` | `Padding` | LAYOUT: set/update/remove, Ink default and precedence |
| `paddingX` | `PaddingX` | LAYOUT: set/update/remove, Ink default and precedence |
| `paddingY` | `PaddingY` | LAYOUT: set/update/remove, Ink default and precedence |
| `paddingTop` | `PaddingTop` | LAYOUT: set/update/remove, Ink default and precedence |
| `paddingBottom` | `PaddingBottom` | LAYOUT: set/update/remove, Ink default and precedence |
| `paddingLeft` | `PaddingLeft` | LAYOUT: set/update/remove, Ink default and precedence |
| `paddingRight` | `PaddingRight` | LAYOUT: set/update/remove, Ink default and precedence |
| `flexGrow` | `FlexGrow` | LAYOUT: set/update/remove, Ink default and precedence |
| `flexShrink` | `FlexShrink` | LAYOUT: set/update/remove, Ink default and precedence |
| `flexDirection` | `FlexDirection` | LAYOUT: set/update/remove, Ink default and precedence |
| `flexBasis` | `FlexBasis` | LAYOUT: set/update/remove, Ink default and precedence |
| `flexWrap` | `FlexWrap` | LAYOUT: set/update/remove, Ink default and precedence |
| `alignItems` | `AlignItems` | LAYOUT: set/update/remove, Ink default and precedence |
| `alignSelf` | `AlignSelf` | LAYOUT: set/update/remove, Ink default and precedence |
| `alignContent` | `AlignContent` | LAYOUT: set/update/remove, Ink default and precedence |
| `justifyContent` | `JustifyContent` | LAYOUT: set/update/remove, Ink default and precedence |
| `width` | `Width` | LAYOUT: set/update/remove, Ink default and precedence |
| `height` | `Height` | LAYOUT: set/update/remove, Ink default and precedence |
| `minWidth` | `MinWidth` | LAYOUT: set/update/remove, Ink default and precedence |
| `minHeight` | `MinHeight` | LAYOUT: set/update/remove, Ink default and precedence |
| `maxWidth` | `MaxWidth` | LAYOUT: set/update/remove, Ink default and precedence |
| `maxHeight` | `MaxHeight` | LAYOUT: set/update/remove, Ink default and precedence |
| `aspectRatio` | `AspectRatio` | LAYOUT: set/update/remove, Ink default and precedence |
| `display` | `Display` | LAYOUT: set/update/remove, Ink default and precedence |
| `borderStyle` | `BorderStyle` | PAINT: set/update/remove, Ink default and precedence |
| `borderTop` | `BorderTop` | PAINT: set/update/remove, Ink default and precedence |
| `borderBottom` | `BorderBottom` | PAINT: set/update/remove, Ink default and precedence |
| `borderLeft` | `BorderLeft` | PAINT: set/update/remove, Ink default and precedence |
| `borderRight` | `BorderRight` | PAINT: set/update/remove, Ink default and precedence |
| `borderColor` | `BorderColor` | PAINT: set/update/remove, Ink default and precedence |
| `borderTopColor` | `BorderTopColor` | PAINT: set/update/remove, Ink default and precedence |
| `borderBottomColor` | `BorderBottomColor` | PAINT: set/update/remove, Ink default and precedence |
| `borderLeftColor` | `BorderLeftColor` | PAINT: set/update/remove, Ink default and precedence |
| `borderRightColor` | `BorderRightColor` | PAINT: set/update/remove, Ink default and precedence |
| `borderDimColor` | `BorderDimColor` | PAINT: set/update/remove, Ink default and precedence |
| `borderTopDimColor` | `BorderTopDimColor` | PAINT: set/update/remove, Ink default and precedence |
| `borderBottomDimColor` | `BorderBottomDimColor` | PAINT: set/update/remove, Ink default and precedence |
| `borderLeftDimColor` | `BorderLeftDimColor` | PAINT: set/update/remove, Ink default and precedence |
| `borderRightDimColor` | `BorderRightDimColor` | PAINT: set/update/remove, Ink default and precedence |
| `borderBackgroundColor` | `BorderBackgroundColor` | PAINT: set/update/remove, Ink default and precedence |
| `borderTopBackgroundColor` | `BorderTopBackgroundColor` | PAINT: set/update/remove, Ink default and precedence |
| `borderBottomBackgroundColor` | `BorderBottomBackgroundColor` | PAINT: set/update/remove, Ink default and precedence |
| `borderLeftBackgroundColor` | `BorderLeftBackgroundColor` | PAINT: set/update/remove, Ink default and precedence |
| `borderRightBackgroundColor` | `BorderRightBackgroundColor` | PAINT: set/update/remove, Ink default and precedence |
| `overflow` | `Overflow` | PAINT: set/update/remove, Ink default and precedence |
| `overflowX` | `OverflowX` | PAINT: set/update/remove, Ink default and precedence |
| `overflowY` | `OverflowY` | PAINT: set/update/remove, Ink default and precedence |
| `contentOffsetX` | `ContentOffsetX` | PAINT: set/update/remove, Ink default and precedence |
| `contentOffsetY` | `ContentOffsetY` | PAINT: set/update/remove, Ink default and precedence |
| `backgroundColor` | `BackgroundColor` | PAINT: set/update/remove, Ink default and precedence |

### Every render option

| Ink option | .NET counterpart | Acceptance group |
|---|---|---|
| `stdout` | `InkOptions.Stdout` | OPTIONS: default/override/non-TTY/teardown |
| `stdin` | `InkOptions.Stdin` | OPTIONS: default/override/non-TTY/teardown |
| `stderr` | `InkOptions.Stderr` | OPTIONS: default/override/non-TTY/teardown |
| `debug` | `InkOptions.Debug` | OPTIONS: default/override/non-TTY/teardown |
| `exitOnCtrlC` | `InkOptions.ExitOnCtrlC` | OPTIONS: default/override/non-TTY/teardown |
| `patchConsole` | `InkOptions.PatchConsole` | OPTIONS: default/override/non-TTY/teardown |
| `onRender` | `InkOptions.OnRender` | OPTIONS: default/override/non-TTY/teardown |
| `isScreenReaderEnabled` | `InkOptions.IsScreenReaderEnabled` | OPTIONS: default/override/non-TTY/teardown |
| `maxFps` | `InkOptions.MaxFps` | OPTIONS: default/override/non-TTY/teardown |
| `incrementalRendering` | `InkOptions.IncrementalRendering` | OPTIONS: default/override/non-TTY/teardown |
| `concurrent` | Blazor Dispatcher; React concurrency excluded | OPTIONS: default/override/non-TTY/teardown |
| `kittyKeyboard` | `InkOptions.KittyKeyboard` | OPTIONS: default/override/non-TTY/teardown |
| `interactive` | `InkOptions.Interactive` | OPTIONS: default/override/non-TTY/teardown |
| `alternateScreen` | `InkOptions.AlternateScreen` | OPTIONS: default/override/non-TTY/teardown |

### Terminal behavior matrix

| Behavior | Acceptance check |
|---|---|
| Primary/alternate screen | mode enter/leave and disposable teardown |
| Static commits | append, hidden ancestor, remount, removal, blank rows |
| UTF-8/CSI/SS3 | every packet split and legacy keys |
| Bracketed paste | split markers, literal controls, subscription fallback |
| Kitty | flags/modifiers/events/associated text/query timeout/suspend |
| Focus | ordering, inactive, duplicate IDs, removal, Esc/Tab |
| Cursor/IME | cursor-only frame and disposal |
| Resize | growth/shrink/fullscreen/Windows bottom-right |
| Output modes | debug, CI, pipe, color, screen reader |
| External writes | stdout/stderr/Console clear-and-restore |
| Flush/exit | slow writer, broken writer, pending tasks, idempotence |
| Suspension | callback failure, nested ownership, protocol cancellation |
| Animation/metrics | shared clock, interval reset, detached and sibling-driven metrics |
| ANSI/grapheme safety | styled prefix fallback, C0/C1/OSC/SGR, wide overlap |
| Stream isolation | separate hosts; reject competing stream ownership |
