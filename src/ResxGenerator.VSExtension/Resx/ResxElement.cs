using System.Diagnostics;

namespace ResxGenerator.VSExtension.Resx
{
    [DebuggerDisplay("Key = {Key} Value = {Value}")]
    public class ResxElement(string key, string? value) : IEquatable<ResxElement>
    {
        /// <summary>
        /// Resource key
        /// </summary>
        public string Key { get; set; } = key;

        /// <summary>
        /// Resource value
        /// </summary>
        public string? Value { get; set; } = value;

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
            return this.Key.ToLower().GetHashCode();
        }
    }
}