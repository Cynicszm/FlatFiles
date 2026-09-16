namespace FlatFiles
{
    internal sealed class TwoCharacterRecordSeparatorMatcher(RetryReader reader, char first, char second) : IRecordSeparatorMatcher
    {
        public int Size => 2;

        public bool IsMatch()
        {
            return reader.IsMatch2(first, second);
        }

        public string Trim(string value)
        {
            int length = value.Length;
            if (length >= 2 && value[length - 2] == first && value[length - 1] == second)
            {
                return value.Substring(0, length - 2);
            }
            return value;
        }
    }
}
