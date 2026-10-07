#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet="${DOTNET:-dotnet}"
python="${PYTHON:-python3}"
version="${1:-$("$dotnet" msbuild src/Blazor.Ink/Blazor.Ink.csproj -getProperty:Version | tr -d '\r')}"
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
  echo "Invalid package version: $version" >&2
  exit 1
fi
mkdir -p artifacts/consumer
cat > artifacts/consumer/Consumer.csproj <<EOF
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <RootNamespace>PackageConsumer</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseAppHost>false</UseAppHost>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Blazor.Ink" Version="[$version]" />
  </ItemGroup>
</Project>
EOF
cat > artifacts/consumer/App.razor <<'EOF'
@using Blazor.Ink
@using Facebook.Yoga
<Blazor.Ink.Components.Box Width="@((Length)5)" BorderStyle="@BorderStyle.Single" FlexDirection="@YGFlexDirection.Row">
    <Blazor.Ink.Components.Text>x👩‍💻</Blazor.Ink.Components.Text>
</Blazor.Ink.Components.Box>
EOF
cat > artifacts/consumer/Program.cs <<'EOF'
var output = await Blazor.Ink.InkHost.RenderToStringAsync<PackageConsumer.App>();
var assembly = typeof(Blazor.Ink.Length).Assembly;
if (assembly.GetName().Name != "Blazor.Ink" ||
    assembly.GetExportedTypes().Any(type => type.Namespace is null ||
        type.Namespace != "Blazor.Ink" && !type.Namespace.StartsWith("Blazor.Ink.", StringComparison.Ordinal)))
    throw new Exception("Package assembly/namespace identity check failed.");
if (output != "┌───┐\n│x👩‍💻│\n└───┘" || Blazor.Ink.TerminalText.Width("👩‍💻") != 2)
    throw new Exception("Local package rendering/width check failed.");
using var sink = new StringWriter();
await using (var session = await Blazor.Ink.InkHost.RenderAsync<PackageConsumer.App>(new()
    { Stdout = sink, Stderr = sink, Interactive = false }))
{
    await session.RerenderAsync(Microsoft.AspNetCore.Components.ParameterView.Empty);
    await session.AwaitFlushAsync();
}
if (sink.ToString() != output + "\n")
    throw new Exception("Local package session/final-output check failed.");
using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("\x1b[200~literal\u0003\x1b[201~q"));
using var inputSink = new StringWriter();
var inputSession = await Blazor.Ink.InkHost.RenderAsync<PackageConsumer.App>(new()
    { Stdin = input, Stdout = inputSink, Stderr = inputSink });
var pasted = false;
using var subscription = inputSession.Input.Subscribe(item =>
{
    if (item.IsPaste) pasted = item.Text == "literal\u0003" && item.Key is null;
    if (!item.IsPaste && item.Text == "q") inputSession.RequestExit(42);
    return Task.CompletedTask;
});
if (await inputSession.WaitUntilExitAsync().WaitAsync(TimeSpan.FromSeconds(5)) != 42 || !pasted)
    throw new Exception("Local package input/paste/exit check failed.");
await inputSession.DisposeAsync();
if (!input.CanRead || inputSession.Input.IsRawModeSupported || inputSink.ToString() != output + "\n")
    throw new Exception("Local package caller-owned input/output check failed.");
Console.WriteLine($"PASS clean package consumer ({System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription})");
EOF
# Isolate the dependency cache: this consumer has no ProjectReference fallback.
NUGET_PACKAGES="$("$python" -c 'import os, tempfile; print(tempfile.mkdtemp(prefix="consumer-cache.", dir=os.path.abspath("artifacts")))' | tr -d '\r')"
export NUGET_PACKAGES
local_feed="$("$python" -c 'import os; print(os.path.abspath("artifacts/packages"))' | tr -d '\r')"
# Keep native paths and HTTPS URLs out of command-line source-list normalization.
"$python" - <<'PY'
import os
import xml.etree.ElementTree as ET
config = ET.parse("NuGet.Config")
sources = config.find("packageSources")
upstream = sources.find("add[@key='nuget.org']")
if os.environ.get("NUGET_SOURCE"):
    upstream.set("value", os.environ["NUGET_SOURCE"])
    upstream.attrib.pop("protocolVersion", None)
ET.SubElement(sources, "add", key="local", value=os.path.abspath("artifacts/packages"))
config.write("artifacts/consumer/NuGet.Config", encoding="utf-8", xml_declaration=True)
PY
"$dotnet" restore artifacts/consumer/Consumer.csproj --configfile artifacts/consumer/NuGet.Config \
  --force --force-evaluate --disable-parallel -m:1
"$python" scripts/check-package.py "$local_feed/Blazor.Ink.$version.nupkg" --restored-cache "$NUGET_PACKAGES"
for framework in net8.0 net10.0; do
  "$dotnet" run --project artifacts/consumer -c Release -f "$framework" --no-restore -p:UseSharedCompilation=false
done
