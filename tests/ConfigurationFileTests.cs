using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ConfigurationFilesReader;

// Regression tests deliberately preserve the documented legacy behavior.
class ConfigurationFileTests
{
    static int passed, failed, assertions;

    static void Equal<T>(T expected, T actual)
    {
        assertions++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception("Expected <" + expected + ">, got <" + actual + ">");
    }

    static void Throws<T>(Action action) where T : Exception
    {
        assertions++;
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    static ConfigurationFile Load(string text)
    {
        File.WriteAllText("config.ini", text);
        return new ConfigurationFile("config.ini");
    }

    static string Get(ConfigurationFile cfg, string section, string key)
    { return cfg.getParameter(section, key, "fallback"); }

    static void Test(string name, Action action)
    {
        string original = Environment.CurrentDirectory;
        string folder = Path.Combine(original, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            Environment.CurrentDirectory = folder;
            action();
            passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception error)
        {
            failed++;
            Console.Error.WriteLine("FAIL " + name + ": " + error);
        }
        finally
        {
            Environment.CurrentDirectory = original;
            // The legacy parser can retain readers after a parsing exception.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Directory.Delete(folder, true);
        }
    }

    static void Defaults()
    {
        ConfigurationFile cfg = new ConfigurationFile();
        Equal(false, cfg.UpdateFile);
        Equal("=", new string(cfg.parSeparator));
        Equal(";,.", new string(cfg.parEndLineDelimiter));
        Equal(false, cfg.checkSection("missing"));
        cfg.SetParameter("S", "existing", "value");
        foreach (string section in new string[] { "missing", "S" })
        {
            Equal("default", cfg.getParameter(section, "absent", "default"));
            Equal<string>(null, cfg.getParameter(section, "absent", (string)null));
            Equal(true, cfg.getParameter(section, "absent", true));
            Equal(false, cfg.getParameter(section, "absent", false));
            Equal(1.25, cfg.getParameter(section, "absent", 1.25));
            Equal(1.25f, cfg.getParameter(section, "absent", 1.25f));
            Equal(123L, cfg.getParameter(section, "absent", 123L));
            Equal(123, cfg.getParameter(section, "absent", 123));
        }
        Equal(0, cfg.getTable("missing").Count);
        Equal(0, Directory.GetFiles(".").Length);
    }

    static void Parsing()
    {
        ConfigurationFile cfg = Load("ignored=before\r\n  ## comment\r\n\r\n [ S ] \r\n" +
            " key = value; \r\nempty=;\r\n=unnamed;\r\ninvalid\r\nmulti=a=b;\r\n" +
            "punctuation=value.,;\r\ndot=.;\r\nspace=value ;\r\nquoted=\"hello\";\r\n" +
            "inline=hello ## literal;\r\npath=./ricette/;\r\nunicode=caffè 日本;\r\n[Other]\r\nkey=last");
        Equal(true, cfg.checkSection("S"));
        Equal(false, cfg.checkSection("s"));
        Equal("value", Get(cfg, "S", "key"));
        Equal("fallback", Get(cfg, "S", "Key"));
        Equal("", Get(cfg, "S", "empty"));
        Equal("unnamed", Get(cfg, "S", ""));
        Equal("fallback", Get(cfg, "S", "invalid"));
        Equal("fallback", Get(cfg, "S", "multi"));
        Equal("fallback", Get(cfg, "S", "ignored"));
        Equal("value", Get(cfg, "S", "punctuation"));
        Equal("", Get(cfg, "S", "dot"));
        Equal("value ", Get(cfg, "S", "space"));
        Equal("\"hello\"", Get(cfg, "S", "quoted"));
        Equal("hello ## literal", Get(cfg, "S", "inline"));
        Equal("./ricette/", Get(cfg, "S", "path"));
        Equal("caffè 日本", Get(cfg, "S", "unicode"));
        Equal("last", Get(cfg, "Other", "key"));
    }

    static void Tables()
    {
        ConfigurationFile cfg = Load("[TABLE: ROWS ]\na;b;c;\n\n ## skip\nd;e.,;\n; comment\n" +
            "x=y;\n.;\n[S]\nk=v\n[TABLE:EMPTY]\n[ROWS]\nkey=section\n[table:lower]\nk=v\n");
        List<string> rows = cfg.getTable("ROWS");
        Equal(5, rows.Count);
        Equal("a;b;c", rows[0]);
        Equal("d;e", rows[1]);
        Equal("; comment", rows[2]);
        Equal("x=y", rows[3]);
        Equal("", rows[4]);
        Equal("v", Get(cfg, "S", "k"));
        Equal("section", Get(cfg, "ROWS", "key"));
        Equal(false, cfg.checkSection("EMPTY"));
        Equal(true, cfg.checkSection("table:lower"));
        Equal(0, cfg.getTable("lower").Count);
        Equal(0, cfg.getTable("rows").Count);
        rows.Add("live");
        Equal(true, Object.ReferenceEquals(rows, cfg.getTable("ROWS")));
        Equal("live", cfg.getTable("ROWS")[5]);
        rows.RemoveAt(0);
        Equal("d;e", cfg.getTable("ROWS")[0]);
        cfg.getTable("EMPTY").Add("also live");
        Equal(1, cfg.getTable("EMPTY").Count);
        cfg.getTable("missing").Add("detached");
        Equal(0, cfg.getTable("missing").Count);
    }

    static void Numbers()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new string[] { "en-US", "it-IT", "tr-TR" })
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                ConfigurationFile cfg = Load("[S]\nnumber=1,25;\nnegative=-12;\nexp=1.25e2;\n" +
                    "max=9223372036854775807;\nmin=-9223372036854775808;\n" +
                    "overflow=9223372036854775808;\nwrap=2147483648;\n" +
                    "intmax=2147483647;\nintmin=-2147483648;\nbad=abc;\nempty=;\n");
                Equal(1.25, cfg.getParameter("S", "number", 0.0));
                Equal(1.25f, cfg.getParameter("S", "number", 0f));
                Equal(125.0, cfg.getParameter("S", "exp", 0.0));
                Equal(-12L, cfg.getParameter("S", "negative", 0L));
                Equal(-12, cfg.getParameter("S", "negative", 0));
                Equal(long.MaxValue, cfg.getParameter("S", "max", 0L));
                Equal(long.MinValue, cfg.getParameter("S", "min", 0L));
                Equal(42L, cfg.getParameter("S", "overflow", 42L));
                Equal(42, cfg.getParameter("S", "overflow", 42));
                Equal(int.MinValue, cfg.getParameter("S", "wrap", 42));
                Equal(int.MaxValue, cfg.getParameter("S", "intmax", 0));
                Equal(int.MinValue, cfg.getParameter("S", "intmin", 0));
                Equal(42, cfg.getParameter("S", "number", 42));
                foreach (string key in new string[] { "bad", "empty", "missing" })
                {
                    Equal(4.25, cfg.getParameter("S", key, 4.25));
                    Equal(4.25f, cfg.getParameter("S", key, 4.25f));
                    Equal(42L, cfg.getParameter("S", key, 42L));
                    Equal(42, cfg.getParameter("S", key, 42));
                }
            }
        }
        finally { System.Threading.Thread.CurrentThread.CurrentCulture = original; }
    }

    static void Booleans()
    {
        ConfigurationFile cfg = new ConfigurationFile();
        foreach (string value in new string[] { "TRUE", "true", " TrUe \t" })
        {
            cfg.SetParameter("S", "b", value);
            Equal(true, cfg.getParameter("S", "b", false));
        }
        foreach (string value in new string[] { "FALSE", "false", "yes", "1", "0", "", "invalid" })
        {
            cfg.SetParameter("S", "b", value);
            Equal(false, cfg.getParameter("S", "b", true));
        }
    }

    static void Overrides()
    {
        const string source = "[S]\nkey=original;\n[TABLE:ROWS]\na;b;\n";
        ConfigurationFile cfg = Load(source);
        foreach (bool update in new bool[] { false, true })
        {
            cfg.UpdateFile = update;
            cfg.SetParameter("S", "key", "override");
            Equal("override", Get(cfg, "S", "key"));
            cfg.SetParameter("S", "key", " last=.,; ");
            Equal(" last=.,; ", Get(cfg, "S", "key"));
            cfg.SetParameter("NEW", "n", "123");
            Equal(true, cfg.checkSection("NEW"));
            Equal(123, cfg.getParameter("NEW", "n", 0));
            cfg.SetParameter("s", "Key", "distinct");
            Equal("distinct", Get(cfg, "s", "Key"));
            Equal("fallback", Get(cfg, "S", "Key"));
            cfg.SetParameter("ROWS", "key", "separate");
            Equal("a;b", cfg.getTable("ROWS")[0]);
            Equal("separate", Get(cfg, "ROWS", "key"));
            Equal(source, File.ReadAllText("config.ini"));
            Equal(1, Directory.GetFiles(".").Length);
            ConfigurationFile empty = new ConfigurationFile();
            empty.UpdateFile = update;
            empty.SetParameter("runtime", "key", "created");
            Equal("created", Get(empty, "runtime", "key"));
            Equal(false, File.Exists("runtime"));
        }
        Equal("original", Get(new ConfigurationFile("config.ini"), "S", "key"));
        cfg.SetParameter("S", "null", null);
        Equal<string>(null, Get(cfg, "S", "null"));
    }

    static void LegacyWrites()
    {
        const string source = "[S]\nk=original;\n";
        ConfigurationFile cfg = Load(source);
        foreach (bool update in new bool[] { false, true })
        {
            cfg.UpdateFile = update;
            cfg.addParameter("S", "k", "replacement");
            cfg.addParameter("S", "new", "value");
            cfg.addParameter("absent", "key", "value");
            Equal("original", Get(cfg, "S", "k"));
            Equal("fallback", Get(cfg, "S", "new"));
            Equal(false, File.Exists("absent"));
        }
        Equal(false, cfg.checkSection("side-effect"));
        string entry = Environment.NewLine + "[side-effect]" + Environment.NewLine;
        Equal(entry, File.ReadAllText("side-effect"));
        Equal("fallback", Get(cfg, "side-effect", "k"));
        Equal(entry + entry, File.ReadAllText("side-effect"));
        cfg.UpdateFile = false;
        Equal(false, cfg.checkSection("side-effect"));
        Equal(source, File.ReadAllText("config.ini"));
    }

    static int Main()
    {
        Test("empty container and all default overloads", Defaults);
        Test("sections, whitespace, delimiters, Unicode and literal values", Parsing);
        Test("tables, transitions, case sensitivity and mutable lists", Tables);
        Test("numeric conversions, boundaries and three cultures", Numbers);
        Test("boolean conversions", Booleans);
        Test("runtime overrides and file immutability", Overrides);
        Test("legacy addParameter and UpdateFile behavior", LegacyWrites);
        Test("empty file", delegate { Equal(false, Load("").checkSection("S")); });
        Test("comments only", delegate { Equal(0, Load(" ## comment\n\n").getTable("S").Count); });
        Test("missing file", delegate { Throws<Exception>(delegate { new ConfigurationFile("missing.ini"); }); });
        Test("duplicate key", delegate { Throws<ArgumentException>(delegate { Load("[S]\nk=1\n k =2\n"); }); });
        Test("duplicate section", delegate { Throws<ArgumentException>(delegate { Load("[S]\n[S]\n"); }); });
        Test("duplicate table", delegate { Throws<ArgumentException>(delegate { Load("[TABLE:T]\n[TABLE:T]\n"); }); });
        Test("case-distinct names", delegate {
            ConfigurationFile cfg = Load("[S]\nk=1\nK=2\n[s]\nk=3\n[TABLE:T]\na\n[TABLE:t]\nb\n");
            Equal("1", Get(cfg, "S", "k")); Equal("2", Get(cfg, "S", "K"));
            Equal("3", Get(cfg, "s", "k")); Equal("a", cfg.getTable("T")[0]); Equal("b", cfg.getTable("t")[0]);
        });
        Test("public delimiters do not reload parsed data", delegate {
            ConfigurationFile cfg = Load("[S]\nk=value;\n");
            cfg.parSeparator = new char[] { ':' }; cfg.parEndLineDelimiter = new char[] { 'e' };
            Equal("value", Get(cfg, "S", "k"));
        });
        Console.WriteLine("{0} passed, {1} failed; {2} assertions", passed, failed, assertions);
        return failed == 0 ? 0 : 1;
    }
}
