import codecs
import io
import math
import os
import pickle
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

PROJECT = Path(__file__).resolve().parents[1]
ROOT = PROJECT.parent
sys.path.insert(0, str(PROJECT))
from configuration_file_reader import ConfigurationFile, ConfigurationFileReader, ConfigurationError, NullParameterValueException


class ConfigurationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="inilike-python-")
        self.original = Path.cwd()
        os.chdir(self.directory.name)

    def tearDown(self):
        os.chdir(self.original)
        self.directory.cleanup()

    def load(self, text, sensitive=True):
        return ConfigurationFile(io.StringIO(text), CaseSensitive=sensitive)

    def test_aliases_and_endings(self):
        for aliases, kind, body in [
            (("S", "SEC:S", "SECTION:S"), "Section", "k=a=b;"),
            (("DICT:S", "DICTIONARY:S"), "Dictionary", "k=a=b;"),
            (("TABLE:S", "TBL:S", "TAB:S"), "Table", "a;b;"),
            (("LIST:S", "LST:S"), "List", "a=b;"),
            (("TEXT:S", "TXT:S"), "Text", " raw ## text "),
            (("ENCODED:S", "ENC:S", "BASE64:S", "B64:S"), "Encoded", "Zg=="),
        ]:
            for header in aliases:
                for end in ("", "[END]", "[END:S]"):
                    with self.subTest(header=header, end=end):
                        config = self.load(f"[{header}]\n{body}\n{end}\n")
                        self.assertTrue(getattr(config, "contains" + kind)("S"))
                        self.assertTrue(getattr(config, "tryGet" + kind)("S")[0])
                        if kind == "Section":
                            self.assertEqual("a=b;", config.getParameter("S", "k", "fallback"))
                        config.save("copy.ini")
                        self.assertTrue(config._sameData(ConfigurationFile("copy.ini")))

    def test_comments_and_punctuation(self):
        config = self.load("[S] ## section ]\na=value.,;; ## note\nb=a=b##note\nempty=\n[END]##done\n[TBL:T]\n;row; ## note\n## skip\n\n[END:T] ## done\n[TXT:X] ## header\n ## literal\n\n  raw;  \n[END] ## done\n[ENC:E]\nZg== ## encoded\n")
        self.assertEqual("value.,;;", config.getParameter("S", "a", ""))
        self.assertEqual("a=b", config.getParameter("S", "b", ""))
        self.assertEqual([";row;"], config.getTable("T"))
        self.assertEqual(" ## literal\n\n  raw;  ", config.getText("X"))
        self.assertEqual(b"f", config.getEncoded("E"))
        self.assertEqual("", config.getParameter("S", "empty", "fallback"))
        for value in (";", ",", ".", "...", "a;,.;;"):
            config = self.load(f"[S]\nk={value}\n[DICT:D]\nk={value}\n[TBL:T]\n{value}\n[LST:L]\n{value}\n")
            config.save("punctuation.ini")
            copy = ConfigurationFile("punctuation.ini")
            self.assertEqual(value, copy.getParameter("S", "k", ""))
            self.assertEqual(value, copy.getDictionary("D")["k"])
            self.assertEqual([value], copy.getTable("T"))
            self.assertEqual([value], copy.getList("L"))

    def test_permissive_rows_and_headers(self):
        config = self.load("outside=x\n[table:lower]\nx=1\n[TBL:T]\n\ta\tb\t\n[bracketed]\nignored\nkey=a=b\n")
        self.assertEqual(["a\tb"], config.getTable("T"))
        self.assertTrue(config.containsSection("bracketed"))
        self.assertTrue(config.containsSection("table:lower"))
        self.assertEqual("a=b", config.getParameter("bracketed", "key", ""))
        self.assertFalse(config.containsParameter("bracketed", "ignored"))

    def test_case_comparison_and_duplicates(self):
        for sensitive in (True, False):
            config = self.load("[S]\nKey=v\n[DICT:S]\nKey=v\n[TBL:S]\na\n[LST:S]\nb\n[TXT:S]\nc\n[END]\n[ENC:S]\nZg==\n", sensitive)
            for kind in ("Section", "Dictionary", "Table", "List", "Text", "Encoded"):
                self.assertEqual(not sensitive, getattr(config, "contains" + kind)("s"))
            self.assertEqual(not sensitive, config.containsParameter("s", "key"))
            if not sensitive:
                config.getDictionary("s")["KEY"] = "changed"
                self.assertEqual(["Key"], list(config.getDictionary("S")))
                self.assertEqual("changed", config.getDictionary("s")["key"])
            with self.assertRaises(AttributeError):
                config.CaseSensitive = False
        self.load("[SEC:S]\n[END:s]\n", False)
        with self.assertRaises(ConfigurationError):
            self.load("[SEC:S]\n[END:s]\n")
        for header in ("SEC", "DICT", "TBL", "LST", "TXT", "ENC"):
            self.load(f"[{header}:S]\n[{header}:s]\n")
            with self.assertRaises(ConfigurationError):
                self.load(f"[{header}:S]\n[{header}:s]\n", False)
        for header in ("SEC", "DICT"):
            with self.assertRaises(ConfigurationError):
                self.load(f"[{header}:S]\nKey=x\nKEY=y\n", False)
        config = self.load("[S]\nß=x\nSS=y\n", False)
        self.assertEqual(2, len(config.listParameters("S")))

    def test_presence_and_ownership(self):
        config = self.load("[S]\n[TBL:S]\n[LST:S]\n[DICT:S]\n[TXT:S]\n[END]\n[ENC:S]\n")
        config.autoUpdateRegistry = config.autoSaveRegistry = True
        for kind in ("Section", "Table", "List", "Dictionary", "Text", "Encoded"):
            contains = getattr(config, "contains" + kind)
            read = getattr(config, "tryGet" + kind)
            self.assertTrue(contains("S"))
            self.assertEqual((False, None), read("Missing"))
            success, value = read("S")
            self.assertTrue(success)
            self.assertEqual(0, len(value))
            with self.assertRaises(TypeError):
                read(None)
            with self.assertRaises(TypeError):
                contains(None)
        config.tryGetSection("S")[1]["detached"] = "x"
        self.assertFalse(config.containsParameter("S", "detached"))
        config.getTable("S").append("row")
        config.getList("S").append("item")
        config.getDictionary("S")["key"] = "value"
        self.assertEqual(["row"], config.tryGetTable("S")[1])
        self.assertIs(config.getList("S"), config.tryGetList("S")[1])
        self.assertIs(config.getDictionary("S"), config.tryGetDictionary("S")[1])
        config.getTable("missing").append("detached")
        self.assertEqual([], config.getTable("missing"))
        self.assertEqual("", config.getText("missing"))
        self.assertEqual(b"", config.getEncoded("missing"))
        self.assertEqual([], list(Path('.').iterdir()))

    def test_sorted_listings(self):
        config = self.load("[z]\na=x\n[A]\na=y\nB=z\n[TBL:z]\n[TBL:A]\n", False)
        self.assertEqual(["A", "z"], config.listSections())
        self.assertEqual(["A", "z"], config.listTables())
        self.assertEqual(["B", "a"], config.listParameters())
        config.listSections().clear()
        self.assertEqual(2, len(config.listSections()))
        self.assertEqual([], config.listParameters("missing"))
        with self.assertRaises(TypeError):
            config.listParameters(None)

    def test_parameter_creation_flags(self):
        for enabled in (False, True):
            config = self.load("[S]\nk=original\n")
            config.autoUpdateRegistry = enabled
            config.setParameter("S", "k", "updated")
            config.addParameter("S", "k", "ignored")
            config.addParameter("New", "k", "created")
            self.assertEqual(enabled, config.containsSection("New"))
            self.assertEqual("updated", config.getParameter("S", "k", ""))
            self.assertEqual(42, config.getParameter("Default", "n", 42))
            self.assertEqual(enabled, config.containsParameter("Default", "n"))
            config.autoUpdateRegistry = True
            config.setParameter("Other", "k", "value")
            self.assertEqual("value", config.getParameter("Other", "k", ""))
            with self.assertRaises(TypeError):
                config.setParameter("invalid", None, "v")
            self.assertFalse(config.containsSection("invalid"))

    def test_typed_reads(self):
        config = ConfigurationFile()
        config.autoUpdateRegistry = True
        cases = [
            ("bool", "TRUE", True, True), ("bool", "0", True, False), ("bool", "yes", False, False),
            ("int", "2147483647", True, 2147483647), ("int", "2147483648", False, 0),
            ("long", "2147483648", True, 2147483648), ("long", "9223372036854775808", False, 0),
            ("double", " 1,25 ", True, 1.25), ("float", "1.25", True, 1.25),
            ("float", "1e100", False, 0), ("double", "1e999", False, 0),
            ("double", "NaN", False, 0), ("double", "Infinity", False, 0),
            ("int", "1_000", False, 0), ("double", "1_000", False, 0),
        ]
        for kind, text, success, value in cases:
            with self.subTest(kind=kind, text=text):
                config.setParameter("S", "k", text)
                self.assertEqual((success, value), config.tryGetParameter("S", "k", kind))
        config.setParameter("S", "k", "2147483648")
        self.assertEqual(-2147483648, config.getParameter("S", "k", 42))
        self.assertEqual(2147483648, config.getParameter("S", "k", 42, "long"))
        config.setParameter("S", "k", "yes")
        self.assertFalse(config.getParameter("S", "k", True))
        config.setParameter("S", "k", "invalid")
        self.assertEqual(1.25, config.getParameter("S", "k", 1.25))
        config.setParameter("S", "k", "1e100")
        self.assertTrue(math.isinf(config.getParameter("S", "k", 1.25, "float")))
        self.assertEqual((False, 0), config.tryGetParameter("missing", "k", int))

    def test_null_semantics(self):
        config = ConfigurationFile()
        config.autoUpdateRegistry = True
        config.addParameter("S", "k", None)
        config.autoSaveRegistry = True
        for kind, default in (("bool", True), ("int", 42), ("long", 42), ("float", 1.25), ("double", 1.25)):
            self.assertEqual(default, config.getParameter("S", "k", default, kind))
            with self.assertRaises(NullParameterValueException) as caught:
                config.tryGetParameter("S", "k", kind)
            self.assertEqual("S", caught.exception.SectionName)
            self.assertEqual("k", caught.exception.ParameterName)
            self.assertEqual("k", pickle.loads(pickle.dumps(caught.exception)).ParameterName)
        self.assertEqual((True, None), config.tryGetParameter("S", "k"))
        self.assertTrue(config.containsParameter("S", "k"))
        self.assertIsNone(config.getParameter("S", "k", "fallback"))
        self.assertEqual(["k"], config.listParameters("S"))

    def test_dump_and_save_destinations(self):
        source = "## comment\n[SEC:S]\nk=value;\n[TBL:T]\nrow.\n"
        Path("input.ini").write_text(source)
        config = ConfigurationFile(Path("input.ini"))
        self.assertIs(ConfigurationFile, ConfigurationFileReader)
        with config.dump() as dump:
            expected = "[S]\nk=value;\n[TABLE:T]\nrow.\n"
            self.assertEqual(0, dump.tell())
            self.assertEqual(expected, dump.read())
        writer = io.StringIO()
        writer.write("prefix")
        self.assertIsNone(config.dump(writer))
        self.assertEqual("prefix" + expected, writer.getvalue())
        self.assertFalse(writer.closed)
        Path("output").mkdir()
        config.save("output", "copy.ini")
        self.assertEqual(source, Path("input.ini").read_text())
        config.setParameter("S", "k", "updated")
        config.save()
        self.assertEqual("updated", ConfigurationFile("input.ini").getParameter("S", "k", ""))
        self.assertEqual(expected, Path("output/copy.ini").read_text())
        self.assertEqual(expected, ConfigurationFile(Path("output"), "copy.ini").dump().read())
        config.save(Path("output/new.ini"))
        self.assertEqual([], list(Path('.').glob('.inilike-*.tmp')))
        self.assertFalse(Path("input.ini").read_bytes().startswith(codecs.BOM_UTF8))

    def test_atomic_save_rejects_loss(self):
        config = ConfigurationFile()
        config.autoUpdateRegistry = True
        Path("destination.ini").write_text("keep")
        for value in (None, " leading", "trailing ", "line\nbreak", "value ## comment"):
            config.setParameter("S", "k", value)
            with self.assertRaises(ValueError):
                config.save("destination.ini")
            self.assertEqual("keep", Path("destination.ini").read_text())
            self.assertEqual([], list(Path('.').glob('.inilike-*.tmp')))
        config.setParameter("S", "k", "valid")
        with patch("configuration_file_reader._persistence.os.replace", side_effect=OSError("replace failed")):
            with self.assertRaises(OSError):
                config.save("destination.ini")
        self.assertEqual("keep", Path("destination.ini").read_text())
        self.assertEqual([], list(Path('.').glob('.inilike-*.tmp')))
        with self.assertRaises(FileNotFoundError):
            config.save("missing/file.ini")
        with self.assertRaises(ValueError):
            config.save()

    def test_live_mutations_validate_on_save(self):
        config = self.load("[TBL:T]\n[LST:L]\n[DICT:D]\n")
        config.getTable("T").append("row;")
        config.getList("L").append("item.")
        config.getDictionary("D")["k"] = "value,"
        config.save("copy.ini")
        other = ConfigurationFile("copy.ini")
        self.assertEqual(["row;"], other.getTable("T"))
        self.assertEqual(["item."], other.getList("L"))
        self.assertEqual("value,", other.getDictionary("D")["k"])
        original = Path("copy.ini").read_bytes()
        config.getTable("T").append("[header]")
        with self.assertRaises(ValueError):
            config.save("copy.ini")
        self.assertEqual(original, Path("copy.ini").read_bytes())

    def test_automatic_save_and_rollback(self):
        source = "[S]\nk=original\n"
        Path("original.ini").write_text(source)
        config = ConfigurationFile("original.ini")
        config.autoUpdateRegistry = config.autoSaveRegistry = True
        config.setParameter("S", "k", "memory")
        self.assertEqual(source, Path("original.ini").read_text())
        config.addParameter("New", "key", "created")
        self.assertEqual("memory", ConfigurationFile("original.ini").getParameter("S", "k", ""))
        saved = Path("original.ini").read_bytes()
        with self.assertRaises(ValueError):
            config.addParameter("Bad", "key", "value ## comment")
        self.assertFalse(config.containsSection("Bad"))
        self.assertEqual(saved, Path("original.ini").read_bytes())
        with patch.object(config, "save", side_effect=OSError("save failed")):
            with self.assertRaises(OSError):
                config.addParameter("S", "bad", "value")
        self.assertFalse(config.containsParameter("S", "bad"))
        memory = ConfigurationFile()
        memory.autoUpdateRegistry = memory.autoSaveRegistry = True
        with self.assertRaises(ValueError):
            memory.getParameter("S", "k", 42)
        self.assertEqual([], memory.listSections())

    def test_streams_encodings_and_newlines(self):
        for newline in ("\n", "\r\n", "\r"):
            text = newline.join(("[S]", "k=café", "[TXT:T]", " raw ", "", "[END]"))
            for encoding in ("utf-8", "utf-8-sig", "utf-16", "utf-16-be", "utf-32"):
                data = text.encode(encoding)
                if encoding == "utf-16-be":
                    data = codecs.BOM_UTF16_BE + data
                stream = io.BytesIO(b"skip" + data)
                stream.seek(4)
                config = ConfigurationFile(stream)
                self.assertEqual("café", config.getParameter("S", "k", ""))
                self.assertEqual(" raw \n", config.getText("T"))
                self.assertFalse(stream.closed)
                self.assertEqual(len(b"skip" + data), stream.tell())
                with self.assertRaises(ValueError):
                    config.save()
        class SlowStream(io.BytesIO):
            def read(self, size=-1):
                return super().read(min(size, 1))
            def seek(self, *args):
                raise AssertionError("Must not seek")
        stream = SlowStream(codecs.BOM_UTF16_LE + "[S]\r\nk=v\r".encode("utf-16-le"))
        self.assertEqual("v", ConfigurationFile(stream).getParameter("S", "k", ""))
        self.assertFalse(stream.closed)

    def test_stream_failure_context(self):
        class Broken:
            def read(self, size):
                raise OSError("read failed")
        with self.assertRaises(ConfigurationError) as caught:
            ConfigurationFile(Broken())
        self.assertEqual("<stream>", caught.exception.FilePath)
        self.assertEqual(1, caught.exception.LineNumber)
        stream = io.StringIO("[ENC:B]\n!\n")
        with self.assertRaises(ConfigurationError) as caught:
            ConfigurationFile(stream)
        self.assertFalse(stream.closed)
        self.assertEqual(1, caught.exception.LineNumber)

    def test_includes(self):
        Path("child.ini").write_text("[S]\nk=value\n")
        for alias in ("INCLUDE", "INC", "INPUT", "LINK", "LOAD"):
            config = self.load(f"[{alias}:child.ini] ## note\n[END:child.ini]\n")
            self.assertEqual("value", config.getParameter("S", "k", ""))
        Path("sub").mkdir()
        Path("sub/parent.ini").write_text("[INC:child.ini]\n")
        self.assertTrue(ConfigurationFile("sub/parent.ini").containsSection("S"))
        with self.assertRaises(ConfigurationError):
            self.load("[INC:child.ini]\n[INC:child.ini]\n")
        Path("cycle.ini").write_text("[INC:cycle.ini]\n")
        with self.assertRaisesRegex(ConfigurationError, "cycle"):
            ConfigurationFile("cycle.ini")
        Path("child.ini").write_text("[S]\nk=1\nk=2\n")
        Path("parent.ini").write_text("[INC:child.ini]\n")
        with self.assertRaises(ConfigurationError) as caught:
            ConfigurationFile("parent.ini")
        self.assertEqual(str(Path("child.ini").absolute()), caught.exception.FilePath)
        self.assertEqual(3, caught.exception.LineNumber)
        self.assertIn("parent.ini", str(caught.exception))
        self.assertIn("child.ini", str(caught.exception))
        with self.assertRaises(ConfigurationError) as caught:
            ConfigurationFile("missing.ini")
        self.assertEqual(0, caught.exception.LineNumber)

    def test_include_depth(self):
        for index in range(129):
            Path(f"{index}.ini").write_text(f"[INC:{index + 1}.ini]\n" if index < 128 else "[S]\n")
        with self.assertRaisesRegex(ConfigurationError, "128"):
            ConfigurationFile("0.ini")

    def test_base64(self):
        config = self.load("[ENC:E]\nAAE\nC/4A=\n")
        self.assertEqual(bytes((0, 1, 2, 255, 128)), config.getEncoded("E"))
        self.assertEqual(b"AAEC/4A=", config.tryGetEncoded("E", True)[1])
        self.assertEqual((False, None), config.tryGetEncoded("missing", True))
        for value in ("!", "Zg=", "Zg===", "____", "é"):
            with self.assertRaises(ConfigurationError):
                self.load(f"[ENC:E]\n{value}\n")

    def test_custom_prefixes_internal_parser(self):
        config = ConfigurationFile()
        config.parComment[0] = "//"
        config._loadReader(io.StringIO("[S] // header\nk=value // note\n##key=literal\n"))
        self.assertEqual("value", config.getParameter("S", "k", ""))
        self.assertEqual("literal", config.getParameter("S", "##key", ""))
        with self.assertRaises(AttributeError):
            config.parComment = []
        for prefix in (None, ""):
            config = ConfigurationFile()
            config.parComment[0] = prefix
            config._loadReader(io.StringIO("[S]\nk=value##literal\n"))
            self.assertEqual("value##literal", config.getParameter("S", "k", ""))

    def test_examples(self):
        for example in sorted((ROOT / "examples").glob("*.ini")):
            with self.subTest(example=example.name):
                original = example.read_bytes()
                config = ConfigurationFile(example)
                config.save("example-copy.ini")
                self.assertTrue(config._sameData(ConfigurationFile("example-copy.ini")))
                self.assertEqual(original, example.read_bytes())


if __name__ == "__main__":
    unittest.main()
