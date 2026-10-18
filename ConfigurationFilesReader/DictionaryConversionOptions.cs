using System;
using System.Globalization;

namespace ConfigurationFilesReader
{
    public enum DictionaryValueStatus { Success, MissingDictionary, MissingKey, NullValue, InvalidValue }
    public enum DecimalSeparatorPolicy { Culture, Dot, Comma, DotOrComma }

    [Flags]
    public enum BooleanRepresentations { None = 0, TrueFalse = 1, OneZero = 2, YesNo = 4, OnOff = 8 }

    public sealed class DictionaryConversionOptions
    {
        public CultureInfo NumericCulture { get; set; }
        public DecimalSeparatorPolicy DecimalSeparator { get; set; }
        public BooleanRepresentations BooleanValues { get; set; }
        public bool AllowNonFinite { get; set; }

        public DictionaryConversionOptions()
        {
            NumericCulture = CultureInfo.InvariantCulture;
            DecimalSeparator = DecimalSeparatorPolicy.DotOrComma;
            BooleanValues = BooleanRepresentations.TrueFalse | BooleanRepresentations.OneZero;
        }
    }
}
