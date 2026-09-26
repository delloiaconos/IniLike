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

    static ConfigurationFile loadForUpdates(string Text)
    {
        File.WriteAllText("config.ini", Text);
        return new ConfigurationFile("config.ini") { autoUpdateRegistry = true };
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
            equal(false, Config.autoUpdateRegistry);
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
        equal(false, cfg.autoUpdateRegistry);
        equal(false, cfg.autoSaveRegistry);
        foreach (string name in new string[] { "parSeparator", "parEndLineDelimiter" })
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
        ConfigurationFile cfg = new ConfigurationFile { autoUpdateRegistry = true };
        foreach (string value in new string[] { "TRUE", "true", " TrUe \t", "1", " 1\t" })
        {
            cfg.setParameter("S", "b", value);
            equal(true, cfg.getParameter("S", "b", false));
        }
        foreach (string value in new string[] { "FALSE", "false", "yes", "0", " 0\t", "2", "-1", "", "invalid" })
        {
            cfg.setParameter("S", "b", value);
            equal(false, cfg.getParameter("S", "b", true));
        }
        ConfigurationFile parsed = load("[S]\non=1;\noff=0;\n");
        equal(true, parsed.getParameter("S", "on", false));
        equal(false, parsed.getParameter("S", "off", true));
    }

    static void overrides()
    {
        const string source = "[S]\nkey=original;\n[TABLE:ROWS]\na;b;\n";
        ConfigurationFile cfg = loadForUpdates(source);

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
        ConfigurationFile empty = new ConfigurationFile { autoUpdateRegistry = true };

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
        ConfigurationFile cfg = loadForUpdates(source);

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

        ConfigurationFile empty = new ConfigurationFile { autoUpdateRegistry = true };

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

    static void listNames()
    {
        ConfigurationFile Empty = new ConfigurationFile { autoUpdateRegistry = true };
        equal(0, Empty.listSections().Count);
        equal(0, Empty.listParameters().Count);
        equal(0, Empty.listParameters("missing").Count);
        equal(0, Empty.listTables().Count);
        equal(0, Directory.GetFiles(".").Length);

        const string Source = "[b]\nshared=1\nz=2\n[A]\nshared=3\nZ=4\n=empty key\n[EMPTY]\n[TABLE:b]\nrow\n[TABLE:A]\n";
        ConfigurationFile Config = loadForUpdates(Source);
        equal("A|EMPTY|b", String.Join("|", Config.listSections().ToArray()));
        equal("A|b", String.Join("|", Config.listTables().ToArray()));
        equal("|Z|shared", String.Join("|", Config.listParameters("A").ToArray()));
        equal("shared|z", String.Join("|", Config.listParameters("b").ToArray()));
        equal("|Z|shared|z", String.Join("|", Config.listParameters().ToArray()));
        equal(0, Config.listParameters("EMPTY").Count);
        equal(0, Config.listParameters("a").Count);
        equal(false, Config.checkSection("a"));
        throws<ArgumentNullException>(delegate { Config.listParameters(null); });

        List<string> Sections = Config.listSections();
        List<string> Parameters = Config.listParameters("A");
        List<string> AllParameters = Config.listParameters();
        Config.listSections().Clear();
        Config.listParameters("A").Clear();
        Config.listParameters().Clear();
        Config.listTables().Clear();
        equal(3, Config.listSections().Count);
        equal(3, Config.listParameters("A").Count);
        equal(4, Config.listParameters().Count);
        equal(2, Config.listTables().Count);
        Config.addParameter("NEW", "added", "value");
        Config.setParameter("A", "new", "value");
        equal(3, Sections.Count);
        equal(3, Parameters.Count);
        equal(4, AllParameters.Count);
        equal("A|EMPTY|NEW|b", String.Join("|", Config.listSections().ToArray()));
        equal("|Z|new|shared", String.Join("|", Config.listParameters("A").ToArray()));
        equal("|Z|added|new|shared|z", String.Join("|", Config.listParameters().ToArray()));
        equal(Source, File.ReadAllText("config.ini"));
        equal(1, Directory.GetFiles(".").Length);
    }

    static void saving()
    {
        const string Source = "## original comment\n[S]\nk=old;\nempty=;\nspace=value ;\n[EMPTY]\n[TABLE:S]\na;b;\n.;\n[TABLE:EMPTY]\n";
        ConfigurationFile Config = loadForUpdates(Source);
        Config.setParameter("S", "k", "caffè 日本");
        Config.addParameter("NEW", "number", "12");
        Config.getTable("S").Add("x=y");
        Config.save("copy.ini");
        equal(Source, File.ReadAllText("config.ini"));
        Config.save(new FileInfo("typed.ini"));
        Directory.CreateDirectory("output files");
        Config.save(new FileInfo("output files"), "combined.ini");
        string Serialized = File.ReadAllText("copy.ini");
        equal(Serialized, File.ReadAllText("typed.ini"));
        equal(Serialized, File.ReadAllText(Path.Combine("output files", "combined.ini")));
        ConfigurationFile Reloaded = new ConfigurationFile("copy.ini");
        equal("caffè 日本", get(Reloaded, "S", "k"));
        equal("", get(Reloaded, "S", "empty"));
        equal("value ", get(Reloaded, "S", "space"));
        equal(12, Reloaded.getParameter("NEW", "number", 0));
        equal(true, Reloaded.checkSection("EMPTY"));
        equal(0, Reloaded.getTable("EMPTY").Count);
        equal("a;b||x=y", String.Join("|", Reloaded.getTable("S").ToArray()));
        string OriginalDirectory = Environment.CurrentDirectory;
        try {
            Environment.CurrentDirectory = Path.Combine(OriginalDirectory, "output files");
            Config.save();
        } finally { Environment.CurrentDirectory = OriginalDirectory; }
        equal(Serialized, File.ReadAllText("config.ini"));
        Config.setParameter("S", "k", "updated");
        Config.save();
        equal("updated", get(new ConfigurationFile("config.ini"), "S", "k"));
        equal(Serialized, File.ReadAllText("copy.ini"));
        using (FileStream Stream = File.Open("config.ini", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            equal(true, Stream.CanWrite);

        ConfigurationFile Empty = new ConfigurationFile { autoUpdateRegistry = true };
        throws<InvalidOperationException>(delegate { Empty.save(); });
        Empty.save("empty.ini");
        equal(0L, new FileInfo("empty.ini").Length);
        throws<InvalidOperationException>(delegate { Empty.save(); });
        Empty.setParameter("", "", "value");
        Empty.save("memory.ini");
        equal("value", get(new ConfigurationFile("memory.ini"), "", ""));
        equal(0, Directory.GetFiles(".", ".inilike-*.tmp").Length);
    }

    static void saveFailures()
    {
        ConfigurationFile Config = new ConfigurationFile { autoUpdateRegistry = true };
        throws<ArgumentNullException>(delegate { Config.save((string)null); });
        throws<ArgumentNullException>(delegate { Config.save((FileInfo)null); });
        throws<ArgumentNullException>(delegate { Config.save(null, "file.ini"); });
        throws<ArgumentNullException>(delegate { Config.save(new FileInfo("."), null); });
        const string Original = "keep this file unchanged";
        File.WriteAllText("destination.ini", Original);
        foreach (string Value in new string[] { null, "a=b", "line\nbreak", " leading", "trailing.", "trailing;" }) {
            Config.setParameter("S", "k", Value);
            throws<InvalidOperationException>(delegate { Config.save("destination.ini"); });
            equal(Original, File.ReadAllText("destination.ini"));
            equal(0, Directory.GetFiles(".", ".inilike-*.tmp").Length);
        }
        Config.setParameter("S", "k", "valid");
        Config.setParameter("TABLE:reserved", "k", "value");
        throws<InvalidOperationException>(delegate { Config.save("new.ini"); });
        equal(false, File.Exists("new.ini"));
        ConfigurationFile Table = loadForUpdates("[TABLE:T]\nrow\n");
        Table.getTable("T").Add("## ignored");
        throws<InvalidOperationException>(delegate { Table.save("destination.ini"); });
        equal(Original, File.ReadAllText("destination.ini"));
        Config = new ConfigurationFile { autoUpdateRegistry = true };
        throws<DirectoryNotFoundException>(delegate { Config.save(Path.Combine("missing", "file.ini")); });
        Directory.CreateDirectory("directory.ini");
        throws<IOException>(delegate { Config.save("directory.ini"); });
        equal(true, Directory.Exists("directory.ini"));
        equal(0, Directory.GetFiles(".", ".inilike-*.tmp").Length);
    }

    static void automaticRegistryUpdates()
    {
        const string Source = "[S]\nexisting=original\n[TABLE:ROWS]\na;b\n";
        File.WriteAllText("config.ini", Source);
        foreach (bool Enabled in new bool[] { false, true }) {
            foreach (ConfigurationFile Config in new ConfigurationFile[] {
                new ConfigurationFile("config.ini") { autoUpdateRegistry = Enabled },
                new ConfigurationFile(new FileInfo("config.ini")) { autoUpdateRegistry = Enabled },
                new ConfigurationFile(new DirectoryInfo("."), "config.ini") { autoUpdateRegistry = Enabled } }) {
                equal(Enabled, Config.autoUpdateRegistry);
                Config.setParameter("S", "existing", "updated");
                Config.addParameter("S", "existing", "ignored");
                equal("updated", get(Config, "S", "existing"));
                Config.setParameter("S", "set", "value");
                Config.addParameter("S", "add", "value");
                equal(Enabled, Config.listParameters("S").Contains("set"));
                equal(Enabled, Config.listParameters("S").Contains("add"));
                Config.setParameter("SET", "key", "value");
                Config.addParameter("ADD", "key", "value");
                equal(Enabled, Config.checkSection("SET"));
                equal(Enabled, Config.checkSection("ADD"));
                equal("first", Config.getParameter("GET", "key", "first"));
                equal(Enabled, Config.checkSection("GET"));
                equal(Enabled ? "first" : "second", Config.getParameter("GET", "key", "second"));
                equal("first", Config.getParameter("S", "get", "first"));
                equal(Enabled ? "first" : "second", Config.getParameter("S", "get", "second"));
                equal(true, Config.getParameter("TYPED", "bool", true));
                equal(1.25, Config.getParameter("TYPED", "double", 1.25));
                equal(2.5f, Config.getParameter("TYPED", "float", 2.5f));
                equal(42L, Config.getParameter("TYPED", "long", 42L));
                equal(7, Config.getParameter("TYPED", "int", 7));
                equal(Enabled ? 5 : 0, Config.listParameters("TYPED").Count);
                equal(Enabled ? 7 : 8, Config.getParameter("TYPED", "int", 8));
                equal(false, Config.checkSection("CHECK"));
                equal(0, Config.listParameters("CHECK").Count);
                equal(0, Config.getTable("MISSING").Count);
                equal(1, Config.listTables().Count);
                throws<ArgumentNullException>(delegate { Config.setParameter("INVALID", null, "value"); });
                equal(false, Config.checkSection("INVALID"));
                equal(Source, File.ReadAllText("config.ini"));
                equal(1, Directory.GetFiles(".").Length);
            }
            ConfigurationFile Empty = new ConfigurationFile { autoUpdateRegistry = Enabled };
            Empty.addParameter("S", "k", "value");
            equal(Enabled, Empty.checkSection("S"));
            equal(Enabled, Empty.autoUpdateRegistry);
        }
    }

    static void toggleRegistryUpdates()
    {
        const string Source = "[S]\nexisting=original\n";
        ConfigurationFile Config = load(Source);
        equal(false, Config.autoUpdateRegistry);
        Config.setParameter("SET", "key", "ignored");
        equal(false, Config.checkSection("SET"));

        Config.autoUpdateRegistry = true;
        equal(true, Config.autoUpdateRegistry);
        Config.setParameter("SET", "key", "created");
        Config.addParameter("ADD", "key", "created");
        equal(42, Config.getParameter("GET", "key", 42));
        equal(true, Config.checkSection("SET"));
        equal(true, Config.checkSection("ADD"));
        equal(true, Config.checkSection("GET"));

        Config.autoUpdateRegistry = false;
        equal(false, Config.autoUpdateRegistry);
        Config.setParameter("SET", "key", "updated");
        Config.addParameter("ADD", "key", "ignored");
        equal("updated", get(Config, "SET", "key"));
        equal("created", get(Config, "ADD", "key"));
        equal(42, Config.getParameter("GET", "key", 0));
        Config.setParameter("SET", "missing", "ignored");
        Config.addParameter("NEW", "key", "ignored");
        equal("fallback", get(Config, "MISSING", "key"));
        equal(false, Config.listParameters("SET").Contains("missing"));
        equal(false, Config.checkSection("NEW"));
        equal(false, Config.checkSection("MISSING"));

        Config.autoUpdateRegistry = true;
        Config.addParameter("NEW", "key", "created");
        equal(true, Config.checkSection("NEW"));
        equal(Source, File.ReadAllText("config.ini"));
        equal(1, Directory.GetFiles(".").Length);
    }

    static void registryDump()
    {
        ConfigurationFile Config = loadForUpdates("[Z]\nb=2\na=caffè 日本\n[A]\n[TABLE:T]\nfirst;row\nsecond\n");
        Config.addParameter("Z", "c", "3");
        string Expected = String.Join(Environment.NewLine, new string[] {
            "[A]", "[Z]", "a=caffè 日本;", "b=2;", "c=3;",
            "[TABLE:T]", "first;row;", "second;", "" });
        Stream OwnedStream;
        using (StreamWriter Writer = Config.dump()) {
            OwnedStream = Writer.BaseStream;
            equal(0L, OwnedStream.Position);
            equal(true, OwnedStream.CanRead);
            byte[] Bytes = ((MemoryStream)OwnedStream).ToArray();
            equal(Expected, System.Text.Encoding.UTF8.GetString(Bytes));
            equal((byte)'[', Bytes[0]);
        }
        equal(false, OwnedStream.CanRead);
        equal(1, Directory.GetFiles(".").Length);
        Config.save("dump.ini");
        equal(Expected, File.ReadAllText("dump.ini"));
        using (MemoryStream Stream = new MemoryStream())
        using (StreamWriter Writer = new StreamWriter(Stream, new System.Text.UTF8Encoding(false))) {
            Writer.Write("prefix");
            Config.dump(Writer);
            equal("prefix" + Expected, System.Text.Encoding.UTF8.GetString(Stream.ToArray()));
            Writer.Write("suffix");
            Writer.Flush();
            equal("prefix" + Expected + "suffix", System.Text.Encoding.UTF8.GetString(Stream.ToArray()));
        }
        using (StreamWriter Writer = new ConfigurationFile().dump()) {
            equal(0L, Writer.BaseStream.Length);
            equal(0L, Writer.BaseStream.Position);
        }
        throws<ArgumentNullException>(delegate { Config.dump(null); });
    }

    static void automaticSaving()
    {
        const string Source = "## original\n[S]\nexisting=original\n";
        foreach (bool Update in new bool[] { false, true }) {
            foreach (bool Save in new bool[] { false, true }) {
                ConfigurationFile Config = load(Source);
                Config.autoUpdateRegistry = Update;
                Config.autoSaveRegistry = Save;
                equal(Save, Config.autoSaveRegistry);
                Config.addParameter("S", "existing", "ignored");
                Config.setParameter("S", "existing", "memory change");
                equal(Source, File.ReadAllText("config.ini"));
                Config.addParameter("S", "added", "value");
                Config.setParameter("NEW", "set", "value");
                equal(42, Config.getParameter("GET", "number", 42));
                ConfigurationFile Disk = new ConfigurationFile("config.ini");
                equal(Update && Save, Disk.listParameters("S").Contains("added"));
                equal(Update && Save, Disk.checkSection("NEW"));
                equal(Update && Save, Disk.checkSection("GET"));
                if (Update && Save) {
                    equal("memory change", get(Disk, "S", "existing"));
                    equal(42, Disk.getParameter("GET", "number", 0));
                } else {
                    equal(Source, File.ReadAllText("config.ini"));
                }
            }
        }
        ConfigurationFile Current = loadForUpdates(Source);
        Current.save("copy.ini");
        string Copy = File.ReadAllText("copy.ini");
        Current.autoSaveRegistry = true;
        Current.addParameter("S", "first", "value");
        equal("value", get(new ConfigurationFile("config.ini"), "S", "first"));
        equal(Copy, File.ReadAllText("copy.ini"));
        string Saved = File.ReadAllText("config.ini");
        Current.autoSaveRegistry = false;
        Current.addParameter("S", "second", "value");
        equal(Saved, File.ReadAllText("config.ini"));
        equal(true, Current.listParameters("S").Contains("second"));
    }

    static void automaticSaveFailures()
    {
        ConfigurationFile Empty = new ConfigurationFile {
            autoUpdateRegistry = true, autoSaveRegistry = true };
        throws<InvalidOperationException>(delegate { Empty.addParameter("ADD", "key", "value"); });
        throws<InvalidOperationException>(delegate { Empty.setParameter("SET", "key", "value"); });
        throws<InvalidOperationException>(delegate { Empty.getParameter("GET", "key", "value"); });
        equal(0, Empty.listSections().Count);
        equal(0, Directory.GetFiles(".").Length);

        const string Source = "[S]\nk=value\n";
        ConfigurationFile Config = loadForUpdates(Source);
        Config.autoSaveRegistry = true;
        throws<InvalidOperationException>(delegate { Config.addParameter("NEW", "key", "a=b"); });
        equal(false, Config.checkSection("NEW"));
        throws<InvalidOperationException>(delegate { Config.setParameter("S", "bad", "a=b"); });
        equal(false, Config.listParameters("S").Contains("bad"));
        equal(Source, File.ReadAllText("config.ini"));
        File.Delete("config.ini");
        Directory.CreateDirectory("config.ini");
        throws<IOException>(delegate { Config.getParameter("GET", "key", "value"); });
        equal(false, Config.checkSection("GET"));
        equal(true, Directory.Exists("config.ini"));
        equal(0, Directory.GetFiles(".", ".inilike-*.tmp").Length);
    }

    static void configuredComments()
    {
        foreach (string Prefix in new string[] { "//", "[SKIP", null, "" }) {
            ConfigurationFile Config = load("");
            Config.parComment[0] = Prefix;
            string Text = "[S]\nkey=value // literal\n##key=kept\n[TABLE:T]\n## kept\nrow // literal\n";
            if (!String.IsNullOrEmpty(Prefix)) {
                Text = "  " + Prefix + " before]\n[S]\n" + Prefix + " key=ignored\nkey=value // literal\n##key=kept\n[TABLE:T]\n  " + Prefix + " row]\n## kept\nrow // literal\n";
            }
            File.WriteAllText("config.ini", Text);
            // Loading is constructor-only publicly; exercise the parser with a changed prefix.
            typeof(ConfigurationFile).GetMethod("loadFile",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Config, null);
            equal("S", String.Join("|", Config.listSections().ToArray()));
            equal("value // literal", get(Config, "S", "key"));
            equal("kept", get(Config, "S", "##key"));
            equal("## kept|row // literal", String.Join("|", Config.getTable("T").ToArray()));
            equal(Text, File.ReadAllText("config.ini"));
        }
    }

    static void tableBlocks()
    {
        string[] Rows = {
            "WARMUP\t\t;WARMUP_COMMANDS\t;6  ;conversione_warm.py\t;\t\t;FALSE",
            "STOPPING\t;STOPPING_COMMANDS\t;1  ;conversione_warm.py\t; \t\t;FALSE",
            "STEPTEST \t;STEPTEST_COMMANDS\t;1  ;conversione_step.py\t;POSTTEST       ;TRUE"
        };
        foreach (string Keyword in new string[] { "TABLE", "TBL" }) {
            foreach (string End in new string[] { "", "[END]", "[END:TEST_PHASES]", " [END: TEST_PHASES ] " }) {
                string Text = "[" + Keyword + ":TEST_PHASES]\n" + String.Join("\n", Rows) + "\n";
                if (End.Length > 0) Text += End + "\nignored;row\nignored=value\n";
                ConfigurationFile Config = load(Text);
                equal(String.Join("\n", Rows), String.Join("\n", Config.getTable("TEST_PHASES").ToArray()));
                equal(0, Config.listSections().Count);
                equal("TEST_PHASES", String.Join("|", Config.listTables().ToArray()));
                Config.save("roundtrip.ini");
                equal(String.Join("\n", Rows), String.Join("\n", new ConfigurationFile("roundtrip.ini").getTable("TEST_PHASES").ToArray()));
            }
        }
        ConfigurationFile Transitions = load("[TBL:A]\nrow\n[S]\nk=v\n[TABLE:B]\n[END:B]\n[TBL:C]\nlast\n[END]\n[S2]\nk=v2\n");
        equal("row", Transitions.getTable("A")[0]);
        equal("v", get(Transitions, "S", "k"));
        equal(0, Transitions.getTable("B").Count);
        equal("last", Transitions.getTable("C")[0]);
        equal("v2", get(Transitions, "S2", "k"));
        throws<ArgumentException>(delegate { load("[TABLE:A]\n[END]\n[TBL:A]\n"); });
        throws<FormatException>(delegate { load("[TBL:A]\n[END:a]\n"); });
        using (FileStream Stream = File.Open("config.ini", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            equal(true, Stream.CanWrite);
    }

    static void sectionBlocks()
    {
        foreach (string Header in new string[] { "[SETTINGS]", "[SEC: SETTINGS ]", "[SECTION:SETTINGS]" }) {
            foreach (string End in new string[] { "", "[END]", "[END:SETTINGS]", " [END: SETTINGS ] " }) {
                string Text = Header + "\nkey=value\nflag=1\n";
                if (End.Length > 0) Text += End + "\noutside=ignored\n";
                ConfigurationFile Config = load(Text);
                equal("SETTINGS", String.Join("|", Config.listSections().ToArray()));
                equal("value", get(Config, "SETTINGS", "key"));
                equal(true, Config.getParameter("SETTINGS", "flag", false));
                equal("fallback", get(Config, "SETTINGS", "outside"));
                equal(0, Config.listTables().Count);
                Config.save("roundtrip.ini");
                ConfigurationFile Reloaded = new ConfigurationFile("roundtrip.ini");
                equal("value", get(Reloaded, "SETTINGS", "key"));
                equal("SETTINGS", String.Join("|", Reloaded.listSections().ToArray()));
            }
        }
        ConfigurationFile Transitions = load("[SEC:A]\nk=a\n[SECTION:B]\nk=b\n[END:B]\nignored=x\n[TBL:A]\nrow\n[END:A]\n[SECTION:EMPTY]\n[END]\n[C]\nk=c\n[END:C]\n[sec:literal]\nk=literal\n");
        equal("a", get(Transitions, "A", "k"));
        equal("b", get(Transitions, "B", "k"));
        equal("fallback", get(Transitions, "B", "ignored"));
        equal("row", Transitions.getTable("A")[0]);
        equal(true, Transitions.checkSection("EMPTY"));
        equal(0, Transitions.listParameters("EMPTY").Count);
        equal("c", get(Transitions, "C", "k"));
        equal("literal", get(Transitions, "sec:literal", "k"));
        throws<ArgumentException>(delegate { load("[A]\n[END]\n[SEC:A]\n"); });
        throws<ArgumentException>(delegate { load("[SEC:A]\n[SECTION:A]\n"); });
        throws<FormatException>(delegate { load("[SECTION:A]\n[END:a]\n"); });
        using (FileStream Stream = File.Open("config.ini", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            equal(true, Stream.CanWrite);
    }

    static void listBlocks()
    {
        string[] Bodies = {
            "mach1; \npil1;\nacq2;\n",
            "x1=mach1.volt1\nx2=mach1.current1\nx3=acq2.sensor1\nx4=acq2.sensor3\nx5=pil1.duty\n"
        };
        string[] Expected = {
            "mach1|pil1|acq2",
            "x1=mach1.volt1|x2=mach1.current1|x3=acq2.sensor1|x4=acq2.sensor3|x5=pil1.duty"
        };
        foreach (string Keyword in new string[] { "LIST", "LST" }) {
            foreach (string End in new string[] { "", "[END]", "[END: ITEMS ]" }) {
                for (int Index = 0; Index < Bodies.Length; Index++) {
                    string Text = "[" + Keyword + ": ITEMS ]\n" + Bodies[Index];
                    if (End.Length > 0) Text += End + "\noutside=ignored\n";
                    ConfigurationFile Config = load(Text);
                    equal(Expected[Index], String.Join("|", Config.getList("ITEMS").ToArray()));
                    equal("ITEMS", String.Join("|", Config.listLists().ToArray()));
                    equal(0, Config.listSections().Count);
                    equal(0, Config.listParameters().Count);
                    equal(0, Config.listTables().Count);
                    Config.save("lists.ini");
                    equal(Expected[Index], String.Join("|", new ConfigurationFile("lists.ini").getList("ITEMS").ToArray()));
                    equal(true, File.ReadAllText("lists.ini").StartsWith("[LIST:ITEMS]"));
                }
            }
        }
        ConfigurationFile Mixed = load("[LST:SHARED]\n## ignored\n\nsame;\nsame;\nx=a=b;\n.;\n[SEC:SHARED]\nk=v\n[TBL:SHARED]\nrow\n[LST:EMPTY]\n[END:EMPTY]\n[LIST:shared]\nlast\n[END]\n[SECTION:AFTER]\nk=end\n");
        equal("same|same|x=a=b|", String.Join("|", Mixed.getList("SHARED").ToArray()));
        equal("v", get(Mixed, "SHARED", "k"));
        equal("row", Mixed.getTable("SHARED")[0]);
        equal("end", get(Mixed, "AFTER", "k"));
        equal("EMPTY|SHARED|shared", String.Join("|", Mixed.listLists().ToArray()));
        List<string> Names = Mixed.listLists();
        Names.Clear();
        equal(3, Mixed.listLists().Count);
        equal(0, Mixed.getList("missing").Count);
        Mixed.getList("missing").Add("detached");
        equal(0, Mixed.getList("missing").Count);
        List<string> Items = Mixed.getList("EMPTY");
        Items.Add("added=value");
        equal(true, Object.ReferenceEquals(Items, Mixed.getList("EMPTY")));
        Mixed.save("mixed.ini");
        equal("added=value", new ConfigurationFile("mixed.ini").getList("EMPTY")[0]);
        string Saved = File.ReadAllText("mixed.ini");
        Items.Add("## not representable");
        throws<InvalidOperationException>(delegate { Mixed.save("mixed.ini"); });
        equal(Saved, File.ReadAllText("mixed.ini"));
        throws<ArgumentNullException>(delegate { Mixed.getList(null); });
        throws<ArgumentException>(delegate { load("[LIST:A]\n[END]\n[LST:A]\n"); });
        throws<FormatException>(delegate { load("[LST:A]\n[END:a]\n"); });
        using (FileStream Stream = File.Open("config.ini", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            equal(true, Stream.CanWrite);
        ConfigurationFile Empty = new ConfigurationFile();
        equal(0, Empty.listLists().Count);
        equal(0, Empty.getList("missing").Count);
    }

    static int Main()
    {
        test("LIST and LST preserve raw ordered items and round-trip with other blocks", listBlocks);
        test("SEC and SECTION headers support optional named and unnamed END", sectionBlocks);
        test("TABLE and TBL blocks support optional named and unnamed END", tableBlocks);
        test("comment recognition uses configured prefixes", configuredComments);
        test("automatic saving occurs only for newly created entries", automaticSaving);
        test("automatic save failures roll back new entries", automaticSaveFailures);
        test("registry dump shares save serialization and respects stream ownership", registryDump);
        test("registry creation can be enabled and disabled at runtime", toggleRegistryUpdates);
        test("automatic registry creation respects the property", automaticRegistryUpdates);
        test("save overloads preserve sections, tables and original destination", saving);
        test("save rejects unrepresentable data and preserves destinations on failure", saveFailures);
        test("name listings are sorted independent snapshots", listNames);
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
