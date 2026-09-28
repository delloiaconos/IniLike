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

        // Return an independent snapshot of the rows, or an empty list when missing.
        public strTable getTable(string tablename)
        {
            strTable Rows;
            return dictTables.TryGetValue(tablename, out Rows) ? new strTable(Rows) : new strTable();
        }
    }
}
