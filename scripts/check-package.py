"""Inspect the release artifact without loading its assemblies."""
import argparse
import filecmp
import re
import xml.etree.ElementTree as ET
from pathlib import Path
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parent.parent
FRAMEWORKS = {"net8.0", "net10.0"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def check_package(path, *, tag=None, repository=None, commit=None, symbols=None, published=False,
                  restored_cache=None):
    project = ET.parse(ROOT / "src/Blazor.Ink/Blazor.Ink.csproj")
    dependencies = {
        item.attrib["Include"]: item.attrib["Version"]
        for item in project.findall(".//PackageReference")
    }
    version = ET.parse(ROOT / "Directory.Build.props").findtext(".//Version")
    with ZipFile(path) as archive:
        names = set(archive.namelist())
        require([name for name in names if name.endswith(".nuspec")] == ["Blazor.Ink.nuspec"],
                "Expected only the Blazor.Ink manifest")
        metadata = ET.fromstring(archive.read("Blazor.Ink.nuspec"))
        for element in metadata.iter():
            element.tag = element.tag.rsplit("}", 1)[-1]
        metadata = metadata.find("metadata")
        require(metadata is not None, "Missing package metadata")
        require(metadata.findtext("id") == "Blazor.Ink", "Incorrect package ID")
        require(metadata.findtext("version") == version, "Package version differs from project version")
        require(re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", version), "Invalid version")
        require(tag is None or tag == f"v{version}", "Release tag differs from package version")
        require(metadata.findtext("authors") == "Blazor.Ink contributors", "Incorrect authors")
        license_node = metadata.find("license")
        require(license_node is not None and license_node.get("type") == "expression"
                and license_node.text == "MIT", "Expected MIT license metadata")
        require(metadata.findtext("readme") == "README.md", "Incorrect package readme")
        require({"README.md", "LICENSE", "THIRD-PARTY-NOTICES.md"} <= names, "Missing notices/readme")
        groups = metadata.findall("dependencies/group")
        require(len(groups) == 2 and {group.get("targetFramework") for group in groups} == FRAMEWORKS,
                "Incorrect dependency frameworks")
        for group in groups:
            items = group.findall("dependency")
            require(len(items) == len(dependencies) and
                    {item.get("id"): item.get("version") for item in items} == dependencies,
                    "Expected only pinned upstream dependencies")
        groups = metadata.findall("frameworkReferences/group")
        require(len(groups) == 2 and {group.get("targetFramework") for group in groups} == FRAMEWORKS,
                "Incorrect shared-framework targets")
        for group in groups:
            require([item.get("name") for item in group] == ["Microsoft.AspNetCore.App"],
                    "Missing ASP.NET Core framework reference")
        expected = {f"lib/{framework}/Blazor.Ink.dll" for framework in FRAMEWORKS}
        require({name for name in names if name.endswith(".dll")} == expected,
                "Unexpected assemblies or missing framework assets")
        require(not any(name.startswith(("vendor/", "tests/", "samples/", "artifacts/", "src/"))
                        or name.endswith((".cs", ".razor", ".pfx", ".snk")) for name in names),
                "Source, temporary files, or keys must not be packaged")
        repo = metadata.find("repository")
        if repository is not None:
            require(repo is not None and repo.get("type") == "git" and repo.get("url") == repository,
                    "Repository URL differs from the release repository")
            require(metadata.findtext("projectUrl") == repository, "Incorrect package project URL")
        if commit is not None:
            require(repo is not None and repo.get("commit") == commit, "Package commit differs from checkout")
        if published:
            require(".signature.p7s" in names, "Published package has no signature")
    if restored_cache is not None:
        normalized = version.lower()
        restored = Path(restored_cache) / "blazor.ink" / normalized / f"blazor.ink.{normalized}.nupkg"
        require(restored.is_file() and filecmp.cmp(path, restored, shallow=False),
                "Consumer did not restore the exact locally built package")
    if symbols is not None:
        with ZipFile(symbols) as archive:
            require({name for name in archive.namelist() if name.endswith(".pdb")} ==
                    {f"lib/{framework}/Blazor.Ink.pdb" for framework in FRAMEWORKS},
                    "Missing portable-symbol assets")
    return version


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package")
    parser.add_argument("--tag")
    parser.add_argument("--repository")
    parser.add_argument("--commit")
    parser.add_argument("--symbols")
    parser.add_argument("--published", action="store_true")
    parser.add_argument("--restored-cache")
    arguments = vars(parser.parse_args())
    package = arguments.pop("package")
    try:
        result = check_package(package, **arguments)
    except (ValueError, ET.ParseError) as error:
        parser.exit(1, f"Package check failed: {error}\n")
    print(f"PASS Blazor.Ink {result} package metadata/content")
