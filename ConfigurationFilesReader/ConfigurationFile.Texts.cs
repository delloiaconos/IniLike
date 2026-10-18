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

        // Presence checks never create entries or write files.
        public bool containsText(string Name)
        {
            if (Name == null) { throw new ArgumentNullException("Name"); }
            return dictTexts.ContainsKey(Name);
        }

        // A missing block returns false and a null output; empty blocks return true.
        public bool tryGetText(string Name, out string Text)
        {
            if (Name == null) { throw new ArgumentNullException("Name"); }
            Text = null;
            List<string> Stored;
            if (!dictTexts.TryGetValue(Name, out Stored)) { return false; }
            Text = String.Join("\n", Stored.ToArray());
            return true;
        }
    }
}
