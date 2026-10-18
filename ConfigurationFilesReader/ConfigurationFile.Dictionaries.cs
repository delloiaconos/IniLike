using System;
using System.Collections.Generic;

namespace ConfigurationFilesReader
{
    using strDictionary = Dictionary<string, string>;

    public partial class ConfigurationFile
    {
        private Dictionary<string, strDictionary> dictDictionaries;

        // Return a sorted snapshot of dictionary names.
        public List<string> listDictionaries()
        {
            return sortedNames(dictDictionaries.Keys);
        }

        // Return the live dictionary, or an unattached empty dictionary when missing.
        public Dictionary<string, string> getDictionary(string DictionaryName)
        {
            if (DictionaryName == null) {
                throw new ArgumentNullException("DictionaryName");
            }
            strDictionary Entries;
            return tryGetDictionary(DictionaryName, out Entries) ? Entries : new strDictionary(NameComparer);
        }

        // Presence checks never create entries or write files.
        public bool containsDictionary(string Name)
        {
            if (Name == null) { throw new ArgumentNullException("Name"); }
            return dictDictionaries.ContainsKey(Name);
        }

        // A missing block returns false and a null output; empty blocks return true.
        public bool tryGetDictionary(string Name, out Dictionary<string, string> Entries)
        {
            if (Name == null) { throw new ArgumentNullException("Name"); }
            return dictDictionaries.TryGetValue(Name, out Entries);
        }
    }
}
