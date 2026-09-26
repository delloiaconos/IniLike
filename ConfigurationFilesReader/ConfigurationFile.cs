#define Debug

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ConfigurationFilesReader
{
    using strDictionary = Dictionary<string, string>;
    using strTable = List<string>;
    using System.Globalization;

    public class ConfigurationFile
    {
        private enum SectionType { None = 0, Section, Table };

        public readonly string[] parComment = { "##" };
        public readonly char[] parSeparator = { '=' };
        public readonly char[] parEndLineDelimiter = { ';', ',', '.' };
        public bool autoUpdateRegistry { get; set; }
        public bool autoSaveRegistry { get; set; }

        private readonly FileInfo FilePath;
        private Dictionary<string, strDictionary> dictSections;
        private Dictionary<string, strTable> dictTables;
        
        public ConfigurationFile(string FileName)
            : this(new FileInfo(FileName))
        {
        }

        public ConfigurationFile(FileInfo FilePath)
            : this()
        {
            if (FilePath == null) {
                throw new ArgumentNullException("FilePath");
            }
            this.FilePath = FilePath;
            if (!loadFile()) {
                throw new System.Exception("Unable to open file: '" + FilePath.FullName + "'.");
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

        // Match full-line comments against the configured prefixes.
        private bool isComment(string Line)
        {
            foreach (string Prefix in parComment) {
                if (!String.IsNullOrEmpty(Prefix) && Line.StartsWith(Prefix, StringComparison.Ordinal)) {
                    return true;
                }
            }
            return false;
        }

        // Load the configuration file content.
        private bool loadFile()
        {
            if (File.Exists(FilePath.FullName)) {
                using (StreamReader srFile = FilePath.OpenText()) {

                    string currentParent = "";
                    SectionType reading = SectionType.None;

                    while (!srFile.EndOfStream) {
                        string currentLine = srFile.ReadLine();
                        currentLine = currentLine.Trim();
                        if (isComment(currentLine)) {
                            continue;
                        }

                        if (currentLine.StartsWith("[TABLE:") && currentLine.EndsWith("]")) {
                            currentParent = currentLine.Substring(7, currentLine.Length - 1 - 7).Trim();
                            reading = SectionType.Table;
                            dictTables.Add(currentParent, new strTable());
                        } else if (currentLine.StartsWith("[") && currentLine.EndsWith("]")) {
                            currentParent = currentLine.Substring(1, currentLine.Length - 1 - 1).Trim();
                            reading = SectionType.Section;
                            dictSections.Add(currentParent, new strDictionary());
                        } else if (reading == SectionType.Section && currentLine.Length > 0) {
                            string[] sline = currentLine.Split(parSeparator);
                            if (sline.Count() == 2) {
                                sline[1] = sline[1].Trim().TrimEnd(parEndLineDelimiter);
                                dictSections[currentParent].Add(sline[0].Trim(), sline[1]);
                            }
                        } else if (reading == SectionType.Table && currentLine.Length > 0) {
                            currentLine = currentLine.Trim().TrimEnd(parEndLineDelimiter);
                            dictTables[currentParent].Add(currentLine);
                        }
                    }
                }
                return true;
            } else {
                return false;
            }
            
        }

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
                    Writer.WriteLine(Parameter + "=" + dictSections[SecName][Parameter] + ";");
                }
            }
            foreach (string Table in listTables()) {
                Writer.WriteLine("[TABLE:" + Table + "]");
                foreach (string Row in dictTables[Table]) {
                    Writer.WriteLine(Row + ";");
                }
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
                    Reloaded = new ConfigurationFile(TemporaryPath);
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
            if (dictSections.Count != Other.dictSections.Count || dictTables.Count != Other.dictTables.Count) {
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
            return true;
        }

        // Check whether the section exists.
        public bool checkSection(string SecName)
        {
            return dictSections.ContainsKey(SecName);
        }

        // Update existing values; create missing entries only when enabled.
        public void setParameter(string SecName, string ParName, string value)
        {
            if (SecName == null) {
                throw new ArgumentNullException("SecName");
            }
            if (ParName == null) {
                throw new ArgumentNullException("ParName");
            }
            strDictionary Parameters;
            if (dictSections.TryGetValue(SecName, out Parameters) && Parameters.ContainsKey(ParName)) {
                Parameters[ParName] = value;
            } else if (autoUpdateRegistry) {
                addParameter(SecName, ParName, value);
            }
        }

        // Add a missing default and optionally save the registry to its original file.
        public void addParameter(string SecName, string ParName, string DefaultVal)
        {
            if (SecName == null) {
                throw new ArgumentNullException("SecName");
            }
            if (ParName == null) {
                throw new ArgumentNullException("ParName");
            }

            if (!autoUpdateRegistry) {
                return;
            }

            strDictionary Parameters;
            bool NewSection = !dictSections.TryGetValue(SecName, out Parameters);
            if (!NewSection && Parameters.ContainsKey(ParName)) {
                return;
            }
            if (autoSaveRegistry && FilePath == null) {
                throw new InvalidOperationException("Automatic saving requires an original configuration file.");
            }
            if (NewSection) {
                Parameters = new strDictionary();
                dictSections.Add(SecName, Parameters);
            }
            Parameters.Add(ParName, DefaultVal);
            if (autoSaveRegistry) {
                try {
                    save();
                } catch {
                    // Undo this addition if the original file could not be saved.
                    Parameters.Remove(ParName);
                    if (NewSection) {
                        dictSections.Remove(SecName);
                    }
                    throw;
                }
            }
        }

        // Get a parameter value or use the default when missing.
        public string getParameter(string SecName, string ParName, string DefaultVal )
        {
            if (checkSection(SecName)) {
                strDictionary sectParameters = dictSections[SecName];
                if (sectParameters.ContainsKey(ParName)) {
                    return sectParameters[ParName];
                } else {
                    if (autoUpdateRegistry) {
                        addParameter(SecName, ParName, DefaultVal);
                    }
                    return DefaultVal;
                }
            } else {
                if (autoUpdateRegistry) {
                    addParameter(SecName, ParName, DefaultVal);
                }
                return DefaultVal;
            }
        }

        // Get a boolean parameter.
        public bool getParameter(string SecName, string ParName, bool DefaultVal)
        {
            string strVal = getParameter(SecName, ParName, DefaultVal ? "TRUE" : "FALSE");
            strVal = strVal.Trim().ToUpper();
            return strVal == "TRUE" || strVal == "1";
        }

        // Get a double parameter.
        public double getParameter(string SecName, string ParName, double DefaultVal)
        {
            string strVal = getParameter(SecName, ParName, DefaultVal.ToString(CultureInfo.InvariantCulture));
            strVal = strVal.Trim();
            strVal = strVal.Replace(',', '.');
            double outVal;
            try {
                outVal = double.Parse(strVal, CultureInfo.InvariantCulture);
            } catch {
                outVal = DefaultVal;
            }

            return outVal;
        }

        // Get a float parameter.
        public float getParameter(string SecName, string ParName, float DefaultVal)
        {
            return (float)((double)getParameter(SecName, ParName, (double)DefaultVal));
        }

        // Get a long parameter.
        public long getParameter(string SecName, string ParName, long DefaultVal)
        {
            string strVal = getParameter(SecName, ParName, DefaultVal.ToString(CultureInfo.InvariantCulture));
            strVal = strVal.Trim();
            
            long outVal;
            try {
                outVal = long.Parse(strVal, CultureInfo.InvariantCulture);
            } catch {
                outVal = DefaultVal;
            }

            return outVal;
        }

        // Get an integer parameter.
        public int getParameter(string SecName, string ParName, int DefaultVal)
        {
            return (int)((long)getParameter(SecName, ParName, (long)DefaultVal));
        }

        // Return a sorted snapshot of section names, excluding tables.
        public List<string> listSections()
        {
            List<string> Names = new List<string>(dictSections.Keys);
            Names.Sort(StringComparer.Ordinal);
            return Names;
        }

        // Return a sorted snapshot of parameter names in a section.
        public List<string> listParameters(string SecName)
        {
            if (SecName == null) {
                throw new ArgumentNullException("SecName");
            }
            strDictionary Parameters;
            if (!dictSections.TryGetValue(SecName, out Parameters)) {
                return new List<string>();
            }
            List<string> Names = new List<string>(Parameters.Keys);
            Names.Sort(StringComparer.Ordinal);
            return Names;
        }

        // Return distinct parameter names across all sections, sorted ordinally.
        public List<string> listParameters()
        {
            HashSet<string> UniqueNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (strDictionary Parameters in dictSections.Values)
                foreach (string Name in Parameters.Keys)
                    UniqueNames.Add(Name);
            List<string> Names = new List<string>(UniqueNames);
            Names.Sort(StringComparer.Ordinal);
            return Names;
        }

        // Return a sorted snapshot of table names, excluding sections.
        public List<string> listTables()
        {
            List<string> Names = new List<string>(dictTables.Keys);
            Names.Sort(StringComparer.Ordinal);
            return Names;
        }

        // Get a table by name.
        public strTable getTable(string tablename)
        {
            if (dictTables.ContainsKey(tablename)) {
                return dictTables[tablename];
            } else {
                return new strTable(0);
            }
        }
    }
}
