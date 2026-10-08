"""Check the real Git-backed version counter, including reruns and competing pushes."""
import os
import shlex
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

script = Path(__file__).with_name("version.py")

with tempfile.TemporaryDirectory() as directory:
    root = Path(directory)
    remote = root / "remote.git"
    repository = root / "checkout"
    subprocess.run(["git", "init", "--bare", str(remote)], check=True, capture_output=True)
    subprocess.run(["git", "-C", str(remote), "symbolic-ref", "HEAD", "refs/heads/main"], check=True)
    subprocess.run(["git", "init", "-b", "main", str(repository)], check=True, capture_output=True)

    def git(*arguments, cwd=repository):
        return subprocess.check_output(["git", "-C", str(cwd), *arguments], text=True).strip()

    git("config", "user.name", "Version check")
    git("config", "user.email", "version-check@example.invalid")
    git("remote", "add", "origin", str(remote))

    def commit(version, text):
        (repository / "Directory.Build.props").write_text(
            f"<Project><PropertyGroup><Version>{version}</Version></PropertyGroup></Project>"
        )
        (repository / "code.txt").write_text(text)
        git("add", "Directory.Build.props", "code.txt")
        git("commit", "-qm", text)

    def install(cwd):
        (cwd / "scripts").mkdir(exist_ok=True)
        shutil.copy2(script, cwd / "scripts/version.py")

    def command(*arguments, cwd=repository, override=None):
        env = dict(os.environ)
        env.pop("PACKAGE_VERSION", None)
        if override is not None:
            env["PACKAGE_VERSION"] = override
        return subprocess.run([sys.executable, str(cwd / "scripts/version.py"), *arguments],
                              cwd=cwd, env=env, capture_output=True, text=True, timeout=15)

    def assign(run_id, expected):
        result = command("--assign", str(run_id))
        assert result.returncode == 0, result.stderr
        assert result.stdout.strip() == expected, (result.stdout, expected)
        return git("rev-parse", f"v{expected}^{{commit}}")

    commit("1.0.0", "initial")
    git("tag", "v1.0.0")
    git("push", "-q", "origin", "main", "--tags")
    install(repository)
    first = assign(101, "1.0.1")
    assert assign(101, "1.0.1") == first, "A rerun changed its assigned version."
    commit("1.0.0", "second push")
    assign(102, "1.0.2")
    assert command("--assign", "101").returncode != 0, "A run ID was rebound to different code."

    # Both first pushes must reserve v1.0.3 before either can update the remote.
    barrier = root / "barrier.py"
    barrier.write_text("""import sys, time
from pathlib import Path
marker, peer = map(Path, sys.argv[1:])
if not marker.exists():
    marker.write_text(sys.stdin.read())
deadline = time.monotonic() + 10
while not peer.exists():
    if time.monotonic() >= deadline:
        raise TimeoutError("The competing push never reached its reservation.")
    time.sleep(0.01)
""")
    clones = {}
    for run_id, peer_id in ((103, 104), (104, 103)):
        clone = root / f"checkout-{run_id}"
        git("clone", "-q", str(remote), str(clone), cwd=root)
        git("checkout", "-q", "main", cwd=clone)
        git("config", "user.name", "Version check", cwd=clone)
        git("config", "user.email", "version-check@example.invalid", cwd=clone)
        install(clone)
        hook = clone / ".git/hooks/pre-push"
        arguments = [sys.executable, barrier, root / str(run_id), root / str(peer_id)]
        command_line = " ".join(shlex.quote(Path(p).as_posix()) for p in arguments)
        hook.write_bytes(f"#!/bin/sh\nexec {command_line}\n".encode())
        hook.chmod(0o755)
        clones[run_id] = clone
    processes = []
    for run_id, clone in clones.items():
        env = dict(os.environ)
        env.pop("PACKAGE_VERSION", None)
        processes.append(subprocess.Popen(
            [sys.executable, str(clone / "scripts/version.py"), "--assign", str(run_id)],
            cwd=clone, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
        ))
    assigned = []
    for process in processes:
        output, error = process.communicate(timeout=15)
        assert process.returncode == 0, error
        assigned.append(output.strip())
    assert sorted(assigned) == ["1.0.3", "1.0.4"], assigned
    for run_id in clones:
        assert (root / str(run_id)).read_text().split()[0] == "refs/tags/v1.0.3", "The test did not force a collision."
    assign(105, "1.0.5")  # A distinct push gets a new version, even at the same commit.

    for base, run_id, expected in (("1.1", 106, "1.1.0"), ("1.1", 107, "1.1.1"),
                                  ("1.2.0", 108, "1.2.0"), ("2.0", 109, "2.0.0")):
        commit(base, f"base {base}, push {run_id}")
        assign(run_id, expected)
    assert command().stdout.strip() == "2.0.0", "Two-part bases were not normalized."
    assert command(override="2.0.42").stdout.strip() == "2.0.42"
    for invalid in ("2.1.0", "02.0.1", "2.0", "2.0.1;echo bad"):
        assert command(override=invalid).returncode != 0, f"Accepted invalid override {invalid}"
    assert command("--release-tag", "v2.0.0").stdout.strip() == "2.0.0"
    assert command("--release-tag", "v2.1.0").returncode != 0, "A mismatched release base was accepted."
    assert command("--release-tag", "v2.0.0;echo bad").returncode != 0
    assert command("--assign", "not-a-run-id").returncode != 0
    commit("1.0.9", "return to a used base with a different checked-in patch")
    assign(110, "1.0.6")  # Existing immutable package versions must never be reused.

print("PASS automatic versions, base resets, reruns, competing pushes and release guards")
