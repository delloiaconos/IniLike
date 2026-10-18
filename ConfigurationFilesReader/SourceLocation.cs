namespace ConfigurationFilesReader
{
    // Immutable provenance of an element parsed from one source line.
    public sealed class SourceLocation
    {
        public string SourceName { get; private set; }
        public string FilePath { get; private set; }
        public int LineNumber { get; private set; }

        internal SourceLocation(string SourceName, string FilePath, int LineNumber)
        {
            this.SourceName = SourceName ?? FilePath ?? "<stream>";
            this.FilePath = FilePath;
            this.LineNumber = LineNumber;
        }
    }
}
