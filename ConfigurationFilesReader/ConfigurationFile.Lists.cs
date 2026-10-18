using System;
using System.Collections.Generic;

namespace ConfigurationFilesReader
{
    public partial class ConfigurationFile
    {
        private Dictionary<string, List<string>> dictLists;

        // Return a sorted snapshot of list names, excluding sections and tables.
        public List<string> listLists()
        {
            List<string> Names = new List<string>(dictLists.Keys);
            Names.Sort(StringComparer.Ordinal);
            return Names;
        }

        // Return the live list, or an unattached empty list when missing.
        public List<string> getList(string ListName)
        {
            if (ListName == null) {
                throw new ArgumentNullException("ListName");
            }
            List<string> Items;
            return dictLists.TryGetValue(ListName, out Items) ? Items : new List<string>();
        }

        // Presence checks never create entries or write files.
        public bool containsList(string Name)
        {
            if (Name == null) { throw new ArgumentNullException("Name"); }
            return dictLists.ContainsKey(Name);
        }

        // A missing block returns false and a null output; empty blocks return true.
        public bool tryGetList(string Name, out List<string> Items)
        {
            if (Name == null) { throw new ArgumentNullException("Name"); }
            Items = null;
            List<string> Stored;
            if (!dictLists.TryGetValue(Name, out Stored)) { return false; }
            Items = Stored;
            return true;
        }
    }
}
