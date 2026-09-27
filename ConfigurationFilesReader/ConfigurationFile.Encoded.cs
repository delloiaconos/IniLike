using System;
using System.Collections.Generic;

namespace ConfigurationFilesReader
{
    public partial class ConfigurationFile
    {
        private Dictionary<string, byte[]> dictEncoded;

        // Return a sorted snapshot of binary block names.
        public List<string> listEncoded()
        {
            List<string> Names = new List<string>(dictEncoded.Keys);
            Names.Sort(StringComparer.Ordinal);
            return Names;
        }

        // Return an independent copy of decoded bytes, or an empty array when missing.
        public byte[] getEncoded(string BlockName)
        {
            if (BlockName == null) {
                throw new ArgumentNullException("BlockName");
            }
            byte[] Data;
            return dictEncoded.TryGetValue(BlockName, out Data) ? (byte[])Data.Clone() : new byte[0];
        }

        private static byte[] decodeBase64(string BlockName, string Encoded)
        {
            try {
                return Convert.FromBase64String(Encoded);
            } catch (FormatException Error) {
                throw new FormatException("Invalid Base64 data in block '" + BlockName + "'.", Error);
            }
        }
    }
}
