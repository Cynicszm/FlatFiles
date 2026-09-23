namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     A draw in [0, 1) that spreads evenly rather than clumping. Successive values step by the fractional
    ///     part of the golden ratio and wrap, so after any number of draws the values are spread about as evenly
    ///     over the interval as that many values can be. A random source would need far more records to land on
    ///     the shares a profile asks for, and would not give the same file twice.
    /// </summary>
    internal struct Sequence( double seed )
    {
        private const double GoldenRatio = 0.618033988749895;

        private double position = seed - (long) seed;

        public double Next()
        {
            var current = position;
            position += GoldenRatio;
            if (position >= 1.0)
            {
                position -= 1.0;
            }
            return current;
        }
    }
}
