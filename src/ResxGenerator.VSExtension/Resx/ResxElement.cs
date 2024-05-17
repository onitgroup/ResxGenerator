using System.Diagnostics;

namespace ResxGenerator.VSExtension.Resx
{
    [DebuggerDisplay("Key = {Key} Value = {Value} Comment = {Comment}")]
    public class ResxElement: IEquatable<ResxElement>
    {
        /// <summary>
        /// Resource key
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Resource value
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// Resource comment
        /// </summary>
        public string? Comment { get; set; }

        public ResxElement(string key, string? value, string? comment)
        {
            Key = key;
            Value = value;
            Comment = comment;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is ResxElement element) return Equals(element);
            return false;
        }

        public bool Equals(ResxElement other)
        {
            if (other is null) return false;
            return other.Key.Equals(Key, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return Key.ToLower().GetHashCode();
        }
    }
}