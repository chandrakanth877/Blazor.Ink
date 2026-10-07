"""PTY oracle: independently compare native termios and descriptor flags around child sessions."""
import errno
import fcntl
import os
import pty
import select
import subprocess
import sys
import termios
import time

def settings(fd):
    state = termios.tcgetattr(fd)
    if sys.platform == "darwin":
        # PENDIN is kernel retype bookkeeping, not a writable configuration bit.
        state[3] &= ~termios.PENDIN
    return state

for scenario in ("normal", "release", "ctrl-c", "cancel", "callback-failure", "startup-failure",
                 "mount-input", "preconfigured", "component-failure", "competing", "output-only"):
    master, slave = pty.openpty()
    # Console's asynchronous stdout can set O_NONBLOCK. Give output its own open-file
    # description so that the independently observed input flags belong to input.
    output_fd = os.open(os.ttyname(slave), os.O_RDWR)
    if scenario == "preconfigured":
        initial = termios.tcgetattr(slave)
        initial[3] &= ~termios.ECHO
        termios.tcsetattr(slave, termios.TCSANOW, initial)
        fcntl.fcntl(slave, fcntl.F_SETFL, fcntl.fcntl(slave, fcntl.F_GETFL) | os.O_NONBLOCK)
    before = settings(slave)
    flags = fcntl.fcntl(slave, fcntl.F_GETFL)
    process = subprocess.Popen(
        [sys.argv[1], sys.argv[2], "--native-input-child", scenario],
        stdin=slave, stdout=output_fd, stderr=output_fd, close_fds=True,
    )
    output = bytearray()
    sent = False
    try:
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            if select.select([master], [], [], 0.05)[0]:
                try:
                    chunk = os.read(master, 65536)
                except OSError as error:
                    if error.errno != errno.EIO:
                        raise
                    chunk = b""
                output.extend(chunk)
            if not sent and b"NATIVE_READY" in output:
                sent = True
                if scenario == "callback-failure":
                    os.write(master, b"x")
                elif scenario == "ctrl-c":
                    os.write(master, b"\x03")
                elif scenario != "cancel":
                    os.write(master, b"\x1b[200~p\r\x03\x1b[201~q")
            if process.poll() is not None:
                break
        else:
            raise AssertionError(f"{scenario}: native child timed out\n{output.decode(errors='replace')}")
        assert process.returncode == 0, output.decode(errors="replace")
        assert ("NATIVE_PASS " + scenario).encode() in output, output.decode(errors="replace")
        assert before == settings(slave), f"{scenario}: parent-observed termios configuration changed"
        assert flags == fcntl.fcntl(slave, fcntl.F_GETFL), f"{scenario}: descriptor flags changed"
        if scenario == "output-only":
            assert b"\x1b[?2004" not in output, "output-only session negotiated input protocols"
        print(f"PASS PTY {scenario} ({sys.argv[2]})")
    finally:
        if process.poll() is None:
            process.kill()
        process.wait()
        os.close(master)
        os.close(slave)
        os.close(output_fd)
