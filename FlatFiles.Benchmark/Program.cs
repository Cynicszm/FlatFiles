using System;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Running;

namespace FlatFiles.Benchmark
{
    internal class Program
    {
        private static void Main( string[] args )
        {
            RunBenchmarks( args );
        }

        // The suite is large enough that running all of it to answer one question is a waste, so the command line is
        // handed to BenchmarkDotNet. Every benchmark's name ends in the direction it measures, so --filter *_Read
        // runs the reading half and --filter *_Write the writing half.
        private static void RunBenchmarks( string[] args )
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
            // What the reading and writing paths cost is mostly what they allocate, so the allocation column is not
            // optional here.
            configuration.AddDiagnoser( MemoryDiagnoser.Default );

            BenchmarkRunner.Run<CoreBenchmarkSuite>( configuration, args );

            Console.Out.Write( "Hit <enter> to exit..." );
            Console.In.ReadLine();
        }
    }
}
