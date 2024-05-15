namespace ResxGenerator.VSExtension.Infrastructure
{
    public static class StringExtensions
    {
        /// <summary>
        /// Trim only one occurrence of the given character from the start and the end of the string
        /// </summary>
        /// <param name="value"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        //[return: NotNullIfNotNull(nameof(value))]
        public static string? TrimOne(this string value, char c)
        {
            if (value is null) return null;

            if (value[0] == c)
                value = value.Substring(1);

            if (value[value.Length - 1] == c)
                value = value.Substring(0, value.Length - 1);

            return value;
        }
    }
}