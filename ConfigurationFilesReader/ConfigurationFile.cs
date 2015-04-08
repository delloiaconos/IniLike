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
        private enum SectionType { None = 0,Section, Table };

        public char[] parSeparator = { '=' };
        public char[] parEndLineDelimiter = { ';', ',', '.' };

        private string FileName;
        private Dictionary<string, strDictionary> dictSections;
        private Dictionary<string, strTable> dictTables;
        
        public bool UpdateFile = false;
        
        
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

        private bool loadFile()
        {
            if (File.Exists(FileName))
            {
                StreamReader srFile = new StreamReader(FileName);

                string currentParent = "";
                SectionType reading = SectionType.None;


                while (!srFile.EndOfStream)
                {
                    string currentLine = srFile.ReadLine();
                    currentLine = currentLine.Trim();

                    if (currentLine.StartsWith("[TABLE:") && currentLine.EndsWith("]"))
                    {
                        //Ho trovato una TABLE
                        currentParent = currentLine.Substring(7, currentLine.Length - 1 - 7).Trim();
                        reading = SectionType.Table;
                        //Creo la table...
                        dictTables.Add(currentParent, new strTable());
                    }
                    else if (currentLine.StartsWith("[") && currentLine.EndsWith("]"))
                    {
                        // Ho trovato una sezione
                        currentParent = currentLine.Substring(1, currentLine.Length - 1 - 1).Trim();
                        reading = SectionType.Section;
                        // Creo la sezione
                        dictSections.Add(currentParent, new strDictionary());
                    }
                    else if (currentLine.Length > 0 && currentLine.StartsWith("##"))
                    {
                        // Ho trovato un commento tra le righe....
                    }
                    else if (reading == SectionType.Section && currentLine.Length > 0)
                    {
                        // Aggiungo il parametro
                        string[] sline = currentLine.Split(parSeparator);
                        if (sline.Count() == 2)
                        {
                            sline[1] = sline[1].Trim().TrimEnd(parEndLineDelimiter);
                            dictSections[currentParent].Add(sline[0].Trim(), sline[1]);
                        }
                    }
                    else if (reading == SectionType.Table && currentLine.Length > 0)
                    {
                        // Aggiungo il parametro
                        currentLine = currentLine.Trim().TrimEnd(parEndLineDelimiter);
                        dictTables[currentParent].Add(currentLine);
                    }

                }
                srFile.Close();
                return true;
            }
            else
            {
                return false;
            }
            
        }

        

        private void addSection( string SectionName )
        {
            StreamWriter swFile = new StreamWriter(SectionName, true);
            swFile.WriteLine();
            swFile.WriteLine("[" + SectionName + "]");
            swFile.Close();
        }

        public bool checkSection(string SectionName)
        {
            if (!dictSections.ContainsKey(SectionName))
            {
                if (UpdateFile == true)
                {
                    addSection(SectionName);
                }
                return false;
            }
            else
            {
                return true;
            }
        }

        public void addParameter(string SectionName, string ParameterName, string DefaultValue)
        {
            
        }



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
                    if (UpdateFile)
                    {
                        addParameter(SectionName, ParameterName, DefaultValue);
                    }
                    return DefaultValue;
                }
            }
            else
            {
                if (UpdateFile)
                {
                    addParameter(SectionName, ParameterName, DefaultValue);
                }
                return DefaultValue;
            }
        }

        public bool getParameter(string SectionName, string ParameterName, bool DefaultValue)
        {
            string strParameter = getParameter(SectionName, ParameterName, DefaultValue ? "TRUE" : "FALSE");
            strParameter = strParameter.Trim().ToUpper();
            return strParameter.CompareTo("TRUE") == 0 ? true : false;
        }

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

        public float getParameter(string SectionName, string ParameterName, float DefaultValue)
        {
            return (float)((double)getParameter(SectionName, ParameterName, (double)DefaultValue));
        }

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

        public int getParameter(string SectionName, string ParameterName, int DefaultValue)
        {
            return (int)((long)getParameter(SectionName, ParameterName, (long)DefaultValue));
        }

        public strTable getTable(string tablename)
        {
            if (dictTables.ContainsKey(tablename))
                return dictTables[tablename];
            else
                return new strTable(0);
        }
    }
}
