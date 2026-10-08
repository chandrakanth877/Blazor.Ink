#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet="${DOTNET:-dotnet}"
python="${PYTHON:-python3}"
PACKAGE_VERSION="$("$python" scripts/version.py)"
export PACKAGE_VERSION
version="$("$dotnet" msbuild src/Blazor.Ink/Blazor.Ink.csproj -getProperty:Version | tr -d '\r')"
if [[ -n "${RELEASE_TAG:-}" && "$RELEASE_TAG" != "v$version" ]]; then
  echo "Release tag must equal v$version." >&2
  exit 1
fi
"$python" scripts/test-package.py
"$python" scripts/test-version.py
"$python" scripts/test-workflow.py
restore_source=()
if [[ -n "${NUGET_SOURCE:-}" ]]; then restore_source+=(--source "$NUGET_SOURCE"); fi
"$dotnet" restore Blazor.Ink.sln --locked-mode --disable-parallel -m:1 ${restore_source[@]+"${restore_source[@]}"}
mkdir -p artifacts/component-demo
for framework in net8.0 net10.0; do
  "$dotnet" run --project tests/Blazor.Ink.Checks -c Release -f "$framework" --no-restore -p:UseSharedCompilation=false
  "$dotnet" run --project samples/LayoutDemo -c Release -f "$framework" --no-restore -p:UseSharedCompilation=false
  "$dotnet" run --project samples/LayoutDemo -c Release -f "$framework" --no-restore -p:UseSharedCompilation=false -- --session
  "$dotnet" run --project samples/LayoutDemo -c Release -f "$framework" --no-restore -p:UseSharedCompilation=false -- --input --scripted
  "$dotnet" run --project samples/ComponentDemo -c Release -f "$framework" --no-restore -p:UseSharedCompilation=false -- --check
  "$dotnet" run --project samples/ComponentDemo -c Release -f "$framework" --no-build -- --snapshot --all > "artifacts/component-demo/snapshot-$framework.txt"
  "$dotnet" run --project samples/ComponentDemo -c Release -f "$framework" --no-build -- --scripted > "artifacts/component-demo/scripted-$framework.txt"
  case "$(uname -s)" in
    Darwin|Linux) "$python" scripts/check-resize.py "$dotnet" "samples/ComponentDemo/bin/Release/$framework/ComponentDemo.dll" ;;
  esac
done
"$dotnet" pack src/Blazor.Ink/Blazor.Ink.csproj -c Release --no-build --no-restore -o artifacts/packages
metadata=()
if [[ -n "${GITHUB_REPOSITORY:-}" ]]; then
  metadata+=(--repository "https://github.com/$GITHUB_REPOSITORY" --commit "$(git rev-parse HEAD)")
fi
if [[ -n "${RELEASE_TAG:-}" ]]; then metadata+=(--tag "$RELEASE_TAG"); fi
"$python" scripts/check-package.py "artifacts/packages/Blazor.Ink.$version.nupkg" \
  --symbols "artifacts/packages/Blazor.Ink.$version.snupkg" ${metadata[@]+"${metadata[@]}"}
DOTNET="$dotnet" bash scripts/check-packages.sh "$version"
