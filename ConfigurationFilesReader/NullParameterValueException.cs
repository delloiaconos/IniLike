using System;
using System.Runtime.Serialization;

namespace ConfigurationFilesReader
{
    // A present parameter has no value and the typed read supplied no default.
    [Serializable]
    public class NullParameterValueException : InvalidOperationException
    {
        public string SectionName { get; private set; }
        public string ParameterName { get; private set; }

        public NullParameterValueException(string SectionName, string ParameterName)
            : base("Parameter '" + ParameterName + "' in section '" + SectionName + "' has a null value.")
        {
            this.SectionName = SectionName;
            this.ParameterName = ParameterName;
        }

        protected NullParameterValueException(SerializationInfo Info, StreamingContext Context)
            : base(Info, Context)
        {
            SectionName = Info.GetString("SectionName");
            ParameterName = Info.GetString("ParameterName");
        }

        public override void GetObjectData(SerializationInfo Info, StreamingContext Context)
        {
            base.GetObjectData(Info, Context);
            Info.AddValue("SectionName", SectionName);
            Info.AddValue("ParameterName", ParameterName);
        }
    }
}
