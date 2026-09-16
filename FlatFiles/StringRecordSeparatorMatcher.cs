namespace FlatFiles
{
    internal sealed class StringRecordSeparatorMatcher(RetryReader reader, string separator) : IRecordSeparatorMatcher
    {
        public int Size => separator.Length;

        public bool IsMatch()
        {
            return reader.IsMatch(separator);
        }

        public string Trim(string value)
        {
            if (value.EndsWith(separator))
            {
                return value.Substring(0, value.Length - separator.Length);
            }
            return value;
        }
    }
}
