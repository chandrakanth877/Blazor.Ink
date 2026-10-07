# Blazor.Ink research and reuse review

> Historical research/design document carried into the standalone export.
> Current package identity, dependency acquisition, and release policy are in
> [Publishing Blazor.Ink](publishing.md); earlier vendor acquisition notes do not
> describe the dependencies shipped by this repository.

## Baseline and evidence

Reference: [Ink 8.0.0 at 26d2c3f83008142061c22267482489588cc3823c](https://github.com/vadimdemedes/ink/tree/26d2c3f83008142061c22267482489588cc3823c).
The original checkout is a read-only reference for this project.
Review date: 2026-10-07. Source review is not evidence of passing runtime tests.

Ink's pipeline is `React reconciliation → terminal node tree → Yoga 3.2.1 → styled compositing → terminal writer`.
`src/reconciler.ts` owns commit semantics; `dom.ts` owns measured nodes;
`styles.ts` maps styles to Yoga; `output.ts` composites cells;
`log-update.ts` and `line-update.ts` maintain terminal state.
`components/App.tsx` coordinates raw input, paste, focus, and cleanup.
`ink.tsx` owns scheduling, resize, static output, logging, suspension, and exit.
`render-to-string.ts` is a separate headless host.

The inspected source contains approximately 10,000 core lines and 1,370 test
declarations. These are inventory counts, not executed test results.

## Library decisions

| Candidate | Evidence / revision | Reuse decision |
|---|---|---|
| [Yoga.Net](https://github.com/chenrensong/Yoga.Net) | Commit `baf14fcd6cbf21d8930a297e32ef3b76674c37bd`; project declares `3.2.3`, MIT, net8/net9/net10 | Managed layout engine, behind an internal adapter. Test flexbox and measurement before adoption. |
| [RazorConsole](https://github.com/RazorConsole/RazorConsole/tree/v0.6.0) | `v0.6.0`, commit `8eca4d28e2191faae4515dda63fabbe4b67375e8`, MIT | Renderer/hosting/driver reference, not a second runtime or transitive package dependency. |
| [Wcwidth](https://github.com/spectreconsole/wcwidth) | `b229f5c6bc2d1601a15e06357dc79b44f4cd5006`, MIT; UnicodeCalculator exposes character, Rune, and string widths | Adopted source snapshot plus .NET StringInfo; local ZWJ, skin-tone, flag, and combining-mark checks pass. Complete Ink width corpus pending. |
| [Spectre.Console](https://github.com/spectreconsole/spectre.console) | MIT; cached `0.54.0` package available locally | Future agent renderables only. Segment.CellCount failed the joined-emoji fixture locally, so it is not used for core measurement. Never start another live display. |
| [Markdig](https://github.com/xoofx/markdig) | BSD-2-Clause; cached `0.43.0` | Agent Markdown parsing. Do not send HTML or raw terminal controls from model output. |
| [ColorCode.Core](https://github.com/CommunityToolkit/ColorCode-Universal) | MIT; cached `2.0.15` | Syntax-highlighting candidate; adopt only when an actual renderer integration exists. |
| [Terminal.Gui](https://github.com/tui-cs/Terminal.Gui) | MIT; imperative views and its own application/layout loop | Driver/editor reference, not the selected component runtime. |
| [Hex1b](https://github.com/mitchdenny/hex1b) | MIT; widget/node reconciliation, Unicode and input helpers | Terminal-testing and implementation reference. Reviewed BlazorDemo is WebAssembly-hosted, not a Razor terminal renderer. |
| [Consolonia](https://github.com/Consolonia/Consolonia) | MIT; Avalonia/XAML terminal backend | Rejected for this Blazor-native product, not for quality reasons. |
| [jwosty/Yoga.NET](https://github.com/jwosty/Yoga.NET) | Native bindings; README requires native builds and project references | Rejected as the initial dependency: additional native distribution work. Not the same project as Yoga.Net. |

Yoga.Net's README claims production readiness, upstream equivalence, CSS Grid,
AOT compatibility, and 833 tests. These are upstream claims. The inspected
calculation implementation is primarily flexbox; neither Grid nor AOT is needed
for Ink parity, and neither is certified here.

RazorConsole's stable renderer handles seven edit kinds in its inspected switch.
Do not assume this covers markup updates or permutation lists. A new adapter must
test every Blazor edit kind it encounters. Its stable input decoder also does not
provide Ink's bracketed-paste channel. Its custom layout, viewport-oriented diff,
and default alternate screen differ from Blazor.Ink.

## Dependency acquisition policy

Try exact NuGet package restore first. Never treat a source project's Version
property as confirmation that a matching package is published.
If NuGet is unreachable, a pinned MIT source snapshot of Yoga.Net may be vendored
with its original license and a provenance file. Do not change algorithms merely
to make a smoke test pass. Cached packages can be restored without contacting
NuGet, with exact versions recorded in project files.

Unpinned research links for comparison projects are not reproducible dependency
pins. Record an immutable revision before copying code from any of them.

## Reuse boundaries and risks

- Own the Blazor adapter, terminal tree, compositing, and terminal ownership.
- Reuse Yoga rather than write a flexbox engine.
- Reuse Blazor lifecycle and DI rather than recreate React.
- Translate selected Ink regression fixtures and retain attribution.
- Learn from RazorConsole drivers, but validate native layouts and restoration on
  each platform. Do not copy hardcoded termios offsets without verification.
- Keep agent packages out of core unless a measured dependency limitation
  justifies a documented change.
- No framework competes for stdin or stdout.

Highest risks: grapheme-width differences; committed Static identity; keyed
component reorder; .NET RenderTree version changes; Windows bottom-right writes;
blocked output during shutdown; process-global Console redirection; and escape
injection through generated text.

## Evidence ledger

The roadmap records actual commands and results. A capability is **specified**
until a runnable check verifies it. Passing local checks does not imply Windows,
Linux, NativeAOT, or complete Ink differential conformance.
