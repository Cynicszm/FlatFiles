using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace FlatFiles.Benchmark
{
    internal class Program
    {
        private static void Main()
        {
            RunBenchmarks();

            //RunPerformanceMonitor();
        }

        private static void RunBenchmarks()
        {
            var configuration = new ManualConfig
            {
                Options = ConfigOptions.KeepBenchmarkFiles
            };
            configuration.AddColumn( StatisticColumn.Min );
            configuration.AddColumn( StatisticColumn.Max );
            configuration.AddColumnProvider( [.. DefaultConfig.Instance.GetColumnProviders()] );
            configuration.AddLogger( [.. DefaultConfig.Instance.GetLoggers()] );
            configuration.AddDiagnoser( [.. DefaultConfig.Instance.GetDiagnosers()] );
            configuration.AddAnalyser( [.. DefaultConfig.Instance.GetAnalysers()] );
            configuration.AddJob( [.. DefaultConfig.Instance.GetJobs()] );
            configuration.AddValidator( [.. DefaultConfig.Instance.GetValidators()] );

            BenchmarkRunner.Run<CoreBenchmarkSuite>( configuration );

            Console.Out.Write( "Hit <enter> to exit..." );
            Console.In.ReadLine();
        }

        [SuppressMessage( "CodeQuality", "IDE0051" )]
        private static void RunPerformanceMonitor()
        {
            var tester = new AsyncVsSyncTest();
            for (var i = 0; i != 10; ++i)
            {
                tester.SyncTest();
            }

            var stopwatch = Stopwatch.StartNew();
            var syncResult = tester.SyncTest();
            stopwatch.Stop();
            Console.Out.WriteLine( $"Sync Execution Time: {stopwatch.Elapsed}" );
            Console.Out.WriteLine( $"Sync Result Count: {syncResult.Length}" );

            for (var i = 0; i != 10; ++i)
            {
                tester.AsyncTest().Wait();
            }

            stopwatch.Restart();
            var asyncResult = tester.AsyncTest().Result;
            stopwatch.Stop();
            Console.Out.WriteLine( $"Async Execution Time: {stopwatch.Elapsed}" );
            Console.Out.WriteLine( $"Async Result Count: {asyncResult.Length}" );
        }
    }
}
