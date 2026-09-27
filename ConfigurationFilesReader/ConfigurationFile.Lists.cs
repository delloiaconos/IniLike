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
    }
}
