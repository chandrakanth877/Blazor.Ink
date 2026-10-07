# Blazor.Ink — What is done and what is left

> Historical research/design document carried into the standalone export.
> Current package identity, dependency acquisition, and release policy are in
> [Publishing Blazor.Ink](publishing.md); earlier vendor acquisition notes do not
> describe the dependencies shipped by this repository.

**Status date:** October 7, 2026  
**Current milestone:** Razor layout, output sessions and input foundation
**Targets:** `net8.0` and `net10.0`; .NET 10 recommended

**Active gate:** Linux PTY / Windows ConPTY native verification — environment-blocked,
not an observed input-test failure. Implementation remains uncommitted and unpublished.

The reusable framework foundation works: Razor components can render headlessly
or mount into an output session with live redraw and append-only history.
**This is not yet a full Ink 8 replacement or an interactive agent UI framework.**
Native input/subscriptions are implemented; Kitty, focus, resize and agent widgets
remain open. Native certification is platform-specific.

The original TypeScript Ink checkout remains separate and unchanged. The .NET
solution is at `.`.

## 1. Done

These features are implemented and covered by representative executable checks;
they are not claims of complete cross-platform or Ink differential conformance.

### Research, documentation and dependencies

- [x] Research and library-reuse review.
- [x] Technical Requirements Document and delivery roadmap.
- [x] Compatibility inventory: 48 Ink exports, 66 style properties and 14 render
  options mapped to proposed counterparts and acceptance specifications.
- [x] Separate .NET solution targeting both frameworks.
- [x] Pinned, vendored MIT source dependencies for Yoga.Net and Wcwidth, with
  licenses and provenance. NuGet connectivity was unavailable.
- [x] Runtime ownership remains in Blazor.Ink; no competing third-party UI loop.

### Blazor host and layout

- [x] Custom Blazor renderer and committed terminal tree.
- [x] Component lifecycle, dependency injection and root-parameter updates.
- [x] Components, regions, text and supported XML markup handling.
- [x] Keyed component/element moves, updates, removal and persistent node identity.
- [x] Core components: `Box`, `Text`, `Newline`, `Spacer`, `Static<TItem>` and
  `Transform`.
- [x] Managed Yoga flex layout and typed style mapping, including dimensions,
  percentages, gaps, padding, borders and clipping.
- [x] Headless `RenderToStringAsync`, with an 80-column default.

### Text and rendering

- [x] Styled cell buffer, preset borders, backgrounds and text decorations.
- [x] Grapheme-aware width measurement using StringInfo and Wcwidth.
- [x] Representative CJK, combining-mark, emoji, flag and split-grapheme checks.
- [x] Wrapping, start/end/middle truncation and line transforms.
- [x] Wide-cell compositing and clipping without leaving half glyphs.
- [x] Sanitization of destructive terminal controls; supported SGR styling.
- [x] XML DTD/external-entity rejection and tree/canvas resource guards.

### Output sessions and lifecycle foundation

- [x] `InkHost.RenderAsync` and `InkOptions`.
- [x] Session rerender, clear, stdout/stderr writes, flush, exit result and disposal.
- [x] Injected `InkSession`, `RequestExit` and cooperative `StoppingToken`.
- [x] One serialized writer with bounded admission.
- [x] Full redraw and safe whole-line incremental redraw.
- [x] Unchanged-frame suppression and viewport-safe live tail clipping.
- [x] Append-once Static output, blank rows, hidden/revealed items and remounts.
- [x] Disposal of consumed Static item components.
- [x] Coordinated explicit logging without rewriting Static history.
- [x] Plain redirected output: Static commits immediately, final live frame at exit.
- [x] Flush barriers, admitted-operation draining and observable output failures.
- [x] Writer-instance ownership checks and idempotent teardown.
- [x] Cursor hiding/showing and a safe boundary for preexisting partial-line history.

### Samples, checks and packaging

- [x] Headless Razor layout sample.
- [x] Deterministic output-session sample, requiring no credentials.
- [x] Explicit native/scripted input sample, preserving existing sample modes.
- [x] Framework-free executable assertions and a small virtual-terminal oracle.
- [x] Independent read-only reviews; four output-runtime and three input-foundation findings fixed.
- [x] Local preview packages and clean PackageReference-only consumers.
- [x] **Last recorded verification: 455 assertions pass on each target**, plus
  headless/output/input samples and clean package consumers. No packages have been published.
- [x] macOS arm64 native ABI probe and 11 PTY scenarios per target (22 runs).

## 2. Left to implement

### Native input and interaction

- [x] Windows/macOS/Linux input drivers, subscription-activated ownership and caller-owned byte streams.
- [ ] Complete native certification on every supported OS; macOS normalizes only kernel PENDIN bookkeeping.
- [x] Fragmented UTF-8, CSI/SS3 and legacy-key decoding.
- [x] 20 ms Escape timeout and unknown CSI/SS3 reply handling.
- [x] Bracketed paste with separate key/paste channels and literal fallback.
- [ ] Kitty negotiation, flags, modifiers, event types and restoration.
- [x] Disposable asynchronous input subscriptions with mutable active state.
- [ ] Focus registration/navigation.
- [ ] Cursor intent, cursor-only updates and terminal dimension services.
- [ ] Automatic terminal sizing and resize handling.

### Complete terminal lifecycle

- [ ] Alternate-screen support.
- [ ] Suspension through callback and asynchronous lease forms.
- [ ] Optional Console redirection/patching.
- [ ] Handled shutdown signals and complete native cleanup.
- [ ] Exact prior cursor-state restoration.
- [ ] Screen-reader/accessibility output and component semantics.
- [ ] Element metrics and first-measure notifications.
- [ ] Shared animation scheduler, frame throttling and dynamic-update coalescing.
- [ ] Complete debug/output-option behavior and remaining rendering options.

### Text and layout parity gaps

- [ ] Eight-column tab stops; the preview currently expands tabs to four spaces.
- [ ] Custom border characters and remaining color compatibility.
- [ ] Supported colon-form SGR and hyperlink behavior.
- [ ] `auto` lengths and negative positioned offsets.
- [ ] Validate every required style/default/precedence combination against Blazor.Ink.
- [ ] Complete Yoga flexbox and text-measurement feasibility fixtures.

### Separate agent UI package

- [ ] Chat transcript and streaming Markdown.
- [ ] Code rendering and syntax highlighting.
- [ ] Grapheme-safe multiline prompt editor and bracketed paste.
- [ ] Tool-status and progress widgets.
- [ ] Approval dialog and command picker.
- [ ] Deterministic interactive agent-widget demo.
- [ ] Streaming, cancellation, focus and malicious-text checks for every widget.
- [ ] Validate and pin widget dependencies before adoption.

The existing output-session sample is **not** the agent-widget demo. Model
orchestration and tool execution do not belong in these widgets.

### Conformance and release

- [ ] Full shared Ink fixture corpus and differential comparisons.
- [ ] Windows Terminal/ConPTY, macOS and Linux PTY testing.
- [ ] Complete CI, redirected-stream and terminal-protocol matrix.
  - [ ] Manual Linux/Windows native-verification workflow — proposed, not implemented.
- [ ] Lifecycle soak tests and failure-injection coverage.
- [ ] Recorded-machine performance benchmarks, including 30 FPS and input latency.
- [ ] Dependency/package identity validation and publication preparation.
- [ ] User-facing examples, API documentation and release checklist completion.
- [ ] Publish consumable packages only after the release gates pass.

## 3. Phase status

Weeks are the original relative planning estimate, **not elapsed work or a new
delivery promise**. No completion percentage is inferred from assertion counts.

| Phase | Planned weeks | Current status | Gate still open |
|---|---:|---|---|
| 0 — Research/documents | 1 | Documentation checkpoint completed | Keep references and acceptance inventory current |
| 1 — Feasibility | 2 | Partial | Full Yoga fixtures, dependency validation, native restoration |
| 2 — Host/layout | 3–5 | Substantial preview implemented | Complete layout/component conformance |
| 3 — Text/output | 6–8 | Substantial preview implemented | Remaining text/style parity and output certification |
| 4 — Input/focus | 9–11 | Input foundation implemented | Complete native certification, Kitty and focus |
| 5 — Full lifecycle | 12–14 | Partial | Resize, suspension, signals, accessibility, metrics and animation |
| 6 — Agent widgets | 15–17 | Not implemented | Widget package and interactive demo |
| 7 — Conformance/release | 18–20 | Local packaging checks only | Full corpus, platform/performance gates and publication |
| Contingency | 21–24 | Reserved in the plan | Dependency/framework/terminal discrepancies |

## 4. Recommended next order

1. Obtain usable Linux/Windows runners and run the remaining native restoration
   gates. A manual CI workflow is proposed but has not been implemented.
2. Add Kitty, focus, cursor services and resize; reach the interactive preview gate.
3. Complete suspension, signals, accessibility, metrics and animation scheduling.
4. Build the separate agent widgets and scripted interactive demo.
5. Close remaining text/layout gaps, full differential/platform checks and release.

Expand Yoga and differential checks throughout these steps rather than postponing
all compatibility work until release.

## 5. Current limits and operational caveats

- Live output shows at most `Rows - 1` trailing rows; dimensions default to
  80 columns and 24 rows and are currently explicit, not automatically resized.
- Interactive startup emits CRLF to preserve partial history. It can add a blank
  line when the terminal was already at column zero.
- Teardown restores cursor visibility to visible, not a queried previous state.
- Ownership is by TextWriter instance; distinct wrappers over one OS handle are
  not detected as competing owners.
- Host admission and writer admission are each capped at 256 pending operations.
  Pending writer source text is limited to 16,000,000 characters; individual
  external logs are limited to 1,000,000 characters.
- Uncooperative writers or lifecycle tasks can delay exit indefinitely. Leases
  are not released while an admitted operation could still access the writer.
- Component-origin flush covers committed output, avoiding a self-dependency;
  external host flush also waits for previously admitted host operations.
- Arbitrary future detached tasks are not covered by a flush barrier.
- Caller services and writers remain caller-owned.
- Caller input streams remain caller-owned and must cooperate with cancellation.
- macOS restoration compares every configurable termios setting; only kernel-owned
  `PENDIN` bookkeeping is normalized, so raw saved/returned bytes can differ.
- Linux and Windows native runs remain **unverified**.
- Broad native signal handling, remaining platform certification and full terminal parity remain open.
- .NET 8 reaches end of support on November 10, 2026; .NET 10 is recommended.

## 6. Verification and references

Last recorded full verification: **October 7, 2026**, exit code **0**, on
.NET **8.0.28** and **10.0.9**. This status file summarizes that run; creating this
document does not constitute a new runtime test run.
Native verification also exited **0** on macOS arm64: 22 PTY runs, including
failure, cancellation, reacquisition, competing input, non-default settings and
output-only sessions. Linux PTY and Windows ConPTY checks are present but not run.

The resumed run repeated **455 assertions per framework**, all sample modes,
local packaging and clean consumers, plus **22 macOS arm64 PTY runs**; both
verification scripts exited **0**.

### Current native verification matrix

| Platform | Latest recorded result | Remaining blocker / qualification |
|---|---|---|
| macOS arm64 | **Passed:** ABI probe and 22 PTY runs across .NET 8/10 | Only kernel-owned PENDIN is normalized; this is not full terminal-state parity |
| Linux | **Unverified:** no Linux build, ABI probe or native scenario ran | Docker daemon unavailable; existing Podman disk missing; isolated test VM did not become ready |
| Windows | **Unverified:** ConPTY runner not executed | No Windows host available |

The failed temporary Linux VM and its test processes were removed; the
pre-existing VM configuration and default Podman connection were preserved.
No platform gate was marked passing from a failed environment startup.
Detailed attempt evidence is in the
[input-foundation ledger](blazor-ink-input-foundation.md).

To rerun on the development machine:

```sh
cd .
bash scripts/verify.sh
```

- [Usage guide](../README.md)
- [Research and reuse review](blazor-ink-review.md)
- [Technical Requirements Document](blazor-ink-trd.md)
- [Roadmap, estimates and execution evidence](blazor-ink-roadmap.md)
- [Input-foundation implementation evidence](blazor-ink-input-foundation.md)

**Outside this release:** React-only semantics, model orchestration, tool
execution, MCP, persistence, browser rendering and NativeAOT certification.
