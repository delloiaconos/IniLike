using System;
using System.Collections.Generic;
using System.IO;

namespace ConfigurationFilesReader
{
    using strDictionary = Dictionary<string, string>;
    using strTable = List<string>;

    public partial class ConfigurationFile
    {
        public readonly string[] parComment = { "##" };
        public readonly char[] parSeparator = { '=' };
        public bool autoUpdateRegistry { get; set; }
        public bool autoSaveRegistry { get; set; }

        public bool CaseSensitive { get; private set; }

        private readonly StringComparer NameComparer;
        private readonly FileInfo FilePath;
        private readonly string IncludeBaseDirectory;
        private readonly string StreamSourceName;
        private readonly Func<string, string, string> IncludeResolver;

        public List<string> listLoadedFiles()
        {
            return sortedNames(loadedFiles);
        }

        public ConfigurationFile(string FileName)
            : this(FileName, true)
        {
        }

        public ConfigurationFile(string FileName, bool CaseSensitive)
            : this(new FileInfo(FileName), CaseSensitive)
        {
        }

        public ConfigurationFile(string FileName, ConfigurationLoadOptions Options)
            : this(new FileInfo(FileName), Options, true)
        {
        }

        public ConfigurationFile(string FileName, ConfigurationLoadOptions Options, bool CaseSensitive)
            : this(new FileInfo(FileName), Options, CaseSensitive)
        {
        }

        public ConfigurationFile(FileInfo FilePath)
            : this(FilePath, true)
        {
        }

        public ConfigurationFile(FileInfo FilePath, bool CaseSensitive)
            : this(FilePath, null, CaseSensitive)
        {
        }

        public ConfigurationFile(FileInfo FilePath, ConfigurationLoadOptions Options)
            : this(FilePath, Options, true)
        {
        }

        public ConfigurationFile(FileInfo FilePath, ConfigurationLoadOptions Options, bool CaseSensitive)
            : this(Options, CaseSensitive)
        {
            if (FilePath == null) {
                throw new ArgumentNullException("Expected 'FilePath' not null!");
            }
            this.FilePath = FilePath;
            loadFile();
        }

        // Read from the current position without taking ownership of the stream.
        public ConfigurationFile(Stream Source)
            : this(Source, true)
        {
        }

        public ConfigurationFile(Stream Source, bool CaseSensitive)
            : this(Source, null, CaseSensitive)
        {
        }

        public ConfigurationFile(Stream Source, ConfigurationLoadOptions Options)
            : this(Source, Options, true)
        {
        }

        public ConfigurationFile(Stream Source, ConfigurationLoadOptions Options, bool CaseSensitive)
            : this(Options, CaseSensitive)
        {
            if (Source == null) {
                throw new ArgumentNullException("Source");
            }
            if (!Source.CanRead) {
                throw new ArgumentException("The stream must be readable.", "Source");
            }
            using (StreamReader Reader = new StreamReader(new BorrowedReadStream(Source))) {
                FileStream FileSource = Source as FileStream;
                string SourcePath = FileSource == null ? null : Path.GetFullPath(FileSource.Name);
                if (SourcePath != null) { loadingFiles.Add(SourcePath); }
                try {
                    loadReader(Reader, SourcePath, StreamSourceName ?? SourcePath);
                    if (SourcePath != null) { loadedFiles.Add(SourcePath); }
                } finally {
                    if (SourcePath != null) { loadingFiles.Remove(SourcePath); }
                }
            }
        }

        public ConfigurationFile(DirectoryInfo BaseDirectory, string FileName)
            : this(BaseDirectory, FileName, true)
        {
        }

        public ConfigurationFile(DirectoryInfo BaseDirectory, string FileName, bool CaseSensitive)
            : this(combinePath(BaseDirectory, FileName), CaseSensitive)
        {
        }

        public ConfigurationFile()
            : this(true)
        {
        }

        public ConfigurationFile(bool CaseSensitive)
            : this((ConfigurationLoadOptions)null, CaseSensitive)
        {
        }

        private ConfigurationFile(ConfigurationLoadOptions Options, bool CaseSensitive)
        {
            if (Options != null) {
                if (Options.SourceName != null && Options.SourceName.Trim().Length == 0) {
                    throw new ArgumentException("SourceName must not be empty or whitespace.", "Options");
                }
                StreamSourceName = Options.SourceName;
                IncludeBaseDirectory = Options.IncludeBaseDirectory == null ? null : Path.GetFullPath(Options.IncludeBaseDirectory);
                IncludeResolver = Options.IncludeResolver;
            }
            this.CaseSensitive = CaseSensitive;
            NameComparer = CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            dictSections = new Dictionary<string, strDictionary>(NameComparer);
            dictTables = new Dictionary<string, strTable>(NameComparer);
            dictLists = new Dictionary<string, List<string>>(NameComparer);
            dictDictionaries = new Dictionary<string, strDictionary>(NameComparer);
            dictTexts = new Dictionary<string, List<string>>(NameComparer);
            dictEncoded = new Dictionary<string, byte[]>(NameComparer);
        }

        // Keep listing order ordinal regardless of the lookup comparer.
        private static List<string> sortedNames(IEnumerable<string> Names)
        {
            List<string> Result = new List<string>(Names);
            Result.Sort(StringComparer.Ordinal);
            return Result;
        }

        private static FileInfo combinePath(DirectoryInfo BaseDirectory, string FileName)
        {
            if (BaseDirectory == null) {
                throw new ArgumentNullException("BaseDirectory");
            }
            if (FileName == null) {
                throw new ArgumentNullException("FileName");
            }
            return new FileInfo(Path.Combine(BaseDirectory.FullName, FileName));
        }
    }
}
