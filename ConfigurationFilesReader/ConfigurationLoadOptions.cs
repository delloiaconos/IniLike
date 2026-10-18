using System;

namespace ConfigurationFilesReader
{
    // Options are captured by ConfigurationFile before any input is read.
    public sealed class ConfigurationLoadOptions
    {
        // Null preserves working-directory resolution; otherwise all relative includes use this base.
        public string IncludeBaseDirectory { get; set; }

        // Display label for a caller-owned stream; does not affect file resolution.
        public string SourceName { get; set; }

        // Arguments: requested filename, including absolute source path (null for anonymous streams).
        // Return a nonempty path; relative results use IncludeBaseDirectory or the working directory.
        public Func<string, string, string> IncludeResolver { get; set; }
    }
}
