using System;
using System.Collections.Generic;

namespace ConfigurationFilesReader
{
    public partial class ConfigurationFile
    {
        private Dictionary<string, List<string>> dictTexts;

        // Return a sorted snapshot of text block names.
        public List<string> listTexts()
        {
            List<string> Names = new List<string>(dictTexts.Keys);
            Names.Sort(StringComparer.Ordinal);
            return Names;
        }

        // Join raw lines with LF; do not create missing blocks or save.
        public string getText(string TextName)
        {
            if (TextName == null) {
                throw new ArgumentNullException("TextName");
            }
            List<string> Lines;
            return dictTexts.TryGetValue(TextName, out Lines) ? String.Join("\n", Lines.ToArray()) : "";
        }
    }
}
