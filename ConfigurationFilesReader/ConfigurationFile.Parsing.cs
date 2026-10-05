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
                    loadReader(Reader, FullPath);
                }
            } catch (Exception Error) {
                if (Error.Data.Contains("FilePath")) {
                    throw;
                }
                throw withSourceContext(Error, FullPath, 0);
            } finally {
                loadingFiles.Remove(FullPath);
            }
        }

        private void loadReader(TextReader Reader, string SourcePath)
        {
            string currentParent = "";
            BlockTypes reading = BlockTypes.None;
            StringBuilder Encoded = new StringBuilder();

            int LineNumber = 0;
            int BlockLineNumber = 0;
            try {
                string currentLine;
                while (true) {
                    LineNumber++;
                    currentLine = Reader.ReadLine();
                    if (currentLine == null) {
                        LineNumber--;
                        break;
                    }
                    string RawLine = currentLine;
                    currentLine = currentLine.Trim();
                    if (reading != BlockTypes.Text && (currentLine.Length == 0 || isComment(currentLine))) {
                        continue;
                    }

                    BlockTypes Type;
                    string Name;
                    if (tryReadBlockHeader(currentLine, out Type, out Name)) {
                        if (Type == BlockTypes.None && reading != BlockTypes.None && Name != null &&
                            !NameComparer.Equals(Name, currentParent)) {
                            throw new FormatException("End label does not match the current block: '" + currentParent + "'.");
                        }
                        if (reading == BlockTypes.Encoded) {
                            dictEncoded[currentParent] = decodeBase64AtSource(currentParent, Encoded.ToString(), SourcePath, BlockLineNumber);
                            Encoded.Length = 0;
                        }
                        BlockLineNumber = LineNumber;
                        reading = Type;
                        currentParent = Type == BlockTypes.None ? "" : Name;
                        switch (reading) {
                            case BlockTypes.Include:
                                loadIncludedFile(currentParent);
                                break;
                            case BlockTypes.Section:
                                dictSections.Add(currentParent, new strDictionary(NameComparer));
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
                                dictDictionaries.Add(currentParent, new strDictionary(NameComparer));
                                break;
                        }
                    } else if (reading == BlockTypes.Encoded) {
                        Encoded.Append(currentLine);
                    } else if (reading == BlockTypes.Text) {
                        dictTexts[currentParent].Add(RawLine);
                    } else if (reading == BlockTypes.Section || reading == BlockTypes.Dictionary) {
                        string[] sline = currentLine.Split(parSeparator, 2);
                        if (sline.Length == 2) {
                            strDictionary Entries = reading == BlockTypes.Section ? dictSections[currentParent] : dictDictionaries[currentParent];
                            Entries.Add(sline[0].Trim(), sline[1].Trim());
                        }
                    } else if (reading == BlockTypes.Table || reading == BlockTypes.List) {
                        List<string> Rows = reading == BlockTypes.Table ? dictTables[currentParent] : dictLists[currentParent];
                        Rows.Add(currentLine);
                    }
                }
                if (reading == BlockTypes.Encoded) {
                    dictEncoded[currentParent] = decodeBase64AtSource(currentParent, Encoded.ToString(), SourcePath, BlockLineNumber);
                }
            } catch (Exception Error) {
                // Already annotated within this source (for example deferred Base64 decoding).
                if (String.Equals(Error.Data["ContextSource"] as string, SourcePath ?? "<stream>", StringComparison.Ordinal)) {
                    throw;
                }
                throw withSourceContext(Error, SourcePath, LineNumber);
            }
        }

        private static byte[] decodeBase64AtSource(string BlockName, string Encoded, string SourcePath, int LineNumber)
        {
            try {
                return decodeBase64(BlockName, Encoded);
            } catch (FormatException Error) {
                throw withSourceContext(Error, SourcePath, LineNumber);
            }
        }

        // Keep the original exception as the cause and preserve common exception types.
        private static Exception withSourceContext(Exception Error, string SourcePath, int LineNumber)
        {
            string Source = SourcePath ?? "<stream>";
            string Location = LineNumber > 0 ? "line " + LineNumber : "before reading";
            string Message = "Source '" + Source + "', " + Location + ": " + Error.Message;
            Exception Result;
            if (Error is FormatException) {
                Result = new FormatException(Message, Error);
            } else if (Error is ArgumentNullException) {
                Result = new ArgumentNullException(Message, Error);
            } else if (Error is ArgumentException) {
                Result = new ArgumentException(Message, ((ArgumentException)Error).ParamName, Error);
            } else if (Error is FileNotFoundException) {
                Result = new FileNotFoundException(Message, ((FileNotFoundException)Error).FileName, Error);
            } else if (Error is DirectoryNotFoundException) {
                Result = new DirectoryNotFoundException(Message, Error);
            } else if (Error is UnauthorizedAccessException) {
                Result = new UnauthorizedAccessException(Message, Error);
            } else if (Error is IOException) {
                Result = new IOException(Message, Error);
            } else {
                Result = new Exception(Message, Error);
            }
            // Retain the innermost source as structured data; messages describe the include chain.
            Result.Data["FilePath"] = Error.Data.Contains("FilePath") ? Error.Data["FilePath"] : Source;
            Result.Data["FileName"] = Error.Data.Contains("FileName") ? Error.Data["FileName"] :
                (SourcePath == null ? "<stream>" : Path.GetFileName(SourcePath));
            Result.Data["LineNumber"] = Error.Data.Contains("LineNumber") ? Error.Data["LineNumber"] : LineNumber;
            Result.Data["ContextSource"] = Source;
            return Result;
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
