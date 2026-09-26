#!/usr/bin/env python3
"""Build with MSBuild (or legacy xbuild) and run isolated tests with Mono."""
import argparse
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--msbuild", help="Explicit build executable; otherwise prefer msbuild, then xbuild")
    parser.add_argument("--xbuild", default="xbuild", help="Fallback xbuild executable (default: xbuild)")
    parser.add_argument("--operator-ui", type=Path, help="Optional OperatorUI assembly for CompatibilityProbe")
    parser.add_argument("--examples-only", action="store_true",
                        help="Build and validate all examples/*.ini with ConfigurationValidator")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    if args.msbuild:
        build_tool = shutil.which(args.msbuild)
        if not build_tool:
            parser.error("Missing prerequisite: " + args.msbuild)
    else:
        build_tool = shutil.which("msbuild")
        if not build_tool:
            build_tool = shutil.which(args.xbuild)
            if not build_tool:
                parser.error("Missing build tool: install msbuild or xbuild, or specify --msbuild /path/to/executable")
            print("MSBuild is unavailable; using deprecated xbuild: " + build_tool, flush=True)
    for tool in (("mono",) if args.examples_only else ("mcs", "mono")):
        if not shutil.which(tool):
            parser.error("Missing prerequisite: " + tool)
    msbuild = Path(build_tool).resolve()
    operator_ui = args.operator_ui.resolve() if args.operator_ui else None
    if operator_ui and not operator_ui.is_file():
        parser.error("OperatorUI assembly does not exist: " + str(operator_ui))

    def run(command, cwd):
        print("+ " + " ".join(map(str, command)), flush=True)
        subprocess.run(list(map(str, command)), cwd=str(cwd), check=True, timeout=120)

    try:
        with tempfile.TemporaryDirectory(prefix="inilike-tests-") as temp:
            build = Path(temp)
            run([msbuild, root / "IniLike.sln", "/verbosity:minimal",
                 "/p:Configuration=" + args.configuration,
                 "/p:OutputPath=" + str(build) + "/",
                 "/p:IntermediateOutputPath=" + str(build / "obj") + "/"], build)
            if args.examples_only:
                examples = sorted((root / "examples").glob("*.ini"))
                examples = [path for path in examples if path.is_file()]
                if not examples:
                    print("FAIL: No .ini files found in examples.", file=sys.stderr)
                    return 1
                failed = 0
                for example in examples:
                    try:
                        run(["mono", build / "ConfigurationValidator.exe", example], build)
                    except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
                        print("FAIL: {0}: {1}".format(example.name, error), file=sys.stderr)
                        failed += 1
                print("Examples: {0} passed, {1} failed.".format(len(examples) - failed, failed), flush=True)
                return 1 if failed else 0
            library = build / "ConfigurationFilesReader.dll"
            sources = ["ConfigurationFileTests"]
            if operator_ui:
                sources.append("CompatibilityProbe")
            for name in sources:
                executable = build / (name + ".exe")
                run(["mcs", "-warn:4", "-r:" + str(library), "-out:" + str(executable),
                     root / "tests" / (name + ".cs")], build)
                arguments = [library, operator_ui] if name == "CompatibilityProbe" else []
                run(["mono", executable] + arguments, build)
            run([sys.executable, root / "tests" / "validator.py",
                 build / "ConfigurationValidator.exe", root / "examples"], build)
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        print("FAIL: " + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
