using System.Diagnostics;

namespace ResxGenerator.VSExtension.Resx
{
    [DebuggerDisplay("Key = {Key}, Value = {Value}, Comment = {Comment}")]
    public class ResxElement(string key, string? value, string? comment)
    {
        public string Key { get; set; } = key;

        public string? Value { get; set; } = value;

        public string? Comment { get; set; } = comment;

        public override bool Equals(object? obj)
        {
            return obj is ResxElement other && Equals(other);
        }

        public virtual bool Equals(ResxElement? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Key, other.Key, StringComparison.InvariantCultureIgnoreCase);
        }

        public override int GetHashCode()
        {
            return StringComparer.InvariantCultureIgnoreCase.GetHashCode(Key);
        }
    }
}