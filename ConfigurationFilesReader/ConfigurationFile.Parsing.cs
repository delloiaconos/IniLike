using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConfigurationFilesReader
{
    using strDictionary = Dictionary<string, string>;
    using strTable = List<string>;

    public partial class ConfigurationFile
    {
        private enum SectionType { None = 0, Section, Table, List, Dictionary };

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

                        if (currentLine == "[END]" ||
                            (currentLine.StartsWith("[END:", StringComparison.Ordinal) && currentLine.EndsWith("]", StringComparison.Ordinal))) {
                            if (reading != SectionType.None && currentLine != "[END]") {
                                string BlockName = currentLine.Substring(5, currentLine.Length - 6).Trim();
                                if (!String.Equals(BlockName, currentParent, StringComparison.Ordinal)) {
                                    throw new FormatException("End label does not match the current block: '" + currentParent + "'.");
                                }
                            }
                            currentParent = "";
                            reading = SectionType.None;
                        } else if ((currentLine.StartsWith("[TABLE:", StringComparison.Ordinal) ||
                                    currentLine.StartsWith("[TBL:", StringComparison.Ordinal)) && currentLine.EndsWith("]", StringComparison.Ordinal)) {
                            int PrefixLength = currentLine.StartsWith("[TABLE:", StringComparison.Ordinal) ? 7 : 5;
                            currentParent = currentLine.Substring(PrefixLength, currentLine.Length - PrefixLength - 1).Trim();
                            reading = SectionType.Table;
                            dictTables.Add(currentParent, new strTable());
                        } else if ((currentLine.StartsWith("[LIST:", StringComparison.Ordinal) ||
                                    currentLine.StartsWith("[LST:", StringComparison.Ordinal)) && currentLine.EndsWith("]", StringComparison.Ordinal)) {
                            int PrefixLength = currentLine.StartsWith("[LIST:", StringComparison.Ordinal) ? 6 : 5;
                            currentParent = currentLine.Substring(PrefixLength, currentLine.Length - PrefixLength - 1).Trim();
                            reading = SectionType.List;
                            dictLists.Add(currentParent, new List<string>());
                        } else if ((currentLine.StartsWith("[DICT:", StringComparison.Ordinal) ||
                                    currentLine.StartsWith("[DICTIONARY:", StringComparison.Ordinal)) && currentLine.EndsWith("]", StringComparison.Ordinal)) {
                            int PrefixLength = currentLine.StartsWith("[DICT:", StringComparison.Ordinal) ? 6 : 12;
                            currentParent = currentLine.Substring(PrefixLength, currentLine.Length - PrefixLength - 1).Trim();
                            reading = SectionType.Dictionary;
                            dictDictionaries.Add(currentParent, new strDictionary());
                        } else if (currentLine.StartsWith("[") && currentLine.EndsWith("]")) {
                            int PrefixLength = 1;
                            if (currentLine.StartsWith("[SECTION:", StringComparison.Ordinal)) {
                                PrefixLength = 9;
                            } else if (currentLine.StartsWith("[SEC:", StringComparison.Ordinal)) {
                                PrefixLength = 5;
                            }
                            currentParent = currentLine.Substring(PrefixLength, currentLine.Length - PrefixLength - 1).Trim();
                            reading = SectionType.Section;
                            dictSections.Add(currentParent, new strDictionary());
                        } else if ((reading == SectionType.Section || reading == SectionType.Dictionary) && currentLine.Length > 0) {
                            string[] sline = currentLine.Split(parSeparator);
                            if (sline.Count() == 2) {
                                sline[1] = sline[1].Trim().TrimEnd(parEndLineDelimiter);
                                strDictionary Entries = reading == SectionType.Section ? dictSections[currentParent] : dictDictionaries[currentParent];
                                Entries.Add(sline[0].Trim(), sline[1]);
                            }
                        } else if (reading == SectionType.Table && currentLine.Length > 0) {
                            currentLine = currentLine.Trim().TrimEnd(parEndLineDelimiter);
                            dictTables[currentParent].Add(currentLine);
                        } else if (reading == SectionType.List && currentLine.Length > 0) {
                            dictLists[currentParent].Add(currentLine.TrimEnd(parEndLineDelimiter));
                        }
                    }
                }
                return true;
            } else {
                return false;
            }
            
        }
    }
}
