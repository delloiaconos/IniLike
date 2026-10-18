using System;
using System.Collections.Generic;
using System.Globalization;

namespace ConfigurationFilesReader
{
    using strDictionary = Dictionary<string, string>;

    public partial class ConfigurationFile
    {
        private Dictionary<string, strDictionary> dictDictionaries;

        // Return a sorted snapshot of dictionary names.
        public List<string> listDictionaries()
        {
            return sortedNames(dictDictionaries.Keys);
        }

        // Return the live dictionary, or an unattached empty dictionary when missing.
        public Dictionary<string, string> getDictionary(string DictionaryName)
        {
            if (DictionaryName == null) {
                throw new ArgumentNullException("DictionaryName");
            }
            strDictionary Entries;
            return tryGetDictionary(DictionaryName, out Entries) ? Entries : new strDictionary(NameComparer);
        }

        // Presence checks never create entries or write files.
        public bool containsDictionary(string Name)
        {
            if (Name == null) { throw new ArgumentNullException("Name"); }
            return dictDictionaries.ContainsKey(Name);
        }

        // A missing block returns false and a null output; empty blocks return true.
        public bool tryGetDictionary(string Name, out Dictionary<string, string> Entries)
        {
            if (Name == null) { throw new ArgumentNullException("Name"); }
            return dictDictionaries.TryGetValue(Name, out Entries);
        }

        public bool containsDictionaryValue(string DictionaryName, string Key)
        {
            string Value;
            return tryGetDictionaryValue(DictionaryName, Key, out Value);
        }

        // Raw reads preserve null and empty strings and distinguish presence from conversion.
        public bool tryGetDictionaryValue(string DictionaryName, string Key, out string Value)
        {
            DictionaryValueStatus Status;
            return tryGetDictionaryText(DictionaryName, Key, out Value, out Status);
        }

        private bool tryGetDictionaryText(string DictionaryName, string Key, out string Value, out DictionaryValueStatus Status)
        {
            Value = null;
            Status = DictionaryValueStatus.MissingDictionary;
            if (DictionaryName == null) { throw new ArgumentNullException("DictionaryName"); }
            if (Key == null) { throw new ArgumentNullException("Key"); }
            strDictionary Entries;
            if (!dictDictionaries.TryGetValue(DictionaryName, out Entries)) { return false; }
            Status = DictionaryValueStatus.MissingKey;
            if (!Entries.TryGetValue(Key, out Value)) { return false; }
            Status = Value == null ? DictionaryValueStatus.NullValue : DictionaryValueStatus.Success;
            return true;
        }

        private static DictionaryConversionOptions dictionaryOptions(DictionaryConversionOptions Options)
        {
            if (Options == null) { return new DictionaryConversionOptions(); }
            if (Options.NumericCulture == null) { throw new ArgumentNullException("NumericCulture"); }
            if (!Enum.IsDefined(typeof(DecimalSeparatorPolicy), Options.DecimalSeparator)) {
                throw new ArgumentException("Unknown decimal separator policy.", "Options");
            }
            if ((Options.BooleanValues & ~(BooleanRepresentations.TrueFalse | BooleanRepresentations.OneZero |
                BooleanRepresentations.YesNo | BooleanRepresentations.OnOff)) != 0) {
                throw new ArgumentException("Unknown boolean representation.", "Options");
            }
            return Options;
        }

        private bool tryGetDictionaryTypedText(string DictionaryName, string Key, out string Text, out DictionaryValueStatus Status)
        {
            return tryGetDictionaryText(DictionaryName, Key, out Text, out Status) && Text != null;
        }

        private static bool tryConvertDictionaryBoolean(string Text, DictionaryConversionOptions Options, out bool Value)
        {
            Value = false;
            Text = Text.Trim();
            string[] TrueValues = { "true", "1", "yes", "on" };
            string[] FalseValues = { "false", "0", "no", "off" };
            for (int Index = 0; Index < TrueValues.Length; Index++) {
                if (((int)Options.BooleanValues & (1 << Index)) == 0) { continue; }
                if (String.Equals(Text, TrueValues[Index], StringComparison.OrdinalIgnoreCase)) { Value = true; return true; }
                if (String.Equals(Text, FalseValues[Index], StringComparison.OrdinalIgnoreCase)) { return true; }
            }
            return false;
        }

        private static bool tryConvertDictionaryDouble(string Text, DictionaryConversionOptions Options, out double Value)
        {
            Value = 0;
            Text = Text.Trim();
            NumberFormatInfo Format = (NumberFormatInfo)Options.NumericCulture.NumberFormat.Clone();
            switch (Options.DecimalSeparator) {
                case DecimalSeparatorPolicy.Dot: Format.NumberDecimalSeparator = "."; break;
                case DecimalSeparatorPolicy.Comma: Format.NumberDecimalSeparator = ","; break;
                case DecimalSeparatorPolicy.DotOrComma:
                    if (Text.IndexOf('.') >= 0 && Text.IndexOf(',') >= 0) { return false; }
                    Format.NumberDecimalSeparator = ".";
                    Text = Text.Replace(',', '.');
                    break;
            }
            double Parsed;
            if (!Double.TryParse(Text, NumberStyles.Float, Format, out Parsed)) { return false; }
            if (Double.IsNaN(Parsed) || Double.IsInfinity(Parsed)) {
                if (!Options.AllowNonFinite) { return false; }
                // Accept explicit symbols only, never overflow that a runtime maps to infinity.
                if (!String.Equals(Text, Format.NaNSymbol, StringComparison.OrdinalIgnoreCase) &&
                    !String.Equals(Text, Format.PositiveInfinitySymbol, StringComparison.OrdinalIgnoreCase) &&
                    !String.Equals(Text, Format.NegativeInfinitySymbol, StringComparison.OrdinalIgnoreCase)) { return false; }
            }
            Value = Parsed;
            return true;
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, out bool Value)
        {
            return tryGetDictionaryValue(DictionaryName, Key, null, out Value);
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options, out bool Value)
        {
            DictionaryValueStatus Status;
            return tryGetDictionaryValue(DictionaryName, Key, Options, out Value, out Status);
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options,
            out bool Value, out DictionaryValueStatus Status)
        {
            Value = false;
            Options = dictionaryOptions(Options);
            string Text;
            if (!tryGetDictionaryTypedText(DictionaryName, Key, out Text, out Status)) { return false; }
            Status = DictionaryValueStatus.InvalidValue;
            bool Parsed;
            if (!tryConvertDictionaryBoolean(Text, Options, out Parsed)) { return false; }
            Value = Parsed;
            Status = DictionaryValueStatus.Success;
            return true;
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, out int Value)
        {
            return tryGetDictionaryValue(DictionaryName, Key, null, out Value);
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options, out int Value)
        {
            DictionaryValueStatus Status;
            return tryGetDictionaryValue(DictionaryName, Key, Options, out Value, out Status);
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options,
            out int Value, out DictionaryValueStatus Status)
        {
            Value = 0;
            Options = dictionaryOptions(Options);
            string Text;
            if (!tryGetDictionaryTypedText(DictionaryName, Key, out Text, out Status)) { return false; }
            Status = DictionaryValueStatus.InvalidValue;
            int Parsed;
            if (!int.TryParse(Text.Trim(), NumberStyles.Integer, Options.NumericCulture, out Parsed)) { return false; }
            Value = Parsed;
            Status = DictionaryValueStatus.Success;
            return true;
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, out long Value)
        {
            return tryGetDictionaryValue(DictionaryName, Key, null, out Value);
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options, out long Value)
        {
            DictionaryValueStatus Status;
            return tryGetDictionaryValue(DictionaryName, Key, Options, out Value, out Status);
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options,
            out long Value, out DictionaryValueStatus Status)
        {
            Value = 0;
            Options = dictionaryOptions(Options);
            string Text;
            if (!tryGetDictionaryTypedText(DictionaryName, Key, out Text, out Status)) { return false; }
            Status = DictionaryValueStatus.InvalidValue;
            long Parsed;
            if (!long.TryParse(Text.Trim(), NumberStyles.Integer, Options.NumericCulture, out Parsed)) { return false; }
            Value = Parsed;
            Status = DictionaryValueStatus.Success;
            return true;
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, out double Value)
        {
            return tryGetDictionaryValue(DictionaryName, Key, null, out Value);
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options, out double Value)
        {
            DictionaryValueStatus Status;
            return tryGetDictionaryValue(DictionaryName, Key, Options, out Value, out Status);
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options,
            out double Value, out DictionaryValueStatus Status)
        {
            Value = 0;
            Options = dictionaryOptions(Options);
            string Text;
            if (!tryGetDictionaryTypedText(DictionaryName, Key, out Text, out Status)) { return false; }
            Status = DictionaryValueStatus.InvalidValue;
            double Parsed;
            if (!tryConvertDictionaryDouble(Text, Options, out Parsed)) { return false; }
            Value = Parsed;
            Status = DictionaryValueStatus.Success;
            return true;
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, out float Value)
        {
            return tryGetDictionaryValue(DictionaryName, Key, null, out Value);
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options, out float Value)
        {
            DictionaryValueStatus Status;
            return tryGetDictionaryValue(DictionaryName, Key, Options, out Value, out Status);
        }

        public bool tryGetDictionaryValue(string DictionaryName, string Key, DictionaryConversionOptions Options,
            out float Value, out DictionaryValueStatus Status)
        {
            Value = 0;
            Options = dictionaryOptions(Options);
            string Text;
            if (!tryGetDictionaryTypedText(DictionaryName, Key, out Text, out Status)) { return false; }
            Status = DictionaryValueStatus.InvalidValue;
            double Parsed;
            if (!tryConvertDictionaryDouble(Text, Options, out Parsed)) { return false; }
            if (!Double.IsNaN(Parsed) && !Double.IsInfinity(Parsed) &&
                (Parsed > Single.MaxValue || Parsed < -Single.MaxValue)) { return false; }
            Value = (float)Parsed;
            Status = DictionaryValueStatus.Success;
            return true;
        }

    }
}
