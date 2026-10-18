using System;
using System.Collections;
using System.Collections.Generic;

namespace ConfigurationFilesReader
{
    // A live wrapper; every mutation through IDictionary/ICollection is rejected.
    public sealed class ReadOnlyStringDictionary : IDictionary<string, string>
    {
        private readonly Dictionary<string, string> Entries;

        internal ReadOnlyStringDictionary(Dictionary<string, string> Entries)
        {
            this.Entries = Entries;
        }

        public string this[string Key] { get { return Entries[Key]; } }
        string IDictionary<string, string>.this[string Key]
        {
            get { return Entries[Key]; }
            set { throw new NotSupportedException("The dictionary is read-only."); }
        }
        public int Count { get { return Entries.Count; } }
        public bool IsReadOnly { get { return true; } }
        public ICollection<string> Keys { get { return Entries.Keys; } }
        public ICollection<string> Values { get { return Entries.Values; } }
        public bool containsKey(string Key) { return Entries.ContainsKey(Key); }
        public bool tryGetValue(string Key, out string Value) { return Entries.TryGetValue(Key, out Value); }
        bool IDictionary<string, string>.ContainsKey(string Key) { return containsKey(Key); }
        bool IDictionary<string, string>.TryGetValue(string Key, out string Value) { return tryGetValue(Key, out Value); }
        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator() { return Entries.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return Entries.GetEnumerator(); }
        bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> Item)
        {
            return ((ICollection<KeyValuePair<string, string>>)Entries).Contains(Item);
        }
        void ICollection<KeyValuePair<string, string>>.CopyTo(KeyValuePair<string, string>[] Array, int ArrayIndex)
        {
            ((ICollection<KeyValuePair<string, string>>)Entries).CopyTo(Array, ArrayIndex);
        }
        void IDictionary<string, string>.Add(string Key, string Value) { throw new NotSupportedException(); }
        bool IDictionary<string, string>.Remove(string Key) { throw new NotSupportedException(); }
        void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> Item) { throw new NotSupportedException(); }
        bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> Item) { throw new NotSupportedException(); }
        void ICollection<KeyValuePair<string, string>>.Clear() { throw new NotSupportedException(); }
    }
}
