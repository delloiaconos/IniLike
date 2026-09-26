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
        private const enum SectionType { None = 0, Section, Table };

        public readonly char[] parSeparator = { '=' };
        public readonly char[] parEndLineDelimiter = { ';', ',', '.' };
        public readonly bool autoUpdateFile = false;

        private string FileName;
        private Dictionary<string, strDictionary> dictSections;
        private Dictionary<string, strTable> dictTables;
        
        public ConfigurationFile(string filename)
        {
            FileName = filename;
            dictSections = new Dictionary<string, strDictionary>();
            dictTables = new Dictionary<string, strTable>();
            if (!loadFile())
                throw new System.Exception("Impossibile aprire il file: '" + FileName + "'.");
        }

        public ConfigurationFile()
        {
            FileName = "";
            dictSections = new Dictionary<string, strDictionary>();
            dictTables = new Dictionary<string, strTable>();
            
        }

        // Load the configuration file content.
        private bool loadFile()
        {
            if (File.Exists(FileName))
            {
                using (StreamReader srFile = new StreamReader(FileName))
                {

                    string currentParent = "";
                    SectionType reading = SectionType.None;


                    while (!srFile.EndOfStream)
                    {
                        string currentLine = srFile.ReadLine();
                        currentLine = currentLine.Trim();

                        if (currentLine.StartsWith("[TABLE:") && currentLine.EndsWith("]"))
                        {
                            currentParent = currentLine.Substring(7, currentLine.Length - 1 - 7).Trim();
                            reading = SectionType.Table;
                            dictTables.Add(currentParent, new strTable());
                        }
                        else if (currentLine.StartsWith("[") && currentLine.EndsWith("]"))
                        {
                            currentParent = currentLine.Substring(1, currentLine.Length - 1 - 1).Trim();
                            reading = SectionType.Section;
                            dictSections.Add(currentParent, new strDictionary());
                        }
                        else if (currentLine.Length > 0 && currentLine.StartsWith("##"))
                        {
                        }
                        else if (reading == SectionType.Section && currentLine.Length > 0)
                        {
                            string[] sline = currentLine.Split(parSeparator);
                            if (sline.Count() == 2)
                            {
                                sline[1] = sline[1].Trim().TrimEnd(parEndLineDelimiter);
                                dictSections[currentParent].Add(sline[0].Trim(), sline[1]);
                            }
                        }
                        else if (reading == SectionType.Table && currentLine.Length > 0)
                        {
                            currentLine = currentLine.Trim().TrimEnd(parEndLineDelimiter);
                            dictTables[currentParent].Add(currentLine);
                        }

                    }
                }
                return true;
            }
            else
            {
                return false;
            }
            
        }

        

        // Check whether the section exists.
        public bool checkSection(string SectionName)
        {
            return dictSections.ContainsKey(SectionName);
        }

        // Set a parameter in memory only.
        public void setParameter(string sectionName, string parameterName, string value)
        {
            if (!dictSections.ContainsKey(sectionName))
                dictSections.Add(sectionName, new strDictionary());
            dictSections[sectionName][parameterName] = value;
        }

        // Add a default parameter in memory without replacing an existing value.
        public void addParameter(string SectionName, string ParameterName, string DefaultValue)
        {
            if (SectionName == null)
                throw new ArgumentNullException("SectionName");
            if (ParameterName == null)
                throw new ArgumentNullException("ParameterName");

            strDictionary parameters;
            if (!dictSections.TryGetValue(SectionName, out parameters))
            {
                parameters = new strDictionary();
                dictSections.Add(SectionName, parameters);
            }
            if (!parameters.ContainsKey(ParameterName))
                parameters.Add(ParameterName, DefaultValue);
        }



        // Get a parameter value or use the default when missing.
        public string getParameter(string SectionName, string ParameterName, string DefaultValue )
        {
            if (checkSection(SectionName))
            {
                strDictionary sectParameters = dictSections[SectionName];
                if (sectParameters.ContainsKey(ParameterName))
                {
                    return sectParameters[ParameterName];
                }
                else
                {
                    if (autoUpdateFile)
                    {
                        addParameter(SectionName, ParameterName, DefaultValue);
                    }
                    return DefaultValue;
                }
            }
            else
            {
                if (autoUpdateFile)
                {
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
            try
            {
                OutValue = double.Parse(strParameter, CultureInfo.InvariantCulture);
            }
            catch
            {
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
            try
            {
                OutValue = long.Parse(strParameter, CultureInfo.InvariantCulture);
            }
            catch
            {
                OutValue = DefaultValue;
            }

            return OutValue;
        }

        // Get an integer parameter.
        public int getParameter(string SectionName, string ParameterName, int DefaultValue)
        {
            return (int)((long)getParameter(SectionName, ParameterName, (long)DefaultValue));
        }

        // Get a table by name.
        public strTable getTable(string tablename)
        {
            if (dictTables.ContainsKey(tablename))
                return dictTables[tablename];
            else
                return new strTable(0);
        }
    }
}
