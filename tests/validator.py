"""Exercise the real console application and its exit codes without changing inputs."""
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

        def check(arguments, code, message, error=False):
            nonlocal checked
            result = subprocess.run(["mono", str(executable)] + arguments, cwd=folder,
                                    capture_output=True, text=True, timeout=30)
            assert result.returncode == code, result
            assert message in (result.stderr if error else result.stdout), result
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
    print("PASS: {0} console validation checks; input files unchanged.".format(checked))


if __name__ == "__main__":
    main()
