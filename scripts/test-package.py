"""One framework-free regression check for release packaging and its failure gates."""
import runpy
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path
from zipfile import ZipFile

module = runpy.run_path(str(Path(__file__).with_name("check-package.py")))
check_package = module["check_package"]
root = module["ROOT"]
version = ET.parse(root / "Directory.Build.props").findtext(".//Version")
dependencies = {
    item.attrib["Include"]: item.attrib["Version"]
    for item in ET.parse(root / "src/Blazor.Ink/Blazor.Ink.csproj").findall(".//PackageReference")
}

MANIFEST = """<package><metadata>
<id>Blazor.Ink</id><version>0.1.0-preview.1</version>
<authors>Blazor.Ink contributors</authors><license type="expression">MIT</license>
<readme>README.md</readme><projectUrl>https://github.com/example/Blazor.Ink</projectUrl>
<repository type="git" url="https://github.com/example/Blazor.Ink" commit="abc123"/>
<dependencies>
<group targetFramework="net8.0"><dependency id="Yoga.Net" version="3.2.3"/>
<dependency id="Wcwidth" version="4.0.1"/></group>
<group targetFramework="net10.0"><dependency id="Yoga.Net" version="3.2.3"/>
<dependency id="Wcwidth" version="4.0.1"/></group></dependencies>
<frameworkReferences>
<group targetFramework="net8.0"><frameworkReference name="Microsoft.AspNetCore.App"/></group>
<group targetFramework="net10.0"><frameworkReference name="Microsoft.AspNetCore.App"/></group>
</frameworkReferences></metadata></package>"""
MANIFEST = MANIFEST.replace("0.1.0-preview.1", version)
MANIFEST = MANIFEST.replace("3.2.3", dependencies["Yoga.Net"]).replace("4.0.1", dependencies["Wcwidth"])

with tempfile.TemporaryDirectory() as directory:
    package = Path(directory) / "test.nupkg"
    restored = Path(directory) / "cache/blazor.ink" / version.lower() / f"blazor.ink.{version.lower()}.nupkg"
    restored.parent.mkdir(parents=True)
    restored.write_bytes(b"different published package")

    def write(manifest=MANIFEST, extra=None):
        with ZipFile(package, "w") as archive:
            archive.writestr("Blazor.Ink.nuspec", manifest)
            for name in ("README.md", "LICENSE", "THIRD-PARTY-NOTICES.md",
                         "lib/net8.0/Blazor.Ink.dll", "lib/net10.0/Blazor.Ink.dll"):
                archive.writestr(name, "")
            if extra:
                archive.writestr(extra, "")

    write()
    assert check_package(package, tag=f"v{version}",
                         repository="https://github.com/example/Blazor.Ink", commit="abc123")
    cases = [
        (MANIFEST, None, {"tag": f"v{version}-wrong"}),
        (MANIFEST, None, {"repository": "https://github.com/other/repo"}),
        (MANIFEST, None, {"commit": "different"}),
        (MANIFEST, None, {"published": True}),
        (MANIFEST, None, {"restored_cache": str(Path(directory) / "cache")}),
        (MANIFEST.replace(dependencies["Yoga.Net"], "0.0.0-not-restored"), None, {}),
        (MANIFEST.replace("Yoga.Net", "Vendor.Yoga"), None, {}),
        (MANIFEST.replace("net8.0", "net9.0"), None, {}),
        (MANIFEST.replace("<id>Blazor.Ink</id>", "<id>Other.Package</id>"), None, {}),
        (MANIFEST, "vendor/source.cs", {}),
        (MANIFEST, "lib/net8.0/Yoga.Net.dll", {}),
    ]
    for manifest, extra, options in cases:
        write(manifest, extra)
        try:
            check_package(package, **options)
        except ValueError:
            continue
        raise AssertionError(f"Accepted an invalid release package: {extra}, {options}")
    write(extra=".signature.p7s")
    assert check_package(package, published=True)
    restored.write_bytes(package.read_bytes())
    assert check_package(package, restored_cache=str(Path(directory) / "cache"))
print("PASS package inspection and release failure gates")
