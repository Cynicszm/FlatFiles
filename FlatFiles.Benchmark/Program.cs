using System;
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
    }
}
