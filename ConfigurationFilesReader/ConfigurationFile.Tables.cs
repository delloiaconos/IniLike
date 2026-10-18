using System;
using System.Collections.Generic;

namespace ConfigurationFilesReader
{
    using strTable = List<string>;

    public partial class ConfigurationFile
    {
        private Dictionary<string, strTable> dictTables;

        // Return a sorted snapshot of table names, excluding sections.
        public List<string> listTables()
        {
            return sortedNames(dictTables.Keys);
        }

        // Return the live rows, or an unattached empty list when missing.
        public strTable getTable(string tablename)
        {
            if (tablename == null) {
                throw new ArgumentNullException("tablename");
            }
            strTable Rows;
            return tryGetTable(tablename, out Rows) ? Rows : new strTable();
        }

        // Presence checks never create entries or write files.
        public bool containsTable(string Name)
        {
            if (Name == null) { throw new ArgumentNullException("Name"); }
            return dictTables.ContainsKey(Name);
        }

        // A missing block returns false and a null output; empty blocks return true.
        public bool tryGetTable(string Name, out List<string> Rows)
        {
            if (Name == null) { throw new ArgumentNullException("Name"); }
            return dictTables.TryGetValue(Name, out Rows);
        }
    }
}
