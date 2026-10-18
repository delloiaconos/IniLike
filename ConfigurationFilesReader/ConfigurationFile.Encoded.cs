using System;
using System.Collections.Generic;
using System.Text;

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
            return getEncoded(BlockName, false);
        }

        // When encoded is true, return canonical Base64 as ASCII bytes.
        public byte[] getEncoded(string BlockName, bool encoded)
        {
            if (BlockName == null) {
                throw new ArgumentNullException("BlockName");
            }
            byte[] Data;
            if (!dictEncoded.TryGetValue(BlockName, out Data)) {
                return new byte[0];
            }
            return encoded ? Encoding.ASCII.GetBytes(Convert.ToBase64String(Data)) : (byte[])Data.Clone();
        }

        private static byte[] decodeBase64(string BlockName, string Encoded)
        {
            try {
                return Convert.FromBase64String(Encoded);
            } catch (FormatException Error) {
                throw new FormatException("Invalid Base64 data in block '" + BlockName + "'.", Error);
            }
        }

        // Presence checks never create entries or write files.
        public bool containsEncoded(string Name)
        {
            if (Name == null) { throw new ArgumentNullException("Name"); }
            return dictEncoded.ContainsKey(Name);
        }

        // A missing block returns false and a null output; empty blocks return true.
        public bool tryGetEncoded(string Name, out byte[] Data)
        {
            if (Name == null) { throw new ArgumentNullException("Name"); }
            Data = null;
            byte[] Stored;
            if (!dictEncoded.TryGetValue(Name, out Stored)) { return false; }
            Data = (byte[])Stored.Clone();
            return true;
        }

        // Select a decoded copy or canonical Base64 ASCII bytes.
        public bool tryGetEncoded(string Name, bool Encoded, out byte[] Data)
        {
            if (!tryGetEncoded(Name, out Data)) { return false; }
            if (Encoded) { Data = Encoding.ASCII.GetBytes(Convert.ToBase64String(Data)); }
            return true;
        }
    }
}
