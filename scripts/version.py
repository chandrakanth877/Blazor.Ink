"""Assign push versions using immutable Git tags; no version commits or NuGet publication."""
import argparse
import os
import re
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def normalize(value):
    match = re.fullmatch(r"(0|[1-9]\d*)\.(0|[1-9]\d*)(?:\.(0|[1-9]\d*))?(-[0-9A-Za-z.-]+)?", value)
    if match is None:
        raise ValueError(f"Invalid version: {value!r}")
    major, minor, patch, suffix = match.groups()
    return f"{major}.{minor}.{patch or '0'}{suffix or ''}"


def configured_version():
    return normalize(ET.parse(ROOT / "Directory.Build.props").findtext(".//Version") or "")


def release_version(tag):
    if not tag.startswith("v"):
        raise ValueError("Release tags must start with v.")
    version = normalize(tag[1:])
    if tag != f"v{version}" or version.split(".")[:2] != configured_version().split(".")[:2]:
        raise ValueError("Release tag must use the configured major.minor base and a full version.")
    return version


def package_version():
    override = os.environ.get("PACKAGE_VERSION")
    return release_version(f"v{override}") if override else configured_version()


def git(*arguments, check=True):
    return subprocess.run(["git", "-C", str(ROOT), *arguments], check=check,
                          capture_output=True, text=True)


def assign_version(run_id):
    if not re.fullmatch(r"[1-9]\d*", run_id):
        raise ValueError("A positive numeric GitHub run ID is required.")
    base = ".".join(configured_version().split(".")[:2])
    head = git("rev-parse", "HEAD").stdout.strip()
    marker = f"Blazor.Ink CI run {run_id}"
    for _ in range(5):
        git("fetch", "--tags", "origin")
        tags = git("for-each-ref", "--format=%(refname:strip=2)\t%(contents:subject)",
                   "refs/tags").stdout.splitlines()
        patches = []
        for entry in tags:
            name, _, subject = entry.partition("\t")
            match = re.fullmatch(rf"v{re.escape(base)}\.(0|[1-9]\d*)", name)
            if match is None:
                continue
            patches.append(int(match[1]))
            if subject == marker:
                if git("rev-parse", f"{name}^{{commit}}").stdout.strip() != head:
                    raise ValueError("This run already assigned a version to a different commit.")
                return name[1:]
        version = f"{base}.{max(patches, default=-1) + 1}"
        tag = f"v{version}"
        git("tag", "--annotate", "--message", marker, tag, head)
        pushed = git("push", "origin", f"refs/tags/{tag}", check=False)
        if pushed.returncode == 0:
            return version
        git("tag", "--delete", tag)  # Only our unpushed local reservation is removed.
        git("fetch", "--tags", "origin")
        if git("rev-parse", "--verify", f"refs/tags/{tag}", check=False).returncode != 0:
            raise RuntimeError(pushed.stderr.strip())  # Authentication/network failures are not collisions.
    raise RuntimeError("Version assignment collided five times; rerun this workflow.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--assign", metavar="RUN_ID")
    mode.add_argument("--release-tag")
    arguments = parser.parse_args()
    try:
        print(assign_version(arguments.assign) if arguments.assign else
              release_version(arguments.release_tag) if arguments.release_tag else package_version())
    except (ValueError, RuntimeError, ET.ParseError, subprocess.CalledProcessError) as error:
        parser.exit(1, f"Version check failed: {getattr(error, 'stderr', None) or error}\n")
