using System;
using System.Threading;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     The largest the managed heap gets while something runs. A total at the end says nothing, because a
    ///     collection between the work and the reading hides whatever the work was holding, so this samples while
    ///     the work is going on.
    /// </summary>
    internal sealed class PeakMemoryWatcher : IDisposable
    {
        private const int IntervalMilliseconds = 5;

        private readonly Timer timer;
        private long peak;

        public PeakMemoryWatcher()
        {
            peak = GC.GetTotalMemory( false );
            timer = new Timer( _ => Sample(), null, IntervalMilliseconds, IntervalMilliseconds );
        }

        public long Peak => Interlocked.Read( ref peak );

        private void Sample()
        {
            var current = GC.GetTotalMemory( false );
            long seen;
            do
            {
                seen = Interlocked.Read( ref peak );
                if (current <= seen)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange( ref peak, current, seen ) != seen);
        }

        public void Dispose()
        {
            Sample();
            timer.Dispose();
        }
    }
}
