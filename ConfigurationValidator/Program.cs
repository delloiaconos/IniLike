using System;
using System.Collections.Generic;
using ConfigurationFilesReader;

namespace ConfigurationValidator
{
    internal static class Program
    {
        private static int Main(string[] Args)
        {
            if (Args.Length == 1 && (Args[0] == "--help" || Args[0] == "-h")) {
                printUsage();
                return 0;
            }
            string FileName = null;
            HashSet<string> Options = new HashSet<string>(StringComparer.Ordinal);
            bool EndOptions = false;
            foreach (string Arg in Args) {
                if (!EndOptions && Arg == "--") {
                    EndOptions = true;
                } else if (!EndOptions && Arg.StartsWith("-", StringComparison.Ordinal)) {
                    switch (Arg) {
                        case "--sections":
                        case "--dictionaries":
                        case "--tables":
                        case "--lists":
                        case "--parameters":
                            Options.Add(Arg);
                            break;
                        default:
                            Console.Error.WriteLine("Error: unknown option '{0}'. Use --help for usage.", Arg);
                            return 2;
                    }
                } else if (String.IsNullOrEmpty(Arg) || FileName != null) {
                    Console.Error.WriteLine("Error: expected exactly one configuration file path.");
                    return 2;
                } else {
                    FileName = Arg;
                }
            }
            if (FileName == null) {
                Console.Error.WriteLine("Error: expected exactly one configuration file path.");
                Console.Error.WriteLine("Usage: ConfigurationValidator.exe [options] <file.ini>");
                return 2;
            }

            try {
                ConfigurationFile Config = new ConfigurationFile(FileName);
                if (Options.Count == 0) {
                    Console.WriteLine("Configuration loaded successfully: {0}", FileName);
                    Console.WriteLine("Sections: {0}; tables: {1}; lists: {2}; dictionaries: {3}.",
                        Config.listSections().Count, Config.listTables().Count, Config.listLists().Count,
                        Config.listDictionaries().Count);
                } else {
                    if (Options.Contains("--sections"))
                        printNames("Sections", Config.listSections());
                    if (Options.Contains("--dictionaries"))
                        printNames("Dictionaries", Config.listDictionaries());
                    if (Options.Contains("--tables"))
                        printNames("Tables", Config.listTables());
                    if (Options.Contains("--lists"))
                        printNames("Lists", Config.listLists());
                    if (Options.Contains("--parameters"))
                        printNames("Parameters", Config.listParameters());
                }
                return 0;
            } catch (Exception Error) {
                Console.Error.WriteLine("Error loading '{0}':", FileName);
                for (Exception Cause = Error; Cause != null; Cause = Cause.InnerException) {
                    Console.Error.WriteLine("{0}: {1}", Cause.GetType().Name, Cause.Message);
                }
                return 1;
            }
        }

        private static void printNames(string Label, IEnumerable<string> Names)
        {
            Console.WriteLine("{0}:", Label);
            foreach (string Name in Names)
                Console.WriteLine(Name);
        }

        private static void printUsage()
        {
            Console.WriteLine("Usage: ConfigurationValidator.exe [options] <file.ini>");
            Console.WriteLine("Loads a configuration using ConfigurationFilesReader and reports library errors.");
            Console.WriteLine("Validation follows the library parser: ignored lines are not reported as errors.");
            Console.WriteLine("Options (may be combined, before or after the file path):");
            Console.WriteLine("  --sections      List section names (plain, SEC or SECTION).");
            Console.WriteLine("  --dictionaries  List dictionary names (DICT or DICTIONARY).");
            Console.WriteLine("  --tables        List table names (TBL or TABLE).");
            Console.WriteLine("  --lists         List list names (LST or LIST).");
            Console.WriteLine("  --parameters    List distinct parameter names across all sections.");
            Console.WriteLine("Lists are sorted ordinally, under category headings, without values.");
            Console.WriteLine("  --              End options, allowing a file path beginning with '-'.");
            Console.WriteLine("  -h, --help      Show this help (used alone).");
            Console.WriteLine("Exit codes: 0 = loaded/help; 1 = loading error; 2 = invalid arguments.");
        }
    }
}
