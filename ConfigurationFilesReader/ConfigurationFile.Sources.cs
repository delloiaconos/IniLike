using System;
using System.Collections.Generic;

namespace ConfigurationFilesReader
{
    public partial class ConfigurationFile
    {
        private sealed class ParsedBlockSource
        {
            public SourceLocation Location;
            public readonly Dictionary<string, SourceLocation> Entries;
            public readonly List<string> Rows = new List<string>();
            public readonly List<SourceLocation> RowLocations = new List<SourceLocation>();

            public ParsedBlockSource(SourceLocation Location, StringComparer Comparer)
            {
                this.Location = Location;
                Entries = new Dictionary<string, SourceLocation>(Comparer);
            }
        }

        private readonly Dictionary<BlockTypes, Dictionary<string, ParsedBlockSource>> sourceBlocks =
            new Dictionary<BlockTypes, Dictionary<string, ParsedBlockSource>>();

        private void recordBlockSource(BlockTypes Type, string Name, string SourcePath, string SourceName, int LineNumber)
        {
            Dictionary<string, ParsedBlockSource> Blocks;
            if (!sourceBlocks.TryGetValue(Type, out Blocks)) {
                Blocks = new Dictionary<string, ParsedBlockSource>(NameComparer);
                sourceBlocks.Add(Type, Blocks);
            }
            Blocks.Add(Name, new ParsedBlockSource(new SourceLocation(SourceName, SourcePath, LineNumber), NameComparer));
        }

        private bool tryGetBlockSource(BlockTypes Type, string Name, out SourceLocation Location)
        {
            Location = null;
            if (Name == null) { throw new ArgumentNullException("Name"); }
            Dictionary<string, ParsedBlockSource> Blocks;
            ParsedBlockSource Block;
            if (!sourceBlocks.TryGetValue(Type, out Blocks) || !Blocks.TryGetValue(Name, out Block)) { return false; }
            Location = Block.Location;
            return true;
        }

        private bool tryGetEntrySource(BlockTypes Type, string Name, string Key, out SourceLocation Location)
        {
            Location = null;
            if (Name == null) { throw new ArgumentNullException("Name"); }
            if (Key == null) { throw new ArgumentNullException("Key"); }
            Dictionary<string, Dictionary<string, string>> Registry = Type == BlockTypes.Section ? dictSections : dictDictionaries;
            Dictionary<string, string> Entries;
            Dictionary<string, ParsedBlockSource> Blocks;
            ParsedBlockSource Block;
            return Registry.TryGetValue(Name, out Entries) && Entries.ContainsKey(Key) &&
                sourceBlocks.TryGetValue(Type, out Blocks) && Blocks.TryGetValue(Name, out Block) &&
                Block.Entries.TryGetValue(Key, out Location);
        }

        private bool tryGetRowSource(BlockTypes Type, string Name, int Index, out SourceLocation Location)
        {
            Location = null;
            if (Name == null) { throw new ArgumentNullException("Name"); }
            Dictionary<string, List<string>> Registry = Type == BlockTypes.Table ? dictTables : dictLists;
            List<string> Rows;
            Dictionary<string, ParsedBlockSource> Blocks;
            ParsedBlockSource Block;
            if (Index < 0 || !Registry.TryGetValue(Name, out Rows) || Index >= Rows.Count ||
                !sourceBlocks.TryGetValue(Type, out Blocks) || !Blocks.TryGetValue(Name, out Block) ||
                Index >= Block.Rows.Count || !Object.ReferenceEquals(Rows[Index], Block.Rows[Index])) { return false; }
            Location = Block.RowLocations[Index];
            return true;
        }

        private void copySourcesTo(ConfigurationFile Copy)
        {
            foreach (KeyValuePair<BlockTypes, Dictionary<string, ParsedBlockSource>> Kind in sourceBlocks) {
                Dictionary<string, ParsedBlockSource> Blocks = new Dictionary<string, ParsedBlockSource>(NameComparer);
                Copy.sourceBlocks.Add(Kind.Key, Blocks);
                foreach (KeyValuePair<string, ParsedBlockSource> Entry in Kind.Value) {
                    ParsedBlockSource Block = new ParsedBlockSource(Entry.Value.Location, NameComparer);
                    foreach (KeyValuePair<string, SourceLocation> Parameter in Entry.Value.Entries) {
                        Block.Entries.Add(Parameter.Key, Parameter.Value);
                    }
                    Block.Rows.AddRange(Entry.Value.Rows);
                    Block.RowLocations.AddRange(Entry.Value.RowLocations);
                    Blocks.Add(Entry.Key, Block);
                }
            }
        }

        public bool tryGetSectionSource(string Name, out SourceLocation Location)
        {
            return tryGetBlockSource(BlockTypes.Section, Name, out Location);
        }

        public bool tryGetTableSource(string Name, out SourceLocation Location)
        {
            return tryGetBlockSource(BlockTypes.Table, Name, out Location);
        }

        public bool tryGetListSource(string Name, out SourceLocation Location)
        {
            return tryGetBlockSource(BlockTypes.List, Name, out Location);
        }

        public bool tryGetDictionarySource(string Name, out SourceLocation Location)
        {
            return tryGetBlockSource(BlockTypes.Dictionary, Name, out Location);
        }

        public bool tryGetTextSource(string Name, out SourceLocation Location)
        {
            return tryGetBlockSource(BlockTypes.Text, Name, out Location);
        }

        public bool tryGetEncodedSource(string Name, out SourceLocation Location)
        {
            return tryGetBlockSource(BlockTypes.Encoded, Name, out Location);
        }

        public bool tryGetParameterSource(string Name, string Key, out SourceLocation Location)
        {
            return tryGetEntrySource(BlockTypes.Section, Name, Key, out Location);
        }

        public bool tryGetDictionaryValueSource(string Name, string Key, out SourceLocation Location)
        {
            return tryGetEntrySource(BlockTypes.Dictionary, Name, Key, out Location);
        }

        public bool tryGetTableRowSource(string Name, int Index, out SourceLocation Location)
        {
            return tryGetRowSource(BlockTypes.Table, Name, Index, out Location);
        }

        public bool tryGetListItemSource(string Name, int Index, out SourceLocation Location)
        {
            return tryGetRowSource(BlockTypes.List, Name, Index, out Location);
        }

    }
}
