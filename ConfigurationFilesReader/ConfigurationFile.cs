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
        public readonly char[] parEndLineDelimiter = { ';', ',', '.' };
        public bool autoUpdateRegistry { get; set; }
        public bool autoSaveRegistry { get; set; }

        public bool CaseSensitive { get; private set; }

        private readonly StringComparer NameComparer;
        private readonly FileInfo FilePath;

        public ConfigurationFile(string FileName)
            : this(FileName, true)
        {
        }

        public ConfigurationFile(string FileName, bool CaseSensitive)
            : this(new FileInfo(FileName), CaseSensitive)
        {
        }

        public ConfigurationFile(FileInfo FilePath)
            : this(FilePath, true)
        {
        }

        public ConfigurationFile(FileInfo FilePath, bool CaseSensitive)
            : this(CaseSensitive)
        {
            if (FilePath == null) {
                throw new ArgumentNullException("Expected 'FilePath' not null!");
            }
            this.FilePath = FilePath;
            if (!this.FilePath.Exists)
            {
                throw withSourceContext(new System.Exception("File '" + FilePath.FullName + "' not found."), FilePath.FullName, 0);
            } else if (!loadFile())
            {
                throw withSourceContext(new System.Exception("Unable to load file '" + FilePath.FullName + "'."), FilePath.FullName, 0);
            }
        }

        // Read from the current position without taking ownership of the stream.
        public ConfigurationFile(Stream Source)
            : this(Source, true)
        {
        }

        public ConfigurationFile(Stream Source, bool CaseSensitive)
            : this(CaseSensitive)
        {
            if (Source == null) {
                throw new ArgumentNullException("Source");
            }
            if (!Source.CanRead) {
                throw new ArgumentException("The stream must be readable.", "Source");
            }
            using (StreamReader Reader = new StreamReader(new BorrowedReadStream(Source))) {
                FileStream FileSource = Source as FileStream;
                loadReader(Reader, FileSource == null ? null : Path.GetFullPath(FileSource.Name));
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
        {
            this.CaseSensitive = CaseSensitive;
            NameComparer = CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            dictSections = new Dictionary<string, strDictionary>(NameComparer);
            dictTables = new Dictionary<string, strTable>(NameComparer);
            dictLists = new Dictionary<string, List<string>>(NameComparer);
            dictDictionaries = new Dictionary<string, strDictionary>(NameComparer);
            dictTexts = new Dictionary<string, List<string>>(NameComparer);
            dictEncoded = new Dictionary<string, byte[]>(NameComparer);
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
