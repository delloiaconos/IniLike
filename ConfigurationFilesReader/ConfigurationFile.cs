#define Debug

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

        private readonly FileInfo FilePath;

        public ConfigurationFile(string FileName)
            : this(new FileInfo(FileName))
        {
        }

        public ConfigurationFile(FileInfo FilePath)
            : this()
        {
            if (FilePath == null) {
                throw new ArgumentNullException("Expected 'FilePath' not null!");
            }
            this.FilePath = FilePath;
            if (!this.FilePath.Exists)
            {
                throw new System.Exception("File '" + FilePath.FullName + "' not found.");
            } else if (!loadFile())
            {
                throw new System.Exception("Unable to load file '" + FilePath.FullName + "'.");
            }
        }

        public ConfigurationFile(DirectoryInfo BaseDirectory, string FileName)
            : this(combinePath(BaseDirectory, FileName))
        {
        }

        public ConfigurationFile()
        {
            dictSections = new Dictionary<string, strDictionary>();
            dictTables = new Dictionary<string, strTable>();
            dictLists = new Dictionary<string, List<string>>();
            dictDictionaries = new Dictionary<string, strDictionary>();
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
