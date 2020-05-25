using System;
using BenchmarkDotNet.Attributes;

namespace CustomWorkQueue.Benchmarks
{
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true)]
    public class WorkItemPerSecondMaxColumnAttribute : ColumnConfigBaseAttribute
    {
        public WorkItemPerSecondMaxColumnAttribute()
            : base(new WorkItemsPerSecondColumn(x => x.Min, "Max"))
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true)]
    public class WorkItemPerSecondQ1ColumnAttribute : ColumnConfigBaseAttribute
    {
        public WorkItemPerSecondQ1ColumnAttribute()
            : base(new WorkItemsPerSecondColumn(x => x.Q1, "Q1"))
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true)]
    public class WorkItemPerSecondMedianColumnAttribute : ColumnConfigBaseAttribute
    {
        public WorkItemPerSecondMedianColumnAttribute()
            : base(new WorkItemsPerSecondColumn(x => x.Median, "Median"))
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true)]
    public class WorkItemPerSecondQ3ColumnAttribute : ColumnConfigBaseAttribute
    {
        public WorkItemPerSecondQ3ColumnAttribute()
            : base(new WorkItemsPerSecondColumn(x => x.Q3, "Q3"))
        {
        }
    }
}