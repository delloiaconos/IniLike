using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ConfigurationFilesReader
{
    using strDictionary = Dictionary<string, string>;
    using strTable = List<string>;

    public partial class ConfigurationFile
    {
        private enum BlockTypes { None = 0, Section, Table, List, Dictionary, Text, Encoded, Include };

        private readonly Dictionary<BlockTypes, List<string>> blockIdentifiers = new Dictionary<BlockTypes, List<string>> {
            { BlockTypes.Include, new List<string> { "INCLUDE", "INC", "INPUT", "LINK", "LOAD" } },
            { BlockTypes.None, new List<string> { "END" } },
            { BlockTypes.Section, new List<string> { "SECTION", "SEC" } },
            { BlockTypes.Table, new List<string> { "TABLE", "TBL", "TAB" } },
            { BlockTypes.List, new List<string> { "LIST", "LST" } },
            { BlockTypes.Dictionary, new List<string> { "DICT", "DICTIONARY" } },
            { BlockTypes.Text, new List<string> { "TEXT", "TXT" } },
            { BlockTypes.Encoded, new List<string> { "ENCODED", "ENC", "BASE64", "B64" } }
        };

        private readonly HashSet<string> loadingFiles = new HashSet<string>(
            Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        // Unrecognized identifiers remain plain section names. A null name marks an unnamed end.
        private bool tryReadBlockHeader(string Line, out BlockTypes Type, out string Name)
        {
            Type = BlockTypes.Section;
            Name = null;
            if (!Line.StartsWith("[", StringComparison.Ordinal) || !Line.EndsWith("]", StringComparison.Ordinal)) {
                return false;
            }

            string Header = Line.Substring(1, Line.Length - 2);
            foreach (KeyValuePair<BlockTypes, List<string>> Entry in blockIdentifiers) {
                foreach (string Identifier in Entry.Value) {
                    if (Entry.Key == BlockTypes.None && String.Equals(Header, Identifier, StringComparison.Ordinal)) {
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

            loadIncludedFile(FilePath.FullName);
            return true;
        }

        // Resolve every include against the working directory, including nested ones.
        private void loadIncludedFile(string FileName)
        {
            if (FileName.Length == 0) {
                throw new FormatException("An INCLUDE block requires a filename.");
            }
            string FullPath = Path.GetFullPath(FileName);
            if (loadingFiles.Contains(FullPath)) {
                throw new FormatException("Include cycle detected for file '" + FullPath + "'.");
            }
            // Bound recursion even when filesystem aliases hide a repeated path.
            if (loadingFiles.Count >= 128) {
                throw new FormatException("Maximum include nesting depth (128 files) exceeded at '" + FullPath + "'.");
            }
            loadingFiles.Add(FullPath);
            try {
                using (StreamReader Reader = File.OpenText(FullPath)) {
                    loadReader(Reader);
                }
            } finally {
                loadingFiles.Remove(FullPath);
            }
        }

        private void loadReader(TextReader Reader)
        {
            string currentParent = "";
            BlockTypes reading = BlockTypes.None;
            StringBuilder Encoded = new StringBuilder();

            string currentLine;
            while ((currentLine = Reader.ReadLine()) != null) {
                string RawLine = currentLine;
                currentLine = currentLine.Trim();
                if (reading != BlockTypes.Text && (currentLine.Length == 0 || isComment(currentLine))) {
                    continue;
                }

                BlockTypes Type;
                string Name;
                if (tryReadBlockHeader(currentLine, out Type, out Name)) {
                    if (Type == BlockTypes.None && reading != BlockTypes.None && Name != null &&
                        !String.Equals(Name, currentParent, StringComparison.Ordinal)) {
                        throw new FormatException("End label does not match the current block: '" + currentParent + "'.");
                    }
                    if (reading == BlockTypes.Encoded) {
                        dictEncoded[currentParent] = decodeBase64(currentParent, Encoded.ToString());
                        Encoded.Length = 0;
                    }
                    reading = Type;
                    currentParent = Type == BlockTypes.None ? "" : Name;
                    switch (reading) {
                        case BlockTypes.Include:
                            loadIncludedFile(currentParent);
                            break;
                        case BlockTypes.Section:
                            dictSections.Add(currentParent, new strDictionary());
                            break;
                        case BlockTypes.Table:
                            dictTables.Add(currentParent, new strTable());
                            break;
                        case BlockTypes.List:
                            dictLists.Add(currentParent, new List<string>());
                            break;
                        case BlockTypes.Encoded:
                            dictEncoded.Add(currentParent, new byte[0]);
                            break;
                        case BlockTypes.Text:
                            dictTexts.Add(currentParent, new List<string>());
                            break;
                        case BlockTypes.Dictionary:
                            dictDictionaries.Add(currentParent, new strDictionary());
                            break;
                    }
                } else if (reading == BlockTypes.Encoded) {
                    Encoded.Append(currentLine);
                } else if (reading == BlockTypes.Text) {
                    dictTexts[currentParent].Add(RawLine);
                } else if (reading == BlockTypes.Section || reading == BlockTypes.Dictionary) {
                    string[] sline = currentLine.Split(parSeparator);
                    if (sline.Length == 2) {
                        strDictionary Entries = reading == BlockTypes.Section ? dictSections[currentParent] : dictDictionaries[currentParent];
                        Entries.Add(sline[0].Trim(), sline[1].Trim().TrimEnd(parEndLineDelimiter));
                    }
                } else if (reading == BlockTypes.Table || reading == BlockTypes.List) {
                    List<string> Rows = reading == BlockTypes.Table ? dictTables[currentParent] : dictLists[currentParent];
                    Rows.Add(currentLine.TrimEnd(parEndLineDelimiter));
                }
            }
            if (reading == BlockTypes.Encoded) {
                dictEncoded[currentParent] = decodeBase64(currentParent, Encoded.ToString());
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
