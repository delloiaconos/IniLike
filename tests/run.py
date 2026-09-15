#!/usr/bin/env python3
"""Build the real library and run isolated regression tests (Python 3 + Mono)."""
import argparse
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--operator-ui", type=Path, help="Optional OperatorUI assembly for CompatibilityProbe")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    for tool in ("xbuild", "mcs", "mono"):
        if not shutil.which(tool):
            parser.error("Missing prerequisite: " + tool)
    operator_ui = args.operator_ui.resolve() if args.operator_ui else None
    if operator_ui and not operator_ui.is_file():
        parser.error("OperatorUI assembly does not exist: " + str(operator_ui))

    def run(command, cwd):
        print("+ " + " ".join(map(str, command)), flush=True)
        subprocess.run(list(map(str, command)), cwd=str(cwd), check=True, timeout=120)

    try:
        with tempfile.TemporaryDirectory(prefix="inilike-tests-") as temp:
            build = Path(temp)
            run(["xbuild", root / "IniLike.sln", "/verbosity:minimal",
                 "/p:Configuration=" + args.configuration,
                 "/p:OutputPath=" + str(build) + "/",
                 "/p:IntermediateOutputPath=" + str(build / "obj") + "/"], build)
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
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        print("FAIL: " + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
