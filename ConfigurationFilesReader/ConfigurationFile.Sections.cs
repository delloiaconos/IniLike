using System;
using System.Collections.Generic;
using System.Globalization;

namespace ConfigurationFilesReader
{
    using strDictionary = Dictionary<string, string>;

    public partial class ConfigurationFile
    {
        private Dictionary<string, strDictionary> dictSections;

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
                Parameters = new strDictionary(NameComparer);
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
            HashSet<string> UniqueNames = new HashSet<string>(NameComparer);
            foreach (strDictionary Parameters in dictSections.Values)
                foreach (string Name in Parameters.Keys)
                    UniqueNames.Add(Name);
            List<string> Names = new List<string>(UniqueNames);
            Names.Sort(StringComparer.Ordinal);
            return Names;
        }

        // Presence checks never create entries or write files.
        public bool containsSection(string Name)
        {
            if (Name == null) { throw new ArgumentNullException("Name"); }
            return dictSections.ContainsKey(Name);
        }

        // A missing block returns false and a null output; empty blocks return true.
        public bool tryGetSection(string Name, out Dictionary<string, string> Parameters)
        {
            if (Name == null) { throw new ArgumentNullException("Name"); }
            Parameters = null;
            Dictionary<string, string> Stored;
            if (!dictSections.TryGetValue(Name, out Stored)) { return false; }
            Parameters = new Dictionary<string, string>(Stored, NameComparer);
            return true;
        }

        public bool containsParameter(string Section, string Parameter)
        {
            string Value;
            return tryGetParameter(Section, Parameter, out Value);
        }

        // Presence is independent of value: a stored null still returns true.
        public bool tryGetParameter(string Section, string Parameter, out string Value)
        {
            if (Section == null) { throw new ArgumentNullException("Section"); }
            if (Parameter == null) { throw new ArgumentNullException("Parameter"); }
            Value = null;
            strDictionary Parameters;
            return dictSections.TryGetValue(Section, out Parameters) &&
                Parameters.TryGetValue(Parameter, out Value);
        }

        public bool tryGetParameter(string Section, string Parameter, out bool Value)
        {
            Value = false;
            string Text;
            if (!tryGetParameter(Section, Parameter, out Text) || Text == null) { return false; }
            Text = Text.Trim();
            if (Text == "1" || String.Equals(Text, "true", StringComparison.OrdinalIgnoreCase)) {
                Value = true;
                return true;
            }
            return Text == "0" || String.Equals(Text, "false", StringComparison.OrdinalIgnoreCase);
        }

        // Typed reads reject invalid, null and out-of-range values without mutation.
        public bool tryGetParameter(string Section, string Parameter, out int Value)
        {
            Value = 0;
            string Text;
            if (!tryGetParameter(Section, Parameter, out Text) || Text == null) { return false; }
            int Parsed;
            if (!int.TryParse(Text.Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out Parsed)) { return false; }
            Value = (int)Parsed;
            return true;
        }

        // Typed reads reject invalid, null and out-of-range values without mutation.
        public bool tryGetParameter(string Section, string Parameter, out long Value)
        {
            Value = 0;
            string Text;
            if (!tryGetParameter(Section, Parameter, out Text) || Text == null) { return false; }
            long Parsed;
            if (!long.TryParse(Text.Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out Parsed)) { return false; }
            Value = (long)Parsed;
            return true;
        }

        // Typed reads reject invalid, null and out-of-range values without mutation.
        public bool tryGetParameter(string Section, string Parameter, out double Value)
        {
            Value = 0;
            string Text;
            if (!tryGetParameter(Section, Parameter, out Text) || Text == null) { return false; }
            double Parsed;
            if (!double.TryParse(Text.Trim().Replace(',', '.'), NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out Parsed) || Double.IsNaN(Parsed) || Double.IsInfinity(Parsed)) { return false; }
            Value = (double)Parsed;
            return true;
        }

        // Typed reads reject invalid, null and out-of-range values without mutation.
        public bool tryGetParameter(string Section, string Parameter, out float Value)
        {
            Value = 0;
            string Text;
            if (!tryGetParameter(Section, Parameter, out Text) || Text == null) { return false; }
            double Parsed;
            if (!double.TryParse(Text.Trim().Replace(',', '.'), NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out Parsed) || Double.IsNaN(Parsed) || Double.IsInfinity(Parsed) || Parsed > float.MaxValue || Parsed < -float.MaxValue) { return false; }
            Value = (float)Parsed;
            return true;
        }
    }
}
