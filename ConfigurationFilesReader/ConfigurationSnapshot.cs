using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ConfigurationFilesReader
{
    // Owns a private deep copy. No mutable storage is exposed.
    public sealed class ConfigurationSnapshot
    {
        private readonly ConfigurationFile Data;

        internal ConfigurationSnapshot(ConfigurationFile Data) { this.Data = Data; }
        public bool CaseSensitive { get { return Data.CaseSensitive; } }
        public ReadOnlyCollection<string> getTable(string Name) { return Data.getTableReadOnly(Name); }
        public ReadOnlyCollection<string> getList(string Name) { return Data.getListReadOnly(Name); }
        public ReadOnlyStringDictionary getDictionary(string Name) { return Data.getDictionaryReadOnly(Name); }
        public ReadOnlyStringDictionary getSection(string Name)
        {
            Dictionary<string, string> Parameters;
            Data.tryGetSection(Name, out Parameters);
            return new ReadOnlyStringDictionary(Parameters ?? new Dictionary<string, string>());
        }
        public string getText(string Name) { return Data.getText(Name); }
        // Defensive copies preserve snapshot data even if callers change the returned bytes.
        public byte[] getEncoded(string Name) { return Data.getEncoded(Name); }
        public byte[] getEncoded(string Name, bool Encoded) { return Data.getEncoded(Name, Encoded); }
        public ReadOnlyCollection<string> listSections() { return Data.listSections().AsReadOnly(); }
        public ReadOnlyCollection<string> listTables() { return Data.listTables().AsReadOnly(); }
        public ReadOnlyCollection<string> listLists() { return Data.listLists().AsReadOnly(); }
        public ReadOnlyCollection<string> listDictionaries() { return Data.listDictionaries().AsReadOnly(); }
        public ReadOnlyCollection<string> listTexts() { return Data.listTexts().AsReadOnly(); }
        public ReadOnlyCollection<string> listEncoded() { return Data.listEncoded().AsReadOnly(); }
        public ReadOnlyCollection<string> listLoadedFiles() { return Data.listLoadedFiles().AsReadOnly(); }
        public bool containsSection(string Name) { return Data.containsSection(Name); }
        public bool containsTable(string Name) { return Data.containsTable(Name); }
        public bool containsList(string Name) { return Data.containsList(Name); }
        public bool containsDictionary(string Name) { return Data.containsDictionary(Name); }
        public bool containsText(string Name) { return Data.containsText(Name); }
        public bool containsEncoded(string Name) { return Data.containsEncoded(Name); }
        public bool containsParameter(string Section, string Parameter) { return Data.containsParameter(Section, Parameter); }
        public bool containsDictionaryValue(string DictionaryName, string Key) { return Data.containsDictionaryValue(DictionaryName, Key); }
        public ReadOnlyCollection<string> listParameters() { return Data.listParameters().AsReadOnly(); }
        public ReadOnlyCollection<string> listParameters(string Section) { return Data.listParameters(Section).AsReadOnly(); }
        public bool tryGetParameter(string Section, string Parameter, out string Value)
        {
            return Data.tryGetParameter(Section, Parameter, out Value);
        }
        public bool tryGetDictionaryValue(string DictionaryName, string Key, out string Value)
        {
            return Data.tryGetDictionaryValue(DictionaryName, Key, out Value);
        }
        public bool tryGetParameter(string Section, string Parameter, out bool Value)
        {
            return Data.tryGetParameter(Section, Parameter, out Value);
        }
        public bool tryGetDictionaryValue(string DictionaryName, string Key, out bool Value)
        {
            return Data.tryGetDictionaryValue(DictionaryName, Key, out Value);
        }
        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options,
            out bool Value, out DictionaryValueStatus Status)
        {
            return Data.tryGetDictionaryValue(DictionaryName, Key, Options, out Value, out Status);
        }
        public bool tryGetParameter(string Section, string Parameter, out int Value)
        {
            return Data.tryGetParameter(Section, Parameter, out Value);
        }
        public bool tryGetDictionaryValue(string DictionaryName, string Key, out int Value)
        {
            return Data.tryGetDictionaryValue(DictionaryName, Key, out Value);
        }
        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options,
            out int Value, out DictionaryValueStatus Status)
        {
            return Data.tryGetDictionaryValue(DictionaryName, Key, Options, out Value, out Status);
        }
        public bool tryGetParameter(string Section, string Parameter, out long Value)
        {
            return Data.tryGetParameter(Section, Parameter, out Value);
        }
        public bool tryGetDictionaryValue(string DictionaryName, string Key, out long Value)
        {
            return Data.tryGetDictionaryValue(DictionaryName, Key, out Value);
        }
        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options,
            out long Value, out DictionaryValueStatus Status)
        {
            return Data.tryGetDictionaryValue(DictionaryName, Key, Options, out Value, out Status);
        }
        public bool tryGetParameter(string Section, string Parameter, out float Value)
        {
            return Data.tryGetParameter(Section, Parameter, out Value);
        }
        public bool tryGetDictionaryValue(string DictionaryName, string Key, out float Value)
        {
            return Data.tryGetDictionaryValue(DictionaryName, Key, out Value);
        }
        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options,
            out float Value, out DictionaryValueStatus Status)
        {
            return Data.tryGetDictionaryValue(DictionaryName, Key, Options, out Value, out Status);
        }
        public bool tryGetParameter(string Section, string Parameter, out double Value)
        {
            return Data.tryGetParameter(Section, Parameter, out Value);
        }
        public bool tryGetDictionaryValue(string DictionaryName, string Key, out double Value)
        {
            return Data.tryGetDictionaryValue(DictionaryName, Key, out Value);
        }
        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options,
            out double Value, out DictionaryValueStatus Status)
        {
            return Data.tryGetDictionaryValue(DictionaryName, Key, Options, out Value, out Status);
        }
        public bool tryGetSectionSource(string Name, out SourceLocation Location)
        {
            return Data.tryGetSectionSource(Name, out Location);
        }
        public bool tryGetTableSource(string Name, out SourceLocation Location)
        {
            return Data.tryGetTableSource(Name, out Location);
        }
        public bool tryGetListSource(string Name, out SourceLocation Location)
        {
            return Data.tryGetListSource(Name, out Location);
        }
        public bool tryGetDictionarySource(string Name, out SourceLocation Location)
        {
            return Data.tryGetDictionarySource(Name, out Location);
        }
        public bool tryGetTextSource(string Name, out SourceLocation Location)
        {
            return Data.tryGetTextSource(Name, out Location);
        }
        public bool tryGetEncodedSource(string Name, out SourceLocation Location)
        {
            return Data.tryGetEncodedSource(Name, out Location);
        }
        public bool tryGetParameterSource(string Name, string Key, out SourceLocation Location)
        {
            return Data.tryGetParameterSource(Name, Key, out Location);
        }
        public bool tryGetDictionaryValueSource(string Name, string Key, out SourceLocation Location)
        {
            return Data.tryGetDictionaryValueSource(Name, Key, out Location);
        }
        public bool tryGetTableRowSource(string Name, int Index, out SourceLocation Location)
        {
            return Data.tryGetTableRowSource(Name, Index, out Location);
        }
        public bool tryGetListItemSource(string Name, int Index, out SourceLocation Location)
        {
            return Data.tryGetListItemSource(Name, Index, out Location);
        }
    }
}
