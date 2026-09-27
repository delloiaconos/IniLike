"""Exercise console validation, listings, rewriting and exit codes."""
from pathlib import Path
import subprocess
import sys
import tempfile


def main():
    executable = Path(sys.argv[1]).resolve()
    examples = Path(sys.argv[2]).resolve()
    checked = 0
    with tempfile.TemporaryDirectory(prefix="inilike-validator-") as folder:
        root = Path(folder)

        def check(arguments, code, message, error=False, exact=False):
            nonlocal checked
            result = subprocess.run(["mono", str(executable)] + arguments, cwd=folder,
                                    capture_output=True, text=True, timeout=30)
            assert result.returncode == code, result
            assert message in (result.stderr if error else result.stdout), result
            if exact:
                assert result.stdout == message, result
            if code == 0:
                assert not result.stderr, result.stderr
            elif error:
                assert not result.stdout, result.stdout
            checked += 1

        for example in sorted(examples.glob("*.ini")):
            original = example.read_bytes()
            check([str(example)], 0, "Configuration loaded successfully")
            assert example.read_bytes() == original
        check([], 2, "expected exactly one", True)
        check(["one.ini", "two.ini"], 2, "expected exactly one", True)
        check([""], 2, "expected exactly one", True)
        check(["--help"], 0, "Exit codes:")
        check(["missing.ini"], 1, "Unable to open file:", True)
        cases = [
            ("valid file.ini", "[SEC:S]\nk=v\n[END:S]\n[LST:L]\na=b\n[END]\n", 0, "Sections: 1; tables: 0; lists: 1; dictionaries: 0."),
            ("empty.ini", "", 0, "Sections: 0; tables: 0; lists: 0; dictionaries: 0."),
            ("dictionary.ini", "[DICT:CHANNELS]\nx=3\ny=3.14\n[END]\n", 0, "dictionaries: 1."),
            ("duplicate-dictionary.ini", "[DICT:D]\nx=1\nx=2\n", 1, "ArgumentException:"),
            ("dictionary-end.ini", "[DICTIONARY:D]\nx=1\n[END:OTHER]\n", 1, "End label does not match the current block: 'D'."),
            ("ignored.ini", "outside=x\n[S]\ninvalid\nkey=a=b\n", 0, "Configuration loaded successfully"),
            ("duplicate.ini", "[S]\nk=1\nk=2\n", 1, "ArgumentException:"),
            ("duplicate-list.ini", "[LIST:L]\n[END]\n[LST:L]\n", 1, "ArgumentException:"),
            ("mismatch.ini", "[TABLE:T]\nrow\n[END:OTHER]\n", 1, "End label does not match the current block: 'T'."),
        ]
        for name, content, code, message in cases:
            path = root / name
            path.write_text(content, encoding="utf-8")
            original = path.read_bytes()
            check([name], code, message, error=code != 0)
            assert path.read_bytes() == original
        assert sorted(p.name for p in root.iterdir()) == sorted(case[0] for case in cases)
        listing = root / "listing file.ini"
        listing.write_text("[SECTION:Zulu]\nshared=1\nz=2\n[SEC:Alpha]\nshared=3\nA=4\n"
                           "[Plain]\na=5\n[DICT:Zulu]\ndictOnly=6\n[DICTIONARY:Alpha]\nx=7\n"
                           "[TBL:Zulu]\ntableOnly=8\n[TABLE:Alpha]\nrow\n"
                           "[LST:Zulu]\nlistOnly=9\n[LIST:Alpha]\nitem\n", encoding="utf-8")
        original = listing.read_bytes()
        outputs = {
            "--sections": "Sections:\nAlpha\nPlain\nZulu\n",
            "--dictionaries": "Dictionaries:\nAlpha\nZulu\n",
            "--tables": "Tables:\nAlpha\nZulu\n",
            "--lists": "Lists:\nAlpha\nZulu\n",
            "--parameters": "Parameters:\nA\na\nshared\nz\n",
        }
        for option, output in outputs.items():
            check([option, listing.name], 0, output, exact=True)
            check([listing.name, option], 0, output, exact=True)
            check([option, "empty.ini"], 0, output.splitlines()[0] + "\n", exact=True)
            check([option], 2, "expected exactly one", True)
        check(list(reversed(outputs)) + [listing.name], 0, "".join(outputs.values()), exact=True)
        check(["--sections", listing.name, "--sections"], 0, outputs["--sections"], exact=True)
        check(["--unknown", listing.name], 2, "unknown option", True)
        check(["--sections", listing.name, "empty.ini"], 2, "expected exactly one", True)
        check(["--parameters", "missing.ini"], 1, "Unable to open file:", True)
        check(["--sections", "mismatch.ini"], 1, "FormatException:", True)
        dashed = root / "--sections"
        dashed.write_bytes(original)
        check(["--parameters", "--", dashed.name], 0, outputs["--parameters"], exact=True)
        assert listing.read_bytes() == original
        assert dashed.read_bytes() == original
        canonical = ("[Alpha]\nA=4;\nshared=3;\n[Plain]\na=5;\n[Zulu]\nshared=1;\nz=2;\n"
                     "[TABLE:Alpha]\nrow;\n[TABLE:Zulu]\ntableOnly=8;\n"
                     "[LIST:Alpha]\nitem;\n[LIST:Zulu]\nlistOnly=9;\n"
                     "[DICT:Alpha]\nx=7;\n[DICT:Zulu]\ndictOnly=6;\n")
        check([listing.name, "--rewrite"], 0, canonical, exact=True)
        check(["--rewrite", listing.name], 0, canonical, exact=True)
        check(["empty.ini", "--rewrite"], 0, "", exact=True)
        check(["--rewrite"], 2, "expected exactly one", True)
        check([listing.name, "--rewrite", ""], 2, "expected exactly one", True)
        check([listing.name, "--rewrite", "a.ini", "b.ini"], 2, "expected exactly one", True)
        for option in outputs:
            check([listing.name, "--rewrite", option], 2, "cannot be combined", True)
        destination = root / "rewritten.ini"
        check([listing.name, "--rewrite", destination.name], 0, "", exact=True)
        assert destination.read_text(encoding="utf-8") == canonical
        check([destination.name, "--rewrite"], 0, canonical, exact=True)
        destination.write_text("old contents", encoding="utf-8")
        check([listing.name, "--rewrite", str(destination)], 0, "", exact=True)
        assert destination.read_text(encoding="utf-8") == canonical
        check(["mismatch.ini", "--rewrite", str(destination)], 1, "Error loading", True)
        assert destination.read_text(encoding="utf-8") == canonical
        check([listing.name, "--rewrite", "missing-directory/out.ini"], 1, "Error rewriting", True)
        check([listing.name, "--rewrite", str(root)], 1, "Error rewriting", True)
        assert not list(root.glob(".inilike-*.tmp"))
        subdir = root / "output folder"
        subdir.mkdir()
        check([listing.name, "--rewrite", "output folder/copy file.ini"], 0, "", exact=True)
        assert (subdir / "copy file.ini").read_text(encoding="utf-8") == canonical
        check([listing.name, "--rewrite", "--", "-output.ini"], 0, "", exact=True)
        assert (root / "-output.ini").read_text(encoding="utf-8") == canonical
        assert listing.read_bytes() == original
        check([destination.name, "--rewrite", destination.name], 0, "", exact=True)
        assert destination.read_text(encoding="utf-8") == canonical
        unicode_file = root / "unicode.ini"
        unicode_file.write_text("## comment\n[SEC:É]\nname=caffè\n[END]\n", encoding="utf-8")
        check([unicode_file.name, "--rewrite"], 0, "[É]\nname=caffè;\n", exact=True)
    print("PASS: {0} console validation checks; rewrite and input preservation verified.".format(checked))


if __name__ == "__main__":
    main()
