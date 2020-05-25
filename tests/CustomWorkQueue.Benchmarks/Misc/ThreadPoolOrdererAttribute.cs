using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace CustomWorkQueue.Benchmarks
{
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class)]
    public class ThreadPoolOrdererAttribute : Attribute, IConfigSource
    {
        public ThreadPoolOrdererAttribute()
        {
            Config = ManualConfig.CreateEmpty().WithOrderer(new ThreadPoolOrderer());
        }

        public IConfig Config { get; }
    }

    internal class ThreadPoolOrderer : IOrderer
    {
        public IEnumerable<BenchmarkCase> GetExecutionOrder(ImmutableArray<BenchmarkCase> benchmarksCase) =>
            from benchmark in benchmarksCase
            orderby benchmark.Parameters["pool"].ToString(), benchmark.Descriptor.WorkloadMethodDisplayInfo
            select benchmark;

        public IEnumerable<BenchmarkCase> GetSummaryOrder(ImmutableArray<BenchmarkCase> benchmarksCase, Summary summary) =>
            from benchmark in benchmarksCase
            orderby benchmark.Parameters["pool"].ToString(), benchmark.Descriptor.WorkloadMethodDisplayInfo
            select benchmark;

        public string GetHighlightGroupKey(BenchmarkCase benchmarkCase) => null;

        public string GetLogicalGroupKey(ImmutableArray<BenchmarkCase> allBenchmarksCases, BenchmarkCase benchmarkCase) =>
            benchmarkCase.Parameters["pool"].ToString();

        public IEnumerable<IGrouping<string, BenchmarkCase>> GetLogicalGroupOrder(IEnumerable<IGrouping<string, BenchmarkCase>> logicalGroups) =>
            logicalGroups.OrderBy(it => it.Key);

        public bool SeparateLogicalGroups => true;
    }
}