using System;
using System.Collections.Generic;
using System.IO;

namespace ConfigurationFilesReader
{
    using strDictionary = Dictionary<string, string>;
    using strTable = List<string>;

    public partial class ConfigurationFile
    {
        private enum SectionType { None = 0, Section, Table, List, Dictionary };

        private readonly Dictionary<SectionType, List<string>> sectionIdentifier = new Dictionary<SectionType, List<string>> {
            { SectionType.None, new List<string> { "END" } },
            { SectionType.Section, new List<string> { "SECTION", "SEC" } },
            { SectionType.Table, new List<string> { "TABLE", "TBL", "TAB" } },
            { SectionType.List, new List<string> { "LIST", "LST" } },
            { SectionType.Dictionary, new List<string> { "DICT", "DICTIONARY" } }
        };

        // Unrecognized identifiers remain plain section names. A null name marks an unnamed end.
        private bool tryReadBlockHeader(string Line, out SectionType Type, out string Name)
        {
            Type = SectionType.Section;
            Name = null;
            if (!Line.StartsWith("[", StringComparison.Ordinal) || !Line.EndsWith("]", StringComparison.Ordinal)) {
                return false;
            }

            string Header = Line.Substring(1, Line.Length - 2);
            foreach (KeyValuePair<SectionType, List<string>> Entry in sectionIdentifier) {
                foreach (string Identifier in Entry.Value) {
                    if (Entry.Key == SectionType.None && String.Equals(Header, Identifier, StringComparison.Ordinal)) {
                        Type = Entry.Key;
                        return true;
                    }
                    string Prefix = Identifier + ":";
                    if (Header.StartsWith(Prefix, StringComparison.Ordinal)) {
                        Type = Entry.Key;
                        Name = Header.Substring(Prefix.Length).Trim();
                        return true;
                    }
                }
            }
            Name = Header.Trim();
            return true;
        }

        // Match full-line comments against the configured prefixes.
        private bool isComment(string Line)
        {
            foreach (string Prefix in parComment) {
                if (!String.IsNullOrEmpty(Prefix) && Line.StartsWith(Prefix, StringComparison.Ordinal)) {
                    return true;
                }
            }
            return false;
        }

        // Load the configuration file content.
        private bool loadFile()
        {
            if (!File.Exists(FilePath.FullName)) {
                return false;
            }

            using (StreamReader srFile = FilePath.OpenText()) {
                loadReader(srFile);
            }
            return true;
        }

        private void loadReader(TextReader Reader)
        {
            string currentParent = "";
            SectionType reading = SectionType.None;

            string currentLine;
            while ((currentLine = Reader.ReadLine()) != null) {
                currentLine = currentLine.Trim();
                if (currentLine.Length == 0 || isComment(currentLine)) {
                    continue;
                }

                SectionType Type;
                string Name;
                if (tryReadBlockHeader(currentLine, out Type, out Name)) {
                    if (Type == SectionType.None && reading != SectionType.None && Name != null &&
                        !String.Equals(Name, currentParent, StringComparison.Ordinal)) {
                        throw new FormatException("End label does not match the current block: '" + currentParent + "'.");
                    }
                    reading = Type;
                    currentParent = Type == SectionType.None ? "" : Name;
                    switch (reading) {
                        case SectionType.Section:
                            dictSections.Add(currentParent, new strDictionary());
                            break;
                        case SectionType.Table:
                            dictTables.Add(currentParent, new strTable());
                            break;
                        case SectionType.List:
                            dictLists.Add(currentParent, new List<string>());
                            break;
                        case SectionType.Dictionary:
                            dictDictionaries.Add(currentParent, new strDictionary());
                            break;
                    }
                } else if (reading == SectionType.Section || reading == SectionType.Dictionary) {
                    string[] sline = currentLine.Split(parSeparator);
                    if (sline.Length == 2) {
                        strDictionary Entries = reading == SectionType.Section ? dictSections[currentParent] : dictDictionaries[currentParent];
                        Entries.Add(sline[0].Trim(), sline[1].Trim().TrimEnd(parEndLineDelimiter));
                    }
                } else if (reading == SectionType.Table || reading == SectionType.List) {
                    List<string> Rows = reading == SectionType.Table ? dictTables[currentParent] : dictLists[currentParent];
                    Rows.Add(currentLine.TrimEnd(parEndLineDelimiter));
                }
            }
        }

        // .NET 3.5 StreamReader has no leaveOpen option. Dispose the reader while
        // keeping the caller's stream open, without buffering or requiring seeking.
        private sealed class BorrowedReadStream : Stream
        {
            private readonly Stream Source;

            public BorrowedReadStream(Stream Source)
            {
                this.Source = Source;
            }

            public override bool CanRead { get { return Source.CanRead; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override int Read(byte[] Buffer, int Offset, int Count)
            {
                return Source.Read(Buffer, Offset, Count);
            }

            public override void Flush() { }
            public override long Seek(long Offset, SeekOrigin Origin) { throw new NotSupportedException(); }
            public override void SetLength(long Value) { throw new NotSupportedException(); }
            public override void Write(byte[] Buffer, int Offset, int Count) { throw new NotSupportedException(); }
        }
    }
}
