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
            List<string> Names = new List<string>(dictTables.Keys);
            Names.Sort(StringComparer.Ordinal);
            return Names;
        }

        // Get a table by name.
        public strTable getTable(string tablename)
        {
            if (dictTables.ContainsKey(tablename)) {
                return dictTables[tablename];
            } else {
                return new strTable(0);
            }
        }
    }
}
