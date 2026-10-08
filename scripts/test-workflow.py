"""Check main-merge publication gates without credentials, network, or new dependencies."""
import re
from pathlib import Path
from types import SimpleNamespace

workflow = (Path(__file__).resolve().parent.parent / ".github/workflows/build.yml").read_text()
publish = workflow.split("\n  publish:\n", 1)[1].split("\n  verify-published:\n", 1)[0]
condition = re.search(r"^    if: (.+)$", publish, re.MULTILINE)[1]
condition = condition.replace("&&", " and ").replace("||", " or ")

for event, ref, repository, enabled, expected in (
    ("push", "refs/heads/main", "chandrakanth877/Blazor.Ink", "true", True),
    ("push", "refs/heads/codex/change", "chandrakanth877/Blazor.Ink", "true", False),
    ("push", "refs/tags/v1.0.3", "chandrakanth877/Blazor.Ink", "true", False),
    ("pull_request", "refs/heads/main", "chandrakanth877/Blazor.Ink", "true", False),
    ("release", "refs/tags/v1.0.3", "chandrakanth877/Blazor.Ink", "true", False),
    ("workflow_dispatch", "refs/heads/main", "chandrakanth877/Blazor.Ink", "true", False),
    ("push", "refs/heads/main", "someone/Blazor.Ink", "true", False),
    ("push", "refs/heads/main", "chandrakanth877/Blazor.Ink", "false", False),
    ("push", "refs/heads/main", "chandrakanth877/Blazor.Ink", "", False),
):
    github = SimpleNamespace(event_name=event, ref=ref, repository=repository)
    variables = SimpleNamespace(NUGET_PUBLISH_ENABLED=enabled)
    actual = eval(condition, {"__builtins__": {}}, {"github": github, "vars": variables})
    assert actual == expected, f"Unexpected publication gate: {event}, {ref}, {repository}, {enabled}"

assert "    needs: [version, verify]\n" in publish, "Publishing must await all verification jobs."
assert "    environment: nuget\n" in publish, "The manual approval environment must remain."
assert "      group: publish-blazor-ink-${{ needs.version.outputs.version }}\n" in publish, "New merges must not cancel another version's pending publication."
assert "      cancel-in-progress: false\n" in publish
assert "      id-token: write\n" in publish, "NuGet must retain short-lived OIDC credentials."
assert "      PACKAGE_VERSION: ${{ needs.version.outputs.version }}\n" in publish
assert "      RELEASE_TAG: ${{ format('v{0}', needs.version.outputs.version) }}\n" in publish
assert publish.index("Check artifact identity") < publish.index("Obtain a short-lived NuGet key")
assert "dotnet pack" not in publish and "dotnet build" not in publish, "Publish the verified artifact, never rebuild."
assert "--skip-duplicate" not in publish, "Immutable version collisions must fail."
assert "\n  verify-published:\n" in workflow, "Already-published versions need no-push recovery."
print("PASS merged-main publication, fork/PR/release/disabled gates and artifact/approval safeguards")
