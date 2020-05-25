using System;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Mathematics;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace CustomWorkQueue.Benchmarks
{
    public class WorkItemsPerSecondColumn : IColumn
    {
        public string Id => $"WorkItemsPerSecond{Name}";
        public string ColumnName => $"WPS {Name}";

        public WorkItemsPerSecondColumn()
            : this(s => s.Mean, "Mean")
        {
        }

        public WorkItemsPerSecondColumn(Func<Statistics, double> selector, string name)
        {
            Selector = selector;
            Name = name;
        }

        public Func<Statistics, double> Selector { get; }
        public string Name { get; }

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
        public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        {
            var results = summary[benchmarkCase].ResultStatistics;
            if (results == null) return null;

            var itemsPerOperation = 1L;

            foreach (var parameterInstance in benchmarkCase.Parameters.Items)
            {
                if (parameterInstance.Value is Counts counts)
                {
                    itemsPerOperation = counts.Iterations * counts.Items * counts.NestedItems;
                }
            }

            return (itemsPerOperation * (1_000_000_000D / Selector(results))).ToString("N0");
        }

        public bool IsAvailable(Summary summary) => true;
        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Statistics;
        public int PriorityInCategory => -1000;
        public bool IsNumeric => true;
        public UnitType UnitType => UnitType.Dimensionless;
        public string Legend => $"Work Items per Second ({Name})";
        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) => GetValue(summary, benchmarkCase);
        public override string ToString() => ColumnName;
    }
}