"""Exercise automatic sizing in the real showcase, using only Python's PTY stdlib."""
import fcntl
import os
import pty
import re
import select
import signal
import struct
import subprocess
import sys
import termios
import time

# Closing a PTY must not terminate the test runner's inherited process group.
signal.signal(signal.SIGHUP, signal.SIG_IGN)


def plain(data):
    return re.sub(rb"\x1b\[[0-?]*[ -/]*[@-~]", b"", data).decode("utf-8", errors="replace")


for arguments, initial, narrow, wide in (
    ([], "100×30", "44×18", "120×40"),
    (["--columns", "70"], "70×30", "70×18", "70×40"),
    (["--rows", "20"], "100×20", "44×20", "120×20"),
    (["--columns", "70", "--rows", "20"], "70×20", None, None),
):
    master, slave = pty.openpty()
    output_fd = os.open(os.ttyname(slave), os.O_RDWR)
    transcript = bytearray()

    def resize(columns, rows):
        fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack("HHHH", rows, columns, 0, 0))

    def read_for(seconds):
        data = bytearray()
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            if select.select([master], [], [], 0.02)[0]:
                data.extend(os.read(master, 65536))
        transcript.extend(data)
        return bytes(data)

    def expect(text, ending=None):
        data = bytearray()
        deadline = time.monotonic() + 10
        while time.monotonic() < deadline:
            data.extend(read_for(0.05))
            rendered = plain(data)
            position = rendered.find(text)
            if position >= 0 and (ending is None or ending in rendered[position:]):
                return bytes(data)
            assert process.poll() is None, plain(data)
        raise AssertionError(f"Expected {text!r}, received:\n{plain(data)}")

    resize(100, 30)
    env = dict(os.environ, TERM="xterm-256color")
    env.pop("CI", None)
    process = subprocess.Popen(
        [sys.argv[1], sys.argv[2], *arguments],
        stdin=slave, stdout=output_fd, stderr=output_fd,
        env=env, start_new_session=True,
    )
    try:
        expect(initial, "Ctrl+C")
        read_for(0.2)
        if not arguments:
            os.write(master, b"/add demo.cs\rhello\rprefix ")
            expect("prefix")
            read_for(0.2)
        for columns, rows, expected in ((44, 18, narrow), (120, 40, wide)):
            resize(columns, rows)
            # Real terminals deliver SIGWINCH to their foreground process group.
            os.kill(process.pid, signal.SIGWINCH)
            if expected is None:
                assert not read_for(0.35), "Explicit dimensions changed after resize."
            else:
                frame = expect(expected, "Ctrl+C")
                assert b"\x1b[2J\x1b[H" in frame, "Resize did not reset the viewport."
                assert b"\x1b[3J" not in frame, "Resize purged scrollback."
                if not arguments:
                    assert "prefix" in plain(frame), "Resize lost the composer draft."
                    assert "demo.cs" in plain(frame), "Resize lost attachments."
                    if columns == 120:
                        assert "hello" in plain(frame), "Resize lost chat messages."
                read_for(0.2)
                assert not read_for(0.25), "Unchanged dimensions emitted extra frames."
        if not arguments:
            resize(0, 0)
            os.kill(process.pid, signal.SIGWINCH)
            assert not read_for(0.35), "An unusable size discarded the last valid dimensions."
            resize(120, 40)
            os.kill(process.pid, signal.SIGWINCH)
            assert not read_for(0.25), "Restoring the same valid size emitted extra frames."
            for page in ("Text", "Box", "Static", "Transform", "Newline", "Spacer", "Runtime"):
                os.write(master, b"\x0e")
                expect(f"[{page}]")
                if page == "Static":
                    os.write(master, b"\r")
                    expect("1 completed")
                for columns, rows in ((44, 18), (20, 8), (120, 40)):
                    resize(columns, rows)
                    os.kill(process.pid, signal.SIGWINCH)
                    frame = expect("Terminal too" if columns == 20 else f"{columns}×{rows}",
                                   "exits." if columns == 20 else "Ctrl+C")
                    assert b"\x1b[2J\x1b[H" in frame, f"{page}: stale viewport after resize"
                    assert b"\x1b[3J" not in frame, f"{page}: scrollback was purged"
                    if columns != 20:
                        assert f"[{page}]" in plain(frame), f"{page}: selection was lost"
                        assert "Ctrl+C" in plain(frame), f"{page}: exit help was lost:\n{plain(frame)}"
                    if page == "Text" and columns == 44:
                        os.write(master, b"\x1b[B" * 10)
                        read_for(0.2)
                    if page == "Text" and columns == 120:
                        assert "Bold" in plain(frame), "Growth did not clamp the old scroll position."
            assert plain(transcript).count("✓ Completed task 1") == 1, "Resize replayed Static history."
        os.write(master, b"\x03")
        process.wait(timeout=5)
        cleanup = read_for(0.1)
        assert process.returncode == 0, plain(cleanup)
        assert b"\x1b[?25h" in cleanup, "Exit did not restore the cursor."
        print(f"PASS PTY resize {arguments or 'automatic'} ({sys.argv[2]})")
    finally:
        if process.poll() is None:
            process.kill()
        process.wait()
        for fd in (master, slave, output_fd):
            os.close(fd)
