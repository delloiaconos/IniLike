using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ConfigurationFilesReader;

// Regression tests deliberately preserve the documented legacy behavior.
class ConfigurationFileTests
{
    static int passed, failed, assertions;

    static void equal<T>(T expected, T actual)
    {
        assertions++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception("Expected <" + expected + ">, got <" + actual + ">");
    }

    static void throws<T>(Action action) where T : Exception
    {
        assertions++;
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    static ConfigurationFile load(string text)
    {
        File.WriteAllText("config.ini", text);
        return new ConfigurationFile("config.ini");
    }

    static string get(ConfigurationFile cfg, string section, string key)
    { return cfg.getParameter(section, key, "fallback"); }

    static void test(string name, Action action)
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
            Directory.Delete(folder, true);
        }
    }

    static void pathConstructors()
    {
        DirectoryInfo BaseDirectory = Directory.CreateDirectory("config files");
        string FileName = "settings.ini";
        string FullPath = Path.Combine(BaseDirectory.FullName, FileName);
        const string Source = "[S]\nk=value;\n";
        File.WriteAllText(FullPath, Source);
        foreach (ConfigurationFile Config in new ConfigurationFile[] {
            new ConfigurationFile(Path.Combine("config files", FileName)),
            new ConfigurationFile(FullPath),
            new ConfigurationFile(new FileInfo(FullPath)),
            new ConfigurationFile(BaseDirectory, FileName) })
        {
            equal("value", get(Config, "S", "k"));
            Config.setParameter("S", "k", "memory only");
            equal(Source, File.ReadAllText(FullPath));
        }
        // DirectoryInfo keeps its resolved path when the working directory changes.
        string Original = Environment.CurrentDirectory;
        Directory.CreateDirectory("other");
        try
        {
            Environment.CurrentDirectory = Path.Combine(Original, "other");
            equal("value", get(new ConfigurationFile(BaseDirectory, FileName), "S", "k"));
        }
        finally { Environment.CurrentDirectory = Original; }
        using (FileStream File = System.IO.File.Open(FullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            equal(true, File.CanWrite);
        throws<ArgumentNullException>(delegate { new ConfigurationFile((FileInfo)null); });
        throws<ArgumentNullException>(delegate { new ConfigurationFile((string)null); });
        throws<ArgumentNullException>(delegate { new ConfigurationFile((DirectoryInfo)null, FileName); });
        throws<ArgumentNullException>(delegate { new ConfigurationFile(BaseDirectory, null); });
        throws<Exception>(delegate { new ConfigurationFile(new FileInfo("missing.ini")); });
        throws<Exception>(delegate { new ConfigurationFile(BaseDirectory, "missing.ini"); });
    }

    static void fileHandles()
    {
        load("[S]\nk=value\n");
        using (FileStream file = File.Open("config.ini", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            equal(true, file.CanWrite);

        foreach (string source in new string[] {
            "[S]\nk=1\nk=2\n", "[S]\n[S]\n", "[TABLE:T]\na\n[TABLE:T]\n" })
        {
            throws<ArgumentException>(delegate { load(source); });
            // Reopening exclusively must work immediately, without forcing GC.
            using (FileStream file = File.Open("config.ini", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                equal(true, file.CanWrite);
        }

    }

    static void defaults()
    {
        ConfigurationFile cfg = new ConfigurationFile();
        equal(false, cfg.autoUpdateFile);
        foreach (string name in new string[] { "parSeparator", "parEndLineDelimiter", "autoUpdateFile" })
        {
            System.Reflection.FieldInfo field = typeof(ConfigurationFile).GetField(name);
            equal(true, field != null && field.IsInitOnly && !field.IsStatic);
        }
        equal("=", new string(cfg.parSeparator));
        equal(";,.", new string(cfg.parEndLineDelimiter));
        equal(false, cfg.checkSection("missing"));
        cfg.setParameter("S", "existing", "value");
        foreach (string section in new string[] { "missing", "S" })
        {
            equal("default", cfg.getParameter(section, "absent", "default"));
            equal<string>(null, cfg.getParameter(section, "absent", (string)null));
            equal(true, cfg.getParameter(section, "absent", true));
            equal(false, cfg.getParameter(section, "absent", false));
            equal(1.25, cfg.getParameter(section, "absent", 1.25));
            equal(1.25f, cfg.getParameter(section, "absent", 1.25f));
            equal(123L, cfg.getParameter(section, "absent", 123L));
            equal(123, cfg.getParameter(section, "absent", 123));
        }
        equal(0, cfg.getTable("missing").Count);
        equal(0, Directory.GetFiles(".").Length);
    }

    static void parsing()
    {
        ConfigurationFile cfg = load("ignored=before\r\n  ## comment\r\n\r\n [ S ] \r\n" +
            " key = value; \r\nempty=;\r\n=unnamed;\r\ninvalid\r\nmulti=a=b;\r\n" +
            "punctuation=value.,;\r\ndot=.;\r\nspace=value ;\r\nquoted=\"hello\";\r\n" +
            "inline=hello ## literal;\r\npath=./ricette/;\r\nunicode=caffè 日本;\r\n[Other]\r\nkey=last");
        equal(true, cfg.checkSection("S"));
        equal(false, cfg.checkSection("s"));
        equal("value", get(cfg, "S", "key"));
        equal("fallback", get(cfg, "S", "Key"));
        equal("", get(cfg, "S", "empty"));
        equal("unnamed", get(cfg, "S", ""));
        equal("fallback", get(cfg, "S", "invalid"));
        equal("fallback", get(cfg, "S", "multi"));
        equal("fallback", get(cfg, "S", "ignored"));
        equal("value", get(cfg, "S", "punctuation"));
        equal("", get(cfg, "S", "dot"));
        equal("value ", get(cfg, "S", "space"));
        equal("\"hello\"", get(cfg, "S", "quoted"));
        equal("hello ## literal", get(cfg, "S", "inline"));
        equal("./ricette/", get(cfg, "S", "path"));
        equal("caffè 日本", get(cfg, "S", "unicode"));
        equal("last", get(cfg, "Other", "key"));
    }

    static void tables()
    {
        ConfigurationFile cfg = load("[TABLE: ROWS ]\na;b;c;\n\n ## skip\nd;e.,;\n; comment\n" +
            "x=y;\n.;\n[S]\nk=v\n[TABLE:EMPTY]\n[ROWS]\nkey=section\n[table:lower]\nk=v\n");
        List<string> rows = cfg.getTable("ROWS");
        equal(5, rows.Count);
        equal("a;b;c", rows[0]);
        equal("d;e", rows[1]);
        equal("; comment", rows[2]);
        equal("x=y", rows[3]);
        equal("", rows[4]);
        equal("v", get(cfg, "S", "k"));
        equal("section", get(cfg, "ROWS", "key"));
        equal(false, cfg.checkSection("EMPTY"));
        equal(true, cfg.checkSection("table:lower"));
        equal(0, cfg.getTable("lower").Count);
        equal(0, cfg.getTable("rows").Count);
        rows.Add("live");
        equal(true, Object.ReferenceEquals(rows, cfg.getTable("ROWS")));
        equal("live", cfg.getTable("ROWS")[5]);
        rows.RemoveAt(0);
        equal("d;e", cfg.getTable("ROWS")[0]);
        cfg.getTable("EMPTY").Add("also live");
        equal(1, cfg.getTable("EMPTY").Count);
        cfg.getTable("missing").Add("detached");
        equal(0, cfg.getTable("missing").Count);
    }

    static void numbers()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new string[] { "en-US", "it-IT", "tr-TR" })
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                ConfigurationFile cfg = load("[S]\nnumber=1,25;\nnegative=-12;\nexp=1.25e2;\n" +
                    "max=9223372036854775807;\nmin=-9223372036854775808;\n" +
                    "overflow=9223372036854775808;\nwrap=2147483648;\n" +
                    "intmax=2147483647;\nintmin=-2147483648;\nbad=abc;\nempty=;\n");
                equal(1.25, cfg.getParameter("S", "number", 0.0));
                equal(1.25f, cfg.getParameter("S", "number", 0f));
                equal(125.0, cfg.getParameter("S", "exp", 0.0));
                equal(-12L, cfg.getParameter("S", "negative", 0L));
                equal(-12, cfg.getParameter("S", "negative", 0));
                equal(long.MaxValue, cfg.getParameter("S", "max", 0L));
                equal(long.MinValue, cfg.getParameter("S", "min", 0L));
                equal(42L, cfg.getParameter("S", "overflow", 42L));
                equal(42, cfg.getParameter("S", "overflow", 42));
                equal(int.MinValue, cfg.getParameter("S", "wrap", 42));
                equal(int.MaxValue, cfg.getParameter("S", "intmax", 0));
                equal(int.MinValue, cfg.getParameter("S", "intmin", 0));
                equal(42, cfg.getParameter("S", "number", 42));
                foreach (string key in new string[] { "bad", "empty", "missing" })
                {
                    equal(4.25, cfg.getParameter("S", key, 4.25));
                    equal(4.25f, cfg.getParameter("S", key, 4.25f));
                    equal(42L, cfg.getParameter("S", key, 42L));
                    equal(42, cfg.getParameter("S", key, 42));
                }
            }
        }
        finally { System.Threading.Thread.CurrentThread.CurrentCulture = original; }
    }

    static void booleans()
    {
        ConfigurationFile cfg = new ConfigurationFile();
        foreach (string value in new string[] { "TRUE", "true", " TrUe \t" })
        {
            cfg.setParameter("S", "b", value);
            equal(true, cfg.getParameter("S", "b", false));
        }
        foreach (string value in new string[] { "FALSE", "false", "yes", "1", "0", "", "invalid" })
        {
            cfg.setParameter("S", "b", value);
            equal(false, cfg.getParameter("S", "b", true));
        }
    }

    static void overrides()
    {
        const string source = "[S]\nkey=original;\n[TABLE:ROWS]\na;b;\n";
        ConfigurationFile cfg = load(source);

        cfg.setParameter("S", "key", "override");
        equal("override", get(cfg, "S", "key"));
        cfg.setParameter("S", "key", " last=.,; ");
        equal(" last=.,; ", get(cfg, "S", "key"));
        cfg.setParameter("NEW", "n", "123");
        equal(true, cfg.checkSection("NEW"));
        equal(123, cfg.getParameter("NEW", "n", 0));
        cfg.setParameter("s", "Key", "distinct");
        equal("distinct", get(cfg, "s", "Key"));
        equal("fallback", get(cfg, "S", "Key"));
        cfg.setParameter("ROWS", "key", "separate");
        equal("a;b", cfg.getTable("ROWS")[0]);
        equal("separate", get(cfg, "ROWS", "key"));
        equal(source, File.ReadAllText("config.ini"));
        equal(1, Directory.GetFiles(".").Length);
        ConfigurationFile empty = new ConfigurationFile();

        empty.setParameter("runtime", "key", "created");
        equal("created", get(empty, "runtime", "key"));
        equal(false, File.Exists("runtime"));
        equal("original", get(new ConfigurationFile("config.ini"), "S", "key"));
        cfg.setParameter("S", "null", null);
        equal<string>(null, get(cfg, "S", "null"));
    }

    static void memoryDefaults()
    {
        const string source = "[S]\nk=original;\n[TABLE:ROWS]\na;b;\n";
        ConfigurationFile cfg = load(source);

        cfg.addParameter("S", "k", "replacement");
        cfg.addParameter("S", "new", " value=.,; ");
        cfg.addParameter("S", "new", "replacement");
        cfg.addParameter("NEW", "key", "123");
        equal("original", get(cfg, "S", "k"));
        equal(" value=.,; ", get(cfg, "S", "new"));
        equal(true, cfg.checkSection("NEW"));
        equal(123, cfg.getParameter("NEW", "key", 0));
        cfg.addParameter("s", "k", "distinct section");
        cfg.addParameter("S", "K", "distinct key");
        equal("distinct section", get(cfg, "s", "k"));
        equal("distinct key", get(cfg, "S", "K"));
        cfg.addParameter("ROWS", "key", "separate");
        equal("a;b", cfg.getTable("ROWS")[0]);
        equal("separate", get(cfg, "ROWS", "key"));
        cfg.addParameter("S", "null", null);
        cfg.addParameter("S", "null", "replacement");
        equal<string>(null, get(cfg, "S", "null"));
        cfg.addParameter("", "", "empty names");
        equal("empty names", get(cfg, "", ""));
        throws<ArgumentNullException>(delegate { cfg.addParameter(null, "key", "value"); });
        throws<ArgumentNullException>(delegate { cfg.addParameter("invalid", null, "value"); });
        equal(false, cfg.checkSection("invalid"));
        cfg.setParameter("S", "new", "explicit override");
        equal("explicit override", get(cfg, "S", "new"));

        ConfigurationFile empty = new ConfigurationFile();

        empty.addParameter("runtime", "key", "value");
        equal("value", get(empty, "runtime", "key"));
        for (int i = 0; i < 2; i++)
        {
            equal(false, empty.checkSection("missing"));
            equal(false, empty.checkSection("config.ini"));
        }
        equal(source, File.ReadAllText("config.ini"));
        equal(1, Directory.GetFiles(".").Length);
        equal("fallback", get(new ConfigurationFile("config.ini"), "S", "new"));
    }

    static void getterDefaults()
    {
        const string source = "[S]\nk=original;\n";
        ConfigurationFile cfg = load(source);

        equal(false, cfg.checkSection("missing"));
        equal("first", cfg.getParameter("missing", "key", "first"));
        equal(false, cfg.checkSection("missing"));
        equal("second", cfg.getParameter("missing", "key", "second"));
        equal("first", cfg.getParameter("S", "new", "first"));
        equal("second", cfg.getParameter("S", "new", "second"));
        equal(42, cfg.getParameter("numbers", "int", 42));
        equal(7, cfg.getParameter("numbers", "int", 7));
        equal(true, cfg.getParameter("numbers", "bool", true));
        equal(false, cfg.getParameter("numbers", "bool", false));
        equal("fallback", get(cfg, "config.ini", "key"));
        equal(source, File.ReadAllText("config.ini"));
        equal(1, Directory.GetFiles(".").Length);
    }

    static int Main()
    {
        test("string and typed path constructors", pathConstructors);
        test("file handles released after loading and parsing errors", fileHandles);
        test("empty container and all default overloads", defaults);
        test("sections, whitespace, delimiters, Unicode and literal values", parsing);
        test("tables, transitions, case sensitivity and mutable lists", tables);
        test("numeric conversions, boundaries and three cultures", numbers);
        test("boolean conversions", booleans);
        test("runtime overrides and file immutability", overrides);
        test("addParameter stores defaults only in memory", memoryDefaults);
        test("getters return defaults without storing missing parameters", getterDefaults);
        test("empty file", delegate { equal(false, load("").checkSection("S")); });
        test("comments only", delegate { equal(0, load(" ## comment\n\n").getTable("S").Count); });
        test("missing file", delegate { throws<Exception>(delegate { new ConfigurationFile("missing.ini"); }); });
        test("duplicate key", delegate { throws<ArgumentException>(delegate { load("[S]\nk=1\n k =2\n"); }); });
        test("duplicate section", delegate { throws<ArgumentException>(delegate { load("[S]\n[S]\n"); }); });
        test("duplicate table", delegate { throws<ArgumentException>(delegate { load("[TABLE:T]\n[TABLE:T]\n"); }); });
        test("case-distinct names", delegate {
            ConfigurationFile cfg = load("[S]\nk=1\nK=2\n[s]\nk=3\n[TABLE:T]\na\n[TABLE:t]\nb\n");
            equal("1", get(cfg, "S", "k")); equal("2", get(cfg, "S", "K"));
            equal("3", get(cfg, "s", "k")); equal("a", cfg.getTable("T")[0]); equal("b", cfg.getTable("t")[0]);
        });
        test("readonly array fields expose mutable elements without reloading", delegate {
            ConfigurationFile cfg = load("[S]\nk=value;\n");
            char[] separators = cfg.parSeparator;
            char[] delimiters = cfg.parEndLineDelimiter;
            separators[0] = ':'; delimiters[0] = 'e';
            equal(":", new string(cfg.parSeparator));
            equal("e,.", new string(cfg.parEndLineDelimiter));
            equal("value", get(cfg, "S", "k"));
        });
        Console.WriteLine("{0} passed, {1} failed; {2} assertions", passed, failed, assertions);
        return failed == 0 ? 0 : 1;
    }
}
