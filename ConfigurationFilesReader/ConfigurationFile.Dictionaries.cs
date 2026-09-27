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
            List<string> Names = new List<string>(dictDictionaries.Keys);
            Names.Sort(StringComparer.Ordinal);
            return Names;
        }

        // Return the live dictionary, or an unattached empty dictionary when missing.
        public Dictionary<string, string> getDictionary(string DictionaryName)
        {
            if (DictionaryName == null) {
                throw new ArgumentNullException("DictionaryName");
            }
            strDictionary Entries;
            return dictDictionaries.TryGetValue(DictionaryName, out Entries) ? Entries : new strDictionary();
        }
    }
}
