using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     The set of widths one column takes, and how often it takes each. A profile records only the smallest,
    ///     largest, mean and most frequent width, so the ladder reconstructs a distribution with those four
    ///     properties rather than the one the original file had: the most frequent width keeps its share, and the
    ///     rest of the mass is spread over a ladder between the smallest and largest and then tilted until the mean
    ///     comes out right.
    /// </summary>
    internal sealed class WidthLadder
    {
        private const int MaximumRungs = 12;

        private readonly int[] widths;
        private readonly double[] cumulative;

        public WidthLadder( int[] widths, double[] shares )
        {
            this.widths = widths;
            cumulative = new double[shares.Length];
            var running = 0.0;
            for (var index = 0; index != shares.Length; ++index)
            {
                running += shares[index];
                cumulative[index] = running;
            }
            // The last rung takes any rounding left over, so a draw of almost one still lands somewhere.
            cumulative[^1] = 1.0;
        }

        /// <summary>
        ///     The width for a draw in [0, 1).
        /// </summary>
        public int Sample( double position )
        {
            for (var index = 0; index != cumulative.Length; ++index)
            {
                if (position < cumulative[index])
                {
                    return widths[index];
                }
            }
            return widths[^1];
        }

        /// <summary>
        ///     The share of records whose field has no width at all, which is the share that must read as empty.
        /// </summary>
        public double ZeroWidthShare
        {
            get
            {
                var previous = 0.0;
                for (var index = 0; index != widths.Length; ++index)
                {
                    if (widths[index] == 0)
                    {
                        return cumulative[index] - previous;
                    }
                    previous = cumulative[index];
                }
                return 0.0;
            }
        }

        public static WidthLadder FromProfile( ColumnProfile column )
        {
            if (column.MinWidth >= column.MaxWidth)
            {
                return new WidthLadder( [column.MaxWidth], [1.0] );
            }
            var modeShare = Math.Clamp( column.ModeShare, 0.0, 1.0 );
            if (modeShare >= 1.0)
            {
                return new WidthLadder( [column.ModeWidth], [1.0] );
            }
            var rest = BuildRungs( column );
            var restShares = Tilt( rest, RemainingMean( column, modeShare ) );

            var byWidth = new SortedDictionary<int, double>();
            for (var index = 0; index != rest.Length; ++index)
            {
                byWidth[rest[index]] = byWidth.GetValueOrDefault( rest[index] ) + restShares[index] * ( 1.0 - modeShare );
            }
            byWidth[column.ModeWidth] = byWidth.GetValueOrDefault( column.ModeWidth ) + modeShare;
            return new WidthLadder( [.. byWidth.Keys], [.. byWidth.Values] );
        }

        /// <summary>
        ///     The mean the rungs other than the most frequent one have to average for the column's own mean to
        ///     come out right.
        /// </summary>
        private static double RemainingMean( ColumnProfile column, double modeShare )
        {
            var mean = ( column.AverageWidth - modeShare * column.ModeWidth ) / ( 1.0 - modeShare );
            return Math.Clamp( mean, column.MinWidth, column.MaxWidth );
        }

        /// <summary>
        ///     Widths between the smallest and largest, excluding the most frequent one, at most one per distinct
        ///     width the original column had.
        /// </summary>
        private static int[] BuildRungs( ColumnProfile column )
        {
            var count = Math.Clamp( column.DistinctWidths - 1, 1, MaximumRungs );
            var rungs = new SortedSet<int> { column.MinWidth, column.MaxWidth };
            for (var step = 1; step <= count && rungs.Count < count; ++step)
            {
                var width = column.MinWidth + (int) Math.Round( (double) step * ( column.MaxWidth - column.MinWidth ) / ( count + 1 ) );
                rungs.Add( width );
            }
            rungs.Remove( column.ModeWidth );
            if (rungs.Count == 0)
            {
                rungs.Add( column.MinWidth == column.ModeWidth ? column.MaxWidth : column.MinWidth );
            }
            return [.. rungs];
        }

        /// <summary>
        ///     Shares over the rungs that average <paramref name="mean" />: uniform to begin with, then blended
        ///     towards whichever end the mean sits on. A blend keeps every rung in play, which a two-point solution
        ///     would not.
        /// </summary>
        private static double[] Tilt( int[] rungs, double mean )
        {
            var shares = new double[rungs.Length];
            Array.Fill( shares, 1.0 / rungs.Length );
            if (rungs.Length == 1)
            {
                return shares;
            }
            var uniformMean = rungs.Average();
            var endpoint = mean > uniformMean ? rungs[^1] : rungs[0];
            var spread = endpoint - uniformMean;
            if (Math.Abs( spread ) < 1e-9)
            {
                return shares;
            }
            var weight = Math.Clamp( ( mean - uniformMean ) / spread, 0.0, 1.0 );
            var target = mean > uniformMean ? rungs.Length - 1 : 0;
            for (var index = 0; index != shares.Length; ++index)
            {
                shares[index] *= 1.0 - weight;
            }
            shares[target] += weight;
            return shares;
        }
    }
}
