using System;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     Produces one column's field, record after record, so that over the whole file the widths and the type
    ///     mix come out as the profile describes them.
    /// </summary>
    /// <remarks>
    ///     Width is drawn first and the kind second, because the two are not independent: a field of no width has
    ///     to read as empty. Whatever share of empty records the width cannot account for is taken from the
    ///     records that do have a width, which are then filled with spaces - a field of spaces is how the profile
    ///     records a column that is empty but still occupies room.
    /// </remarks>
    internal sealed class ColumnWriter
    {
        private readonly WidthLadder ladder;
        private readonly double emptyAmongWidthed;
        private readonly double dateShare;
        private readonly double numericShare;
        private Sequence widths;
        private Sequence kinds;

        public ColumnWriter( ColumnProfile column, int seed )
        {
            ladder = WidthLadder.FromProfile( column );
            widths = new Sequence( seed * 0.7548776662466927 );
            kinds = new Sequence( seed * 0.5698402909980532 + 0.5 );

            var total = column.Total;
            if (total == 0)
            {
                return;
            }
            var zeroWidth = ladder.ZeroWidthShare;
            var empty = (double) column.Empty / total;
            emptyAmongWidthed = zeroWidth >= 1.0 ? 0.0 : Math.Clamp( ( empty - zeroWidth ) / ( 1.0 - zeroWidth ), 0.0, 1.0 );

            var withValue = column.Date + column.Numeric + column.String;
            if (withValue == 0)
            {
                return;
            }
            dateShare = (double) column.Date / withValue;
            numericShare = dateShare + (double) column.Numeric / withValue;
        }

        /// <summary>
        ///     Writes the next record's field and answers its width.
        /// </summary>
        public int Write( Span<char> destination, long record )
        {
            var width = ladder.Sample( widths.Next() );
            var draw = kinds.Next();
            if (width == 0)
            {
                return 0;
            }
            var field = destination[..width];
            if (draw < emptyAmongWidthed)
            {
                ValueFactory.Append( field, ValueKind.Empty, width, record );
                return width;
            }
            var rescaled = emptyAmongWidthed >= 1.0 ? 0.0 : ( draw - emptyAmongWidthed ) / ( 1.0 - emptyAmongWidthed );
            var kind = rescaled < dateShare ? ValueKind.Date
                : rescaled < numericShare ? ValueKind.Numeric
                : ValueKind.String;
            ValueFactory.Append( field, kind, width, record );
            return width;
        }
    }

    /// <summary>
    ///     The final column where it is ragged: the field has one width and the value inside it another, so the
    ///     two are drawn separately and the difference is padding.
    /// </summary>
    internal sealed class RaggedColumnWriter
    {
        private readonly int[] rawWidths;
        private readonly double[] rawCumulative;
        private readonly int[] trimmedLengths;
        private readonly double[] trimmedCumulative;
        private Sequence widths;
        private Sequence lengths;

        public RaggedColumnWriter( RaggedColumnProfile profile, int seed )
        {
            ( rawWidths, rawCumulative ) = Distribution( profile.RawWidths );
            ( trimmedLengths, trimmedCumulative ) = Distribution( profile.TrimmedLengths );
            widths = new Sequence( seed * 0.2718281828459045 );
            lengths = new Sequence( seed * 0.3141592653589793 + 0.25 );
        }

        private static (int[] Values, double[] Cumulative) Distribution( System.Collections.Generic.List<double[]> pairs )
        {
            if (pairs.Count == 0)
            {
                return ( [0], [1.0] );
            }
            var values = new int[pairs.Count];
            var cumulative = new double[pairs.Count];
            var running = 0.0;
            for (var index = 0; index != pairs.Count; ++index)
            {
                values[index] = (int) pairs[index][0];
                running += pairs[index][1];
                cumulative[index] = running;
            }
            cumulative[^1] = 1.0;
            return ( values, cumulative );
        }

        private static int Pick( int[] values, double[] cumulative, double position )
        {
            for (var index = 0; index != cumulative.Length; ++index)
            {
                if (position < cumulative[index])
                {
                    return values[index];
                }
            }
            return values[^1];
        }

        public int Write( Span<char> destination, long record )
        {
            var width = Pick( rawWidths, rawCumulative, widths.Next() );
            var draw = lengths.Next();
            if (width == 0)
            {
                return 0;
            }
            // A value can never be wider than the field holding it, so a draw that is too long is capped.
            var trimmed = Math.Min( Pick( trimmedLengths, trimmedCumulative, draw ), width );
            ValueFactory.AppendRightAligned( destination[..width], trimmed, record );
            return width;
        }
    }
}
