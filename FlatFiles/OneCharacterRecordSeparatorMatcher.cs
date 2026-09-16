namespace FlatFiles
{
    internal sealed class OneCharacterRecordSeparatorMatcher(RetryReader reader, char first) : IRecordSeparatorMatcher
    {
        public int Size => 1;

        public bool IsMatch()
        {
            return reader.IsMatch1(first);
        }

        public string Trim(string value)
        {
            int length = value.Length;
            if (length >= 1 && value[length - 1] == first)
            {
                return value.Substring(0, length - 1);
            }
            return value;
        }
    }
}
