using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ConfigurationFilesReader
{
    public partial class ConfigurationFile
    {
        public ReadOnlyCollection<string> getTableReadOnly(string Name)
        {
            return getTable(Name).AsReadOnly();
        }

        public ReadOnlyCollection<string> getListReadOnly(string Name)
        {
            return getList(Name).AsReadOnly();
        }

        public ReadOnlyStringDictionary getDictionaryReadOnly(string Name)
        {
            return new ReadOnlyStringDictionary(getDictionary(Name));
        }

        // Copy data directly: dump/reload would lose nulls and unrepresentable in-memory values.
        public ConfigurationSnapshot createSnapshot()
        {
            ConfigurationFile Copy = new ConfigurationFile(CaseSensitive);
            foreach (KeyValuePair<string, Dictionary<string, string>> Entry in dictSections) {
                Copy.dictSections.Add(Entry.Key, new Dictionary<string, string>(Entry.Value, NameComparer));
            }
            foreach (KeyValuePair<string, Dictionary<string, string>> Entry in dictDictionaries) {
                Copy.dictDictionaries.Add(Entry.Key, new Dictionary<string, string>(Entry.Value, NameComparer));
            }
            foreach (KeyValuePair<string, List<string>> Entry in dictTables) {
                Copy.dictTables.Add(Entry.Key, new List<string>(Entry.Value));
            }
            foreach (KeyValuePair<string, List<string>> Entry in dictLists) {
                Copy.dictLists.Add(Entry.Key, new List<string>(Entry.Value));
            }
            foreach (KeyValuePair<string, List<string>> Entry in dictTexts) {
                Copy.dictTexts.Add(Entry.Key, new List<string>(Entry.Value));
            }
            foreach (KeyValuePair<string, byte[]> Entry in dictEncoded) {
                Copy.dictEncoded.Add(Entry.Key, (byte[])Entry.Value.Clone());
            }
            foreach (string Name in loadedFiles) { Copy.loadedFiles.Add(Name); }
            return new ConfigurationSnapshot(Copy);
        }
    }
}
