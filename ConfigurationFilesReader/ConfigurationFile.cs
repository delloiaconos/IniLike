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

        public readonly char[] parSeparator = { '=' };
        public readonly char[] parEndLineDelimiter = { ';', ',', '.' };
        public bool autoUpdateRegistry { get; set; }

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

                        if (currentLine.StartsWith("[TABLE:") && currentLine.EndsWith("]")) {
                            currentParent = currentLine.Substring(7, currentLine.Length - 1 - 7).Trim();
                            reading = SectionType.Table;
                            dictTables.Add(currentParent, new strTable());
                        } else if (currentLine.StartsWith("[") && currentLine.EndsWith("]")) {
                            currentParent = currentLine.Substring(1, currentLine.Length - 1 - 1).Trim();
                            reading = SectionType.Section;
                            dictSections.Add(currentParent, new strDictionary());
                        } else if (currentLine.Length > 0 && currentLine.StartsWith("##")) {
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
            foreach (string Section in listSections()) {
                Writer.WriteLine("[" + Section + "]");
                foreach (string Parameter in listParameters(Section)) {
                    Writer.WriteLine(Parameter + "=" + dictSections[Section][Parameter] + ";");
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
            foreach (KeyValuePair<string, strDictionary> Section in dictSections) {
                strDictionary Parameters;
                if (!Other.dictSections.TryGetValue(Section.Key, out Parameters) || Section.Value.Count != Parameters.Count) {
                    return false;
                }
                foreach (KeyValuePair<string, string> Parameter in Section.Value) {
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
        public bool checkSection(string SectionName)
        {
            return dictSections.ContainsKey(SectionName);
        }

        // Update existing values; create missing entries only when enabled.
        public void setParameter(string sectionName, string parameterName, string value)
        {
            if (sectionName == null) {
                throw new ArgumentNullException("sectionName");
            }
            if (parameterName == null) {
                throw new ArgumentNullException("parameterName");
            }
            strDictionary Parameters;
            if (dictSections.TryGetValue(sectionName, out Parameters) && Parameters.ContainsKey(parameterName)) {
                Parameters[parameterName] = value;
            } else if (autoUpdateRegistry) {
                addParameter(sectionName, parameterName, value);
            }
        }

        // Add a default parameter in memory without replacing an existing value.
        public void addParameter(string SectionName, string ParameterName, string DefaultValue)
        {
            if (SectionName == null) {
                throw new ArgumentNullException("SectionName");
            }
            if (ParameterName == null) {
                throw new ArgumentNullException("ParameterName");
            }

            if (!autoUpdateRegistry) {
                return;
            }

            strDictionary parameters;
            if (!dictSections.TryGetValue(SectionName, out parameters)) {
                parameters = new strDictionary();
                dictSections.Add(SectionName, parameters);
            }
            if (!parameters.ContainsKey(ParameterName)) {
                parameters.Add(ParameterName, DefaultValue);
            }
        }

        // Get a parameter value or use the default when missing.
        public string getParameter(string SectionName, string ParameterName, string DefaultValue )
        {
            if (checkSection(SectionName)) {
                strDictionary sectParameters = dictSections[SectionName];
                if (sectParameters.ContainsKey(ParameterName)) {
                    return sectParameters[ParameterName];
                } else {
                    if (autoUpdateRegistry) {
                        addParameter(SectionName, ParameterName, DefaultValue);
                    }
                    return DefaultValue;
                }
            } else {
                if (autoUpdateRegistry) {
                    addParameter(SectionName, ParameterName, DefaultValue);
                }
                return DefaultValue;
            }
        }

        // Get a boolean parameter.
        public bool getParameter(string SectionName, string ParameterName, bool DefaultValue)
        {
            string strParameter = getParameter(SectionName, ParameterName, DefaultValue ? "TRUE" : "FALSE");
            strParameter = strParameter.Trim().ToUpper();
            return strParameter.CompareTo("TRUE") == 0 ? true : false;
        }

        // Get a double parameter.
        public double getParameter(string SectionName, string ParameterName, double DefaultValue)
        {
            string strParameter = getParameter(SectionName, ParameterName, DefaultValue.ToString(CultureInfo.InvariantCulture));
            strParameter = strParameter.Trim();
            strParameter = strParameter.Replace(',', '.');
            double OutValue;
            try {
                OutValue = double.Parse(strParameter, CultureInfo.InvariantCulture);
            } catch {
                OutValue = DefaultValue;
            }

            return OutValue;
        }

        // Get a float parameter.
        public float getParameter(string SectionName, string ParameterName, float DefaultValue)
        {
            return (float)((double)getParameter(SectionName, ParameterName, (double)DefaultValue));
        }

        // Get a long parameter.
        public long getParameter(string SectionName, string ParameterName, long DefaultValue)
        {
            string strParameter = getParameter(SectionName, ParameterName, DefaultValue.ToString(CultureInfo.InvariantCulture));
            strParameter = strParameter.Trim();
            
            long OutValue;
            try {
                OutValue = long.Parse(strParameter, CultureInfo.InvariantCulture);
            } catch {
                OutValue = DefaultValue;
            }

            return OutValue;
        }

        // Get an integer parameter.
        public int getParameter(string SectionName, string ParameterName, int DefaultValue)
        {
            return (int)((long)getParameter(SectionName, ParameterName, (long)DefaultValue));
        }

        // Return a sorted snapshot of section names, excluding tables.
        public List<string> listSections()
        {
            List<string> Names = new List<string>(dictSections.Keys);
            Names.Sort(StringComparer.Ordinal);
            return Names;
        }

        // Return a sorted snapshot of parameter names in a section.
        public List<string> listParameters(string Section)
        {
            if (Section == null) {
                throw new ArgumentNullException("Section");
            }
            strDictionary Parameters;
            if (!dictSections.TryGetValue(Section, out Parameters)) {
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
