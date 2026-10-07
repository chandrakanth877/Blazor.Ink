# Input foundation implementation ledger

> Historical research/design document carried into the standalone export.
> Current package identity, dependency acquisition, and release policy are in
> [Publishing Blazor.Ink](publishing.md); earlier vendor acquisition notes do not
> describe the dependencies shipped by this repository.

The approved input-foundation plan is the specification for this increment.
Work is on `codex/input-foundation` in the supplied checkout; upstream `ink/`
remains a read-only reference.

## Decisions and progress

- Native drivers: Windows, macOS and Linux; only locally executed native gates
  count as verified.
- Activation: first active subscription acquires input; last release restores it.
- Custom byte streams are caller-owned and never cause native mode changes.
- No new dependencies, publication, Kitty, focus, resize or agent widgets.
- Interfaces: nullable `InkOptions.Stdin`, `InkInput`, mutable/disposable
  `InkInputSubscription`, `InkInputEvent` and legacy `InkKey`.
- Pending input ceiling: 1,000,000 UTF-16 characters. Overflow fails visibly.
- Baseline: 158 checks per framework, both samples and clean package consumers.
- Decoder, native drivers, session integration, input sample and package-consumer
  coverage implemented; final verification recorded below.
- Decoder/API RED: new checks failed compilation on missing types and options.
  Initial parser runs exposed ordinal escape-prefix comparison and a C# hex
  literal typo; the typed parser/session suite subsequently passed 427 checks.
- Transition stress RED: 4,096 activation toggles allocated an unbounded task
  chain; transitions now coalesce to one worker plus desired state.
- Ruling: macOS restoration checks normalize only kernel-owned `PENDIN`
  bookkeeping — raw-to-canonical re-entry sets it and a second tcsetattr does
  not clear it — cost: raw termios bytes can differ in that state bit. Every
  configurable setting remains compared, and no pending user input is drained.
- Decoder regression RED: invalid CSI/SS3 continuations were discarded as
  complete replies. Complete-control classification now verifies the whole
  sequence; the 437-check suite and both-framework package run passed.
- Final review: independent read-only review identified three Important
  findings, no Critical findings and no deferred minors. Its fault-injection
  repros were inspected, then added to the executable harness.
- Cancellation lock regression RED: a caller cancellation callback could not
  acquire the subscription lock from another thread. Cancellation now uses
  `CancelAsync`, awaits callbacks before release, and preserves cancellation
  failures without bypassing reconciliation.
- Queued Ctrl+C regression RED: delivery exited after the last subscription was
  deactivated. Dispatch now rechecks the read token before built-in commands.
- Native-startup regression RED: injected setup/restore failures bypassed safe
  ownership. Drivers now capture first, remain retained before raw setup, and
  release only after restoration succeeds; rollback preserves the initial error.
  A controlled restoration of the unsafe assignment order also reproduced the
  exact lease bug: the competing session acquired native input after failed
  rollback. Restoring capture-before-setup rejects that competing owner.
- Additional RED checks covered invalid double-Escape framing, legacy literal
  key metadata and buffered protocol transitions. Framing/metadata now match
  the pinned behavior, and serialized controls flush before ownership release.
- Final: fixed all three reviewer findings — lock inversion/throwing
  cancellation, stale Ctrl+C and failed-startup ownership checks GREEN;
  full suite **455/455 per target**. No deferred review minors.

## Final verification — October 7, 2026

- `bash scripts/verify.sh`: exit **0**,
  **455 assertions each** on .NET **8.0.28** and **10.0.9**; headless,
  output-session and scripted-input samples; local packing; isolated-cache
  PackageReference-only consumers exercising rendering, input, paste and exit.
- One preceding verification attempt was interrupted by host `SIGKILL` during
  the .NET 10 scripted sample. Its direct rerun and the complete fresh run both
  passed; the interrupted attempt is not counted as a passing verification.
- `bash scripts/check-native-input.sh`:
  exit **0**, native-header ABI probe, zero build warnings/errors, **11 macOS
  arm64 PTY scenarios per target (22 runs)**. Parent and child independently
  compare settings/descriptor flags, including normal exit, release/reacquire,
  Ctrl+C, blocked-read cancellation, callback/component/startup failures,
  mount-time subscription, non-default prior settings, competing ownership,
  output-only behavior and repeated disposal.
- Linux native/PTY and Windows ConPTY execution: **unverified**, not passing.
  The platform runners are included; no cross-platform certification is claimed.
- Exact-restoration scope is changed OS input settings, not discovery of prior
  cursor visibility or bracketed-paste protocol state. The macOS PENDIN ruling
  above remains the only normalized termios bit.
- Work remains uncommitted on `codex/input-foundation`, based on `bab6ed1`.
  Upstream `ink/` remains clean at `26d2c3f83008142061c22267482489588cc3823c`.
  No new dependency or package publication.

## Resumed platform-gate attempt

- Fresh local verification exited **0** again: **455 assertions per target**,
  all sample modes, local packaging and isolated package consumers.
- Fresh macOS arm64 ABI/PTY verification also exited **0** again: **22 PTY
  runs**, zero warnings/errors, with the same explicit PENDIN normalization.
- Docker's local daemon was unavailable. The pre-existing Podman VM could not
  start because its configured disk image was missing; that configuration was
  not repaired or replaced.
- A separately authorized `ink-input-checks` VM was created for Linux testing,
  but did not become ready or expose a working Podman connection. No Linux
  build, ABI probe or native-input scenario ran. Linux remains **unverified**.
- The stalled test processes and temporary VM were removed. The pre-existing
  VM and original default Podman connection were preserved.
- No Windows host is available here; Windows ConPTY execution remains
  **unverified**. The existing platform runners require usable OS runners.
