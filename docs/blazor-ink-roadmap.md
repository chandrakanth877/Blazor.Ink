# Blazor.Ink implementation roadmap

> Historical research/design document carried into the standalone export.
> Current package identity, dependency acquisition, and release policy are in
> [Publishing Blazor.Ink](publishing.md); earlier vendor acquisition notes do not
> describe the dependencies shipped by this repository.

## Schedule

One experienced developer assisted by AI, full-time: 20 engineering weeks plus
four contingency weeks. These are relative planning estimates, not elapsed
time or a fixed delivery commitment.

| Phase | Weeks | Deliverable / gate |
|---|---|---|
| 0 | 1 | Review, TRD, complete surface inventory, roadmap, dependency provenance |
| 1 | 2 | Dependency restore/source fallback, Yoga measurement, Blazor edits, widths, native restoration feasibility |
| 2 | 3–5 | Component host, keyed tree, styles, core components, headless output |
| 3 | 6–8 | Styled cells, borders/backgrounds, transforms, wrapping/clipping, both repaint modes |
| 4 | 9–11 | Native input, paste, Kitty, focus, cursor; interactive preview |
| 5 | 12–14 | Static history, resize, suspension, logging, flush/exit, accessibility, animation, metrics |
| 6 | 15–17 | Agent widgets and deterministic demo; agent UI preview |
| 7 | 18–20 | Full conformance, platform/soak/performance gates, docs and package artifacts |
| Buffer | 21–24 | Framework/dependency/Unicode/terminal discrepancies |

Milestones: layout preview week 5; interactive preview week 11; agent UI preview
week 17; full-parity release week 20 with contingency through week 24.

## Work packages and runnable gates

1. Preserve the reference checkout and create the three documents.
2. Acquire a pinned Yoga dependency, compile both targets, and check grow,
   shrink, gap, percentages, dirty text measurement, and disposal.
3. Host real Razor components. Check literal text, keyed reorder/removal,
   nested regions, asynchronous state changes, markup updates, and errors.
4. Build geometry and text output together. Check the literal Ink text-width,
   border, clip, transform, and wrap regressions.
5. Add a single writer and virtual terminal. Check changed/shrinking lines,
   cursor-only updates, final piped output, flushing, and failures.
6. Add native input ownership with packet fixtures. Check UTF-8, keys,
   bracketed paste, Kitty, focus, and restoration.
7. Add static commits, metrics, animation, resize, suspension, and logging.
   Check failure/teardown and cancelled/in-flight work.
8. Add separately packaged agent widgets and credential-free demo.
   Check callbacks, streaming/cancellation, and control-sequence injection.
9. Run the shared differential corpus and OS terminal matrix; pack artifacts
   locally and consume them in a clean application. Do not publish automatically.

Use a small executable check harness initially; introduce additional test
infrastructure only when the conformance workload needs it.

## Phase 1 decision rule

Prefer verified Yoga.Net NuGet restore. If unreachable, vendor the MIT source at
`baf14fcd6cbf21d8930a297e32ef3b76674c37bd` with provenance and original license.
Adopt only after the required flex and measurement fixtures pass. Localized
corrections require regression evidence; extensive discrepancies trigger a new
estimate. Do not substitute a hand-written layout engine.

## Risk register

| Risk | Mitigation / gate |
|---|---|
| NuGet unavailable or package identity ambiguous | Exact cached versions or pinned licensed source; record what was actually restored |
| Framework RenderTree differences | One adapter; real Razor edit tests on net8 and net10 |
| Grapheme-width mismatch | Literal Ink cases and shared differential corpus |
| Keyed/static identity errors | Reorder/remount/append and abandoned-work checks |
| Native mode corruption | Platform-specific PTY checks and exact mode restoration |
| Console redirection collision | Exclusive ownership, opt-in tests, restore previous writers |
| Slow/broken output hangs | Serialized asynchronous writes; settle flush/exit promises on errors |
| Agent output injection | Strip controls before parsing/rendering; malicious text fixtures |
| .NET 8 support ends | Explicit legacy compatibility label; .NET 10 recommended |

## Release checklist

- [ ] Every compatibility row implemented or explicitly mapped to a Blazor-only difference.
- [ ] Shared Ink differential corpus passes.
- [ ] Both target frameworks pass.
- [ ] Windows, macOS, Linux PTY/ConPTY and redirected-output checks pass.
- [ ] Slow/broken output and suspension/shutdown failure checks pass.
- [ ] Agent malicious-text, editing, streaming and cancellation checks pass.
- [ ] Performance/soak workload and machine recorded.
- [ ] Exact dependency versions, license notices, package consumption verified.
- [ ] NuGet name availability checked; packages remain unpublished until authorized.

## Execution ledger

- Documents created before production code.
- Ruling: this is a new sibling solution, not a modification on Ink's master
  branch. Preserve the upstream checkout and do not create a worktree for it.
- Ruling: the approved roadmap spans many independent release gates. Deliver
  runnable increments and mark remaining gates honestly; a local preview does
  not constitute a full-parity release.
- Ruling: NuGet DNS resolution failed even with approved network access.
  Use pinned, unmodified MIT source snapshots of Yoga.Net and Wcwidth with
  original notices rather than invent package versions. Cost if wrong:
  wrapper package migration and additional dependency-conformance work.
- Ruling: committed tree snapshots are rebuilt in O(n), relying on Blazor for
  keyed component identity. The keyed permutation/removal checks pass, but
  persistent terminal-node identity for Static/metrics is still required.
  Cost: rebuild overhead and a future persistent-node adapter before those gates.
- 2026-10-07: the research workspace's sibling .NET solution was created with core, executable
  checks, layout sample, and source-wrapper projects. Targets net8/net10.
- Implementation so far: custom headless Blazor host, typed core components
  and 66-field style mapping, Yoga geometry, grapheme widths, wrapping/
  truncation, styled canvas, backgrounds/borders, transforms, and headless Static.
  This is **partial Phase 1–3 work**, not Phase 2 or 3 completion.
- Observed RED→GREEN: missing headless rendering; joined-emoji measurement;
  scalar Blazor attribute encoding; line-index transforms; headless Static
  separation; widest-line measurement; Text backgrounds; disposal-error
  propagation; DCS BEL payload leakage; Box background painting.
- Verification command:
  `bash scripts/verify.sh`.
  Initial full run: 48 executable assertions pass on .NET 8.0.28 and 10.0.9;
  layout demo runs on both; three preview NuGet packages are produced locally.
- Separate package-consumption command:
  `bash scripts/check-packages.sh`.
  PackageReference-only consumer restores solely from the local package source
  into an isolated cache and renders the expected Unicode border on both targets.
- Inventory check: 48 distinct exports, 66 distinct style properties, and
  14 distinct render options documented. Catalog entries are acceptance
  specifications, not assertions that every counterpart is implemented.

### Remaining immediate gates

This list records the headless checkpoint; the resumed output-session work below
supersedes items 1 and 3 for its tested subset.

1. Close fresh-review correctness findings with executable regressions.
2. Expand Yoga feasibility to the complete required flex/measurement corpus;
   preview adoption is provisional, not a blanket upstream-equivalence claim.
3. Preserve persistent terminal-node identity and implement the single writer/
   virtual-terminal oracle before live sessions and append-only Static.
4. Continue native input and ownership work only with OS-specific restoration
   tests. There is no raw-mode or cursor state to restore in this headless preview.
5. Phases 4–7 remain open: input/Kitty/focus, full lifecycle, agent widgets/demo,
   complete differential/platform/performance certification.

### Fresh review and fix-pass evidence

- Read-only independent review reproduced seven Important findings. It also
  confirmed snapshot arrays are consumed synchronously, async disposal works,
  and vendored source/license bytes match the pinned archives.
- Fixed repeated-SGR memory amplification: plain/styled stress checks cover
  8,000 repeated color commands and enforce allocation under 16 MB, rather
  than the original roughly 323 MB. Effective style families are bounded.
- Fixed recursion exposure: the original 256-deep regression aborted inside
  Yoga; it now throws a catchable NotSupportedException before layout.
  Preview limits: depth 64, 10,000 nodes, 1,000,000 markup characters,
  dimensions 16,384, and 1,000,000 canvas cells.
- Fixed dimension exposure: the original enormous-column check aborted with
  out-of-memory; columns, calculated height, and cell product now reject
  invalid sizes before allocation.
- Fixed grapheme boundaries: ZWJ emoji, wide combining marks and variation
  selectors across separate frames now pass; styled accents use their base
  cell's style without changing the following text's color.
- Fixed root flow: the A/B fragment check was `AB`, now `A\nB`.
- Fixed constrained truncation: the max-width CJK check was `你…|`, now
  `你… |`; the supplied width is reserved only when truncation is needed.
- Fixed application DI: added a caller-supplied IServiceProvider parameter
  after the caller-contract check failed compilation. Rendering/injection and
  retained caller ownership now have executable checks.
- Regraded Static-inside-Text as Important, not Minor: it bypassed the documented
  nesting invariant. Validate the complete tree before extracting Static;
  the invalid nesting check now throws.
- Final full verification: **78 assertions pass on each of .NET 8.0.28 and
  10.0.9**, layout samples run, preview packages build, and a fresh-cache
  PackageReference-only consumer passes on both runtimes. No packages published.
- Reference checkout verified at `26d2c3f83008142061c22267482489588cc3823c`
  with an empty `git status --short`.

Deferred minor findings (also listed in README): four-space tab expansion
instead of eight-column stops; Static drops a blank dynamic row; Static loses
an extracted ancestor's background. These are headless parity gaps and must
not be hidden behind React-only exclusions.

No findings were waived as implemented. No full-parity, platform-matrix,
performance, or native-restoration claim is made.

### Resumed increment: session/output foundation

Work order: preserve committed terminal-node identity; carry row counts and
inherited Static backgrounds; capture append-only commits; introduce a single
serialized writer and session API; reconstruct terminal state in runnable
checks; then review and package. Native input and the agent package remain
later gates, not placeholders in this increment.

Ruling: use Blazor component IDs plus scoped frame/key identities to preserve
terminal nodes while retaining snapshot traversal. This avoids implementing a
second incomplete edit switch. Geometry may still be rebuilt until profiling
justifies caching; identity and layout caching are separate responsibilities.

Implemented in this increment:

- `InkOptions`, `InkHost.RenderAsync`, `InkSession` root-parameter updates,
  clear, stdout/stderr writes, flush, exit/result, cancellation and async disposal.
- Injected session and cooperative component lifetime token. `RequestExit`
  signals exit without asking lifecycle code to await its own disposal.
- An ordered bounded writer, full/whole-line incremental redraw,
  unchanged-frame suppression, cursor visibility and final plain piped output.
- Static append indices, persistent identities, blank rows and inherited
  backgrounds; consumed item components are released.
- A small virtual-terminal oracle and deterministic output-only session demo.
  This demo is **not** the Phase 6 agent-widget demo.
- Clean package consumers now exercise both headless layout and session output.

Ruling: display at most `Rows - 1` trailing live rows until resize/fullscreen
work is implemented — prevents redraw cursor-up from erasing Static that has
scrolled outside the viewport — cost: clipped leading live content in this
preview, not complete fullscreen parity.

Ruling: bound pending output to 256 operations and 16,000,000 source characters;
overflow fails the session rather than losing Static. Do not implement a second
scheduler/coalescer before the frame-rate gate — cost: bursty producers can
hit an explicit preview failure instead of the eventual coalesced 30 FPS path.

Ruling: teardown retains writer leases until admitted lifecycle work and actual
output settle; cancel cooperative work but never abandon an uncooperative writer
that can still emit — cost: uncooperative application code can delay shutdown.
Components receive `StoppingToken` to make their own work cooperative.

Observed RED→GREEN in this increment:

- Session API assertions initially failed compilation because no runtime host
  existed; the full/redraw, Static/logging, ownership and shutdown checks passed
  after implementation.
- Failed display writes arrived as single-inner AggregateException from Blazor;
  normalize them at the adapter boundary to preserve IOException/cancellation.
- Shutdown released ownership before an admitted async parameter lifecycle
  completed; drain admitted operations before disposal/close/release.
- Flush passed a dispatcher barrier while admitted async lifecycle work was
  pending; include prior admitted host operations in the barrier.
- The plain-output oracle originally modeled LF without normal line separation;
  normalize LF for redirected output (interactive output explicitly uses CRLF).

Pre-review full verification passed **142 assertions on each target**, both
layout/output samples, local packing, and fresh-cache PackageReference consumers.
Backlog-limit, hidden/shrinking Static, component-flush and keyed node identity
checks were added before the final checkpoint. This is **partial Phase 1–3 and Phase 5 work**.
Native-input feasibility and full terminal lifecycle/platform gates remain open.

### Resumed increment: independent review and fix pass

The fresh read-only reviewer reproduced four Important defects, no Critical
defects, and no additional minors. All four were reproduced together in the
workspace harness before any fixes:

- Final: fixed off-dispatcher lifecycle flush self-deadlock — initialization
  using `ConfigureAwait(false)` timed out RED; lifecycle origin now flows through
  AsyncLocal and the same mount/flush completes GREEN.
- Final: fixed own-hidden Static consumption — reveal produced no item RED;
  extraction now checks the node's own display. The component owns its append
  index (duplicate renderer counts removed); hidden/reveal and shrink checks GREEN.
- Final: fixed unbounded host barriers — 512 waiting flushes were accepted RED;
  admission now also caps host operations at 256 and visibly rejects overflow
  GREEN. Dependency snapshots are bounded; no unbounded O(n²) backlog remains.
- Final: fixed mid-line startup corruption — preexisting `pre` merged with a
  full-width live row RED; initial CRLF establishes a known boundary and preserves
  partial history through update/clear GREEN.
- Fix-pass suite: **158 assertions pass on .NET 8.0.28**. Final both-framework
  packaging verification is recorded below after it runs.

Ruling: start interactive output at a CRLF boundary without querying cursor
position — preserves partial history without requiring an input driver — cost:
an extra blank line when already at column zero, until cursor-aware startup is
implemented and verified.

Review boundaries, explicitly retained rather than silently counted as parity:

- Final: Ruling: native input, resize, Kitty and OS mode restoration remain later
  gates — no native driver was implemented — cost: no interactive-input release.
- Final: Ruling: agent widgets, suspension, alternate screen, Console patching and
  30 FPS scheduling remain open — this is an output foundation, not placeholders
  claiming those APIs — cost: full terminal/agent previews are not complete.
- Final: Ruling: teardown restores cursor visible, not queried prior visibility —
  the preview exposes HideCursor and documents this — cost: initially hidden
  cursor state cannot be restored exactly before the native-state gate.
- Final: Ruling: ownership is by writer reference, not underlying OS handle —
  callers must not wrap one terminal into competing sessions — cost: distinct
  wrappers around the same handle are not detected.
- Final: Ruling: uncooperative lifecycle/writer work can delay teardown —
  releasing a still-writing owner is unsafe — cost: callers must cooperate with
  cancellation/lifetime shutdown.
- Final: Ruling: flush excludes arbitrary future detached tasks — no finite
  barrier can await work not admitted yet — cost: callers must await their own
  detached work; component-origin flush covers committed output only.
- Final: Ruling: preexisting text/parity gaps and platform/performance certification
  remain open — passing representative checks is not full conformance — cost:
  eight-column tab-stop parity and the complete corpus still require work.
- Final: Ruling: package verification is performed by the parent, not repeated by
  the reviewer — avoids competing builds — cost: packaging must pass the final
  fresh-cache consumer command before this checkpoint is reported.

Deferred minor: four-space tab expansion remains from the previous review.
Blank Static rows and extracted background inheritance are now fixed.

### Final resumed checkpoint — 2026-10-07

Command: `bash scripts/verify.sh`
from the sibling .NET solution. Exit code **0**.

- **158 assertions on each of .NET 8.0.28 and .NET 10.0.9**, including all four
  review regressions; no re-review substituted for the RED→GREEN fix pass.
- Headless layout and deterministic output-session samples run on both targets.
- Core preview package packed locally with pinned Yoga/Wcwidth wrapper
  dependencies. **No package published.**
- A new isolated NuGet cache restored solely from local packages; the separate
  PackageReference-only consumer verified both layout and session final output
  on each target.
- Upstream Ink remains at `26d2c3f83008142061c22267482489588cc3823c`;
  its working tree is checked separately for modifications.

The next gate at this output-only checkpoint was native input ownership,
fragmented decoding and paste; the increment below supersedes that gate.

### Input foundation checkpoint — 2026-10-07

Implemented the approved input foundation: subscription-activated Windows,
macOS and Linux drivers; caller-owned byte streams; incremental UTF-8/legacy
key framing; literal bracketed paste; dispatcher/backpressure integration;
exclusive input ownership; and explicit native/scripted sample modes.

- Fresh full verification: **455 assertions per target** on .NET 8.0.28 and
  10.0.9, all sample modes, local packaging and clean package consumers.
- Fresh macOS arm64 native-header probe and **22 PTY runs** passed, including
  normal/failure cleanup, competing ownership and output-only behavior.
- Independent read-only review found three Important input defects; regression
  checks cover cancellation callbacks, stale Ctrl+C and failed-startup ownership.
- Linux and Windows native execution remains **unverified**.
- Ruling: macOS checks normalize only kernel-owned PENDIN bookkeeping; raw
  termios bytes can differ in that bit. All configurable settings and descriptor
  flags are compared. Prior cursor/protocol-state discovery remains deferred.
- No packages published; upstream Ink remains unchanged.

Detailed evidence and rulings are recorded in
[the input-foundation ledger](blazor-ink-input-foundation.md).

### Resumed platform-gate checkpoint — 2026-10-07

- Local verification repeated successfully: **455 assertions per framework**,
  all sample modes, packaging and clean consumers; **22 macOS arm64 PTY runs**.
- Linux remains **unverified**: no build, ABI probe or native scenario ran.
  Docker was unavailable, the existing Podman disk was missing, and an isolated
  test VM did not become ready. The temporary VM was removed without replacing
  the existing configuration or changing the original default connection.
- Windows remains **unverified** because no Windows host is available.
- Native certification is **environment-blocked**, not failed by an input test.
  A manual Linux/Windows CI workflow is proposed but **not implemented**.
- Implementation remains uncommitted on `codex/input-foundation`; no packages
  published. Deferred features have not been started.

Next gate: obtain usable Linux/Windows runners and execute PTY/ConPTY restoration checks, then
Kitty/focus/cursor/resize. Continue the open Yoga/differential/platform corpus
rather than declaring earlier phases complete from representative checks.
