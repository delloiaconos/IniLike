using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ConfigurationFilesReader
{
    using strDictionary = Dictionary<string, string>;

    public partial class ConfigurationFile
    {
        // Return a flushed UTF-8 dump positioned for reading. The caller owns it.
        public StreamWriter dump()
        {
            StreamWriter Writer = new StreamWriter(new MemoryStream(), new UTF8Encoding(false, true));
            try {
                dump(Writer);
                Writer.BaseStream.Position = 0;
                return Writer;
            } catch {
                Writer.Dispose();
                throw;
            }
        }

        // Write at the current position without closing the caller's writer.
        public void dump(StreamWriter Writer)
        {
            if (Writer == null) {
                throw new ArgumentNullException("Writer");
            }
            foreach (string SecName in listSections()) {
                Writer.WriteLine("[" + SecName + "]");
                foreach (string Parameter in listParameters(SecName)) {
                    Writer.WriteLine(Parameter + "=" + dictSections[SecName][Parameter]);
                }
            }
            foreach (string Table in listTables()) {
                Writer.WriteLine("[TABLE:" + Table + "]");
                foreach (string Row in dictTables[Table]) {
                    Writer.WriteLine(Row);
                }
            }
            foreach (string Name in listLists()) {
                Writer.WriteLine("[LIST:" + Name + "]");
                foreach (string Item in dictLists[Name]) {
                    Writer.WriteLine(Item);
                }
            }
            foreach (string Name in listDictionaries()) {
                Writer.WriteLine("[DICT:" + Name + "]");
                List<string> Keys = new List<string>(dictDictionaries[Name].Keys);
                Keys.Sort(StringComparer.Ordinal);
                foreach (string Key in Keys) {
                    Writer.WriteLine(Key + "=" + dictDictionaries[Name][Key]);
                }
            }
            foreach (string Name in listTexts()) {
                Writer.WriteLine("[TEXT:" + Name + "]");
                foreach (string Line in dictTexts[Name]) {
                    Writer.WriteLine(Line);
                }
                Writer.WriteLine("[END]");
            }
            foreach (string Name in listEncoded()) {
                Writer.WriteLine("[ENCODED:" + Name + "]");
                Writer.WriteLine(Convert.ToBase64String(dictEncoded[Name]));
            }
            Writer.Flush();
        }

        // Save to the original source file without changing its identity.
        public void save()
        {
            if (FilePath == null) {
                throw new InvalidOperationException("No original file is associated with this configuration. Specify a destination.");
            }
            save(FilePath);
        }

        public void save(string FileName)
        {
            if (FileName == null) {
                throw new ArgumentNullException("FileName");
            }
            save(new FileInfo(FileName));
        }

        // Path represents the base directory, despite being supplied as FileInfo.
        public void save(FileInfo Path, string FileName)
        {
            if (Path == null) {
                throw new ArgumentNullException("Path");
            }
            if (FileName == null) {
                throw new ArgumentNullException("FileName");
            }
            save(new FileInfo(System.IO.Path.Combine(Path.FullName, FileName)));
        }

        public void save(FileInfo File)
        {
            if (File == null) {
                throw new ArgumentNullException("File");
            }
            string TemporaryPath = System.IO.Path.Combine(File.DirectoryName,
                ".inilike-" + Guid.NewGuid().ToString("N") + ".tmp");
            bool TemporaryCreated = false;
            try {
                // Create beside the destination so replacement stays on the same filesystem.
                using (FileStream Stream = new FileStream(TemporaryPath, FileMode.CreateNew, FileAccess.Write)) {
                    TemporaryCreated = true;
                    using (StreamWriter Dump = dump()) {
                        byte[] Buffer = new byte[8192];
                        int Count;
                        while ((Count = Dump.BaseStream.Read(Buffer, 0, Buffer.Length)) > 0) {
                            Stream.Write(Buffer, 0, Count);
                        }
                    }
                }
                ConfigurationFile Reloaded;
                try {
                    Reloaded = new ConfigurationFile(TemporaryPath, CaseSensitive);
                } catch (FormatException Error) {
                    throw new InvalidOperationException("The configuration cannot be represented by the INI-like format.", Error);
                } catch (ArgumentException Error) {
                    throw new InvalidOperationException("The configuration cannot be represented by the INI-like format.", Error);
                }
                if (!hasSameData(Reloaded)) {
                    throw new InvalidOperationException("The configuration cannot be represented by the INI-like format.");
                }
                if (System.IO.File.Exists(File.FullName)) {
                    System.IO.File.Replace(TemporaryPath, File.FullName, null);
                } else {
                    System.IO.File.Move(TemporaryPath, File.FullName);
                }
            } finally {
                if (TemporaryCreated && System.IO.File.Exists(TemporaryPath)) {
                    System.IO.File.Delete(TemporaryPath);
                }
            }
        }

        // Compare names using the registry comparers and values without normalization.
        private bool hasSameData(ConfigurationFile Other)
        {
            return hasSameBlocks(dictSections, Other.dictSections, hasSameParameters) &&
                hasSameBlocks(dictDictionaries, Other.dictDictionaries, hasSameParameters) &&
                hasSameBlocks(dictTables, Other.dictTables, hasSameRows) &&
                hasSameBlocks(dictLists, Other.dictLists, hasSameRows) &&
                hasSameBlocks(dictTexts, Other.dictTexts, hasSameRows) &&
                hasSameBlocks(dictEncoded, Other.dictEncoded,
                    delegate(byte[] Left, byte[] Right) { return hasSameSequence(Left, Right); });
        }

        private static bool hasSameBlocks<T>(Dictionary<string, T> Left,
            Dictionary<string, T> Right, Func<T, T, bool> EqualValues)
        {
            if (Left.Count != Right.Count) { return false; }
            foreach (KeyValuePair<string, T> Entry in Left) {
                T Value;
                if (!Right.TryGetValue(Entry.Key, out Value) || !EqualValues(Entry.Value, Value)) {
                    return false;
                }
            }
            return true;
        }

        private static bool hasSameParameters(strDictionary Left, strDictionary Right)
        {
            return hasSameBlocks(Left, Right,
                delegate(string First, string Second) { return First == Second; });
        }

        private static bool hasSameRows(List<string> Left, List<string> Right)
        {
            return hasSameSequence(Left, Right);
        }

        private static bool hasSameSequence<T>(IList<T> Left, IList<T> Right)
        {
            if (Left.Count != Right.Count) { return false; }
            for (int Index = 0; Index < Left.Count; Index++) {
                if (!EqualityComparer<T>.Default.Equals(Left[Index], Right[Index])) { return false; }
            }
            return true;
        }
    }
}
