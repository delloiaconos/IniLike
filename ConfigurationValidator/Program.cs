using System;
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
            if (Args.Length != 1 || String.IsNullOrEmpty(Args[0])) {
                Console.Error.WriteLine("Error: expected exactly one configuration file path.");
                Console.Error.WriteLine("Usage: ConfigurationValidator.exe <file.ini>");
                return 2;
            }

            try {
                ConfigurationFile Config = new ConfigurationFile(Args[0]);
                Console.WriteLine("Configuration loaded successfully: {0}", Args[0]);
                Console.WriteLine("Sections: {0}; tables: {1}; lists: {2}.",
                    Config.listSections().Count, Config.listTables().Count, Config.listLists().Count);
                return 0;
            } catch (Exception Error) {
                Console.Error.WriteLine("Error loading '{0}':", Args[0]);
                for (Exception Cause = Error; Cause != null; Cause = Cause.InnerException) {
                    Console.Error.WriteLine("{0}: {1}", Cause.GetType().Name, Cause.Message);
                }
                return 1;
            }
        }

        private static void printUsage()
        {
            Console.WriteLine("Usage: ConfigurationValidator.exe <file.ini>");
            Console.WriteLine("Loads a configuration using ConfigurationFilesReader and reports library errors.");
            Console.WriteLine("Validation follows the library parser: ignored lines are not reported as errors.");
            Console.WriteLine("Exit codes: 0 = loaded/help; 1 = loading error; 2 = invalid arguments.");
        }
    }
}
