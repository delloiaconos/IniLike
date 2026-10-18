"""Optional executable comparisons against this repository's C# sources."""

import io
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

from test_configuration import ConfigurationFile, ConfigurationError, ROOT


@unittest.skipUnless(shutil.which("mcs") and shutil.which("mono"), "C# parity requires mcs and mono")
class CSharpParityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = tempfile.TemporaryDirectory(prefix="inilike-python-parity-")
        cls.root = Path(cls.temp.name)
        cls.addClassCleanup(cls.temp.cleanup)
        probe = cls.root / "Probe.cs"
        probe.write_text(r'''using System;
using System.IO;
using System.Globalization;
using System.Reflection;
using ConfigurationFilesReader;
class Probe {
    static int Main(string[] Args) {
        try {
            ConfigurationFile Config = new ConfigurationFile(Args[0], Args[1] == "true");
            if (Args.Length == 2) {
                using (StreamWriter Dump = Config.dump())
                using (StreamReader Reader = new StreamReader(Dump.BaseStream)) {
                    Console.Write(Reader.ReadToEnd());
                }
            } else {
                Type ValueType = Args[3] == "bool" ? typeof(bool) : Args[3] == "int" ? typeof(int) :
                    Args[3] == "long" ? typeof(long) : Args[3] == "float" ? typeof(float) :
                    Args[3] == "double" ? typeof(double) : typeof(string);
                bool Try = Args[2] == "try";
                object Default = ValueType == typeof(bool) ? (object)true : ValueType == typeof(string) ? (object)"fallback" :
                    Convert.ChangeType(9, ValueType, CultureInfo.InvariantCulture);
                object[] Parameters = { "S", "k", Try ? null : Default };
                MethodInfo Method = typeof(ConfigurationFile).GetMethod(Try ? "tryGetParameter" : "getParameter",
                    new Type[] { typeof(string), typeof(string), Try ? ValueType.MakeByRefType() : ValueType });
                object Result = Method.Invoke(Config, Parameters);
                object Value = Try ? Parameters[2] : Result;
                Console.Write((Try ? ((bool)Result ? "true" : "false") : "get") + "\t" +
                    (Value == null ? "<null>" : Convert.ToString(Value, CultureInfo.InvariantCulture)));
            }
            return 0;
        } catch (Exception Error) {
            Console.Error.WriteLine(Error.Message);
            return 1;
        }
    }
}''', encoding="utf-8")
        cls.exe = cls.root / "Probe.exe"
        sources = sorted((ROOT / "ConfigurationFilesReader").glob("*.cs"))
        subprocess.run(["mcs", "-warn:4", "-out:" + str(cls.exe), *map(str, sources), str(probe)],
                       check=True, capture_output=True, text=True, timeout=60)

    def csharp(self, path, sensitive=True, *args):
        return subprocess.run(["mono", str(self.exe), str(path), str(sensitive).lower(), *args],
                              cwd=self.root, capture_output=True, text=True, timeout=20)

    def test_all_examples_canonical_dump(self):
        for path in sorted((ROOT / "examples").glob("*.ini")):
            with self.subTest(example=path.name):
                result = self.csharp(path)
                self.assertEqual(0, result.returncode, result.stderr)
                config = ConfigurationFile(path)
                with config.dump() as dump:
                    self.assertEqual(result.stdout, dump.read())

    def test_parser_acceptance_and_dump(self):
        fixtures = [
            "[S]\nk=a=b; ## note\n[END:S] ## close\n",
            "[S]\nk=1\nk=2\n", "[S]\n[END:wrong]\n", "[S]\nK=1\nk=2\n",
            "[TBL:T]\n\ta\tb\t\n[bracket]\nlast\n", "[table:T]\nk=v\n",
            "[TXT:X] ## header\n ## raw\n\n[END] ## close\n",
            "[ENC:E]\nAAE\nC/4A=\n", "[ENC:E]\n!\n", "[ENC:E]\nZg===\n",
            "[S]\n\nempty=\n[DICT:S]\nk=value.,;\n[LIST:S]\nitem=val.,;\n",
            "[S]\nK=1\n[s]\nk=2\n", "[S]\nk=1\n[END:s]\n",
            "[SEC: ]\n=v\n[END:]\n", "[TAB:T]\n;\n,\n.\n",
        ]
        for index, text in enumerate(fixtures):
            path = self.root / f"fixture-{index}.ini"
            path.write_text(text, encoding="utf-8")
            for sensitive in (True, False):
                with self.subTest(index=index, sensitive=sensitive):
                    result = self.csharp(path, sensitive)
                    if result.returncode:
                        with self.assertRaises(ConfigurationError):
                            ConfigurationFile(path, CaseSensitive=sensitive)
                    else:
                        with ConfigurationFile(path, CaseSensitive=sensitive).dump() as dump:
                            self.assertEqual(result.stdout, dump.read())

    def test_typed_conversion_results(self):
        for kind, values in {
            "bool": ["TRUE", "0", "yes", "FALSE"],
            "int": ["2147483648", "2147483647", "-2147483649", "invalid"],
            "long": ["9223372036854775807", "9223372036854775808", "1.25"],
            "double": ["1,25", "1e999", "NaN", "Infinity", "1_000"],
            "float": ["1.25", "1e100", "NaN"],
            "string": ["a=b;", "", "literal"],
        }.items():
            for value in values:
                path = self.root / "typed.ini"
                path.write_text("[S]\nk=" + value + "\n", encoding="utf-8")
                for mode in ("try", "get"):
                    with self.subTest(kind=kind, value=value, mode=mode):
                        config = ConfigurationFile(path)
                        if mode == "try":
                            success, actual = config.tryGetParameter("S", "k", kind)
                            prefix = str(success).lower()
                        else:
                            default = True if kind == "bool" else "fallback" if kind == "string" else 9
                            actual = config.getParameter("S", "k", default, kind)
                            prefix = "get"
                        if isinstance(actual, float):
                            rendered = format(actual, ".15g").replace("nan", "NaN").replace("inf", "Infinity")
                        else:
                            rendered = "<null>" if actual is None else str(actual)
                        result = self.csharp(path, True, mode, kind)
                        self.assertEqual(0, result.returncode, result.stderr)
                        self.assertEqual(result.stdout, prefix + "\t" + rendered)

    def test_python_saved_file_readable_by_csharp(self):
        config = ConfigurationFile(io.StringIO("[S]\nk=v\n[TBL:T]\nrow;\n[LST:L]\n[DICT:D]\n[TXT:X]\n text \n[END]\n[ENC:E]\nZg==\n"))
        config.autoUpdateRegistry = True
        config.setParameter("S", "new", "a=b.,;")
        config.getTable("T").append("new;")
        config.getList("L").append("item,")
        config.getDictionary("D")["key"] = "value."
        path = self.root / "saved.ini"
        config.save(path)
        result = self.csharp(path)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(path.read_text(encoding="utf-8"), result.stdout)
