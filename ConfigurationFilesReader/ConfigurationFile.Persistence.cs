using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ConfigurationFilesReader
{
    using strDictionary = Dictionary<string, string>;
    using strTable = List<string>;

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

        // Validate serialized data with the same parser used by callers.
        private bool hasSameData(ConfigurationFile Other)
        {
            if (dictSections.Count != Other.dictSections.Count || dictTables.Count != Other.dictTables.Count || dictLists.Count != Other.dictLists.Count || dictDictionaries.Count != Other.dictDictionaries.Count || dictTexts.Count != Other.dictTexts.Count || dictEncoded.Count != Other.dictEncoded.Count) {
                return false;
            }
            foreach (KeyValuePair<string, strDictionary> SecName in dictSections) {
                strDictionary Parameters;
                if (!Other.dictSections.TryGetValue(SecName.Key, out Parameters) || SecName.Value.Count != Parameters.Count) {
                    return false;
                }
                foreach (KeyValuePair<string, string> Parameter in SecName.Value) {
                    string Value;
                    if (!Parameters.TryGetValue(Parameter.Key, out Value) || Parameter.Value != Value) {
                        return false;
                    }
                }
            }
            foreach (KeyValuePair<string, strTable> Table in dictTables) {
                strTable Rows;
                if (!Other.dictTables.TryGetValue(Table.Key, out Rows) || Table.Value.Count != Rows.Count) {
                    return false;
                }
                for (int Index = 0; Index < Rows.Count; Index++) {
                    if (Table.Value[Index] != Rows[Index]) {
                        return false;
                    }
                }
            }
            foreach (KeyValuePair<string, List<string>> List in dictLists) {
                List<string> Items;
                if (!Other.dictLists.TryGetValue(List.Key, out Items) || List.Value.Count != Items.Count) {
                    return false;
                }
                for (int Index = 0; Index < Items.Count; Index++) {
                    if (List.Value[Index] != Items[Index]) {
                        return false;
                    }
                }
            }
            foreach (KeyValuePair<string, List<string>> Text in dictTexts) {
                List<string> Lines;
                if (!Other.dictTexts.TryGetValue(Text.Key, out Lines) || Text.Value.Count != Lines.Count) {
                    return false;
                }
                for (int Index = 0; Index < Lines.Count; Index++) {
                    if (Text.Value[Index] != Lines[Index]) {
                        return false;
                    }
                }
            }
            foreach (KeyValuePair<string, byte[]> Block in dictEncoded) {
                byte[] Data;
                if (!Other.dictEncoded.TryGetValue(Block.Key, out Data) || Block.Value.Length != Data.Length) {
                    return false;
                }
                for (int Index = 0; Index < Data.Length; Index++) {
                    if (Block.Value[Index] != Data[Index]) {
                        return false;
                    }
                }
            }
            foreach (KeyValuePair<string, strDictionary> Dictionary in dictDictionaries) {
                strDictionary Entries;
                if (!Other.dictDictionaries.TryGetValue(Dictionary.Key, out Entries) || Dictionary.Value.Count != Entries.Count) {
                    return false;
                }
                foreach (KeyValuePair<string, string> Entry in Dictionary.Value) {
                    string Value;
                    if (!Entries.TryGetValue(Entry.Key, out Value) || Entry.Value != Value) {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
