using System;

namespace CustomWorkQueue.Benchmarks
{
    public sealed class Counts
    {
        public Counts(long iterations, long items)
            : this(iterations, items, 1L)
        {
        }

        public Counts(long iterations, long items, long nestedItems)
        {
            Iterations = Math.Max(iterations, 1L);
            Items = Math.Max(items, 1L);
            NestedItems = Math.Max(nestedItems, 1L);
        }

        public long Iterations { get; }
        public long Items { get; }
        public long NestedItems { get; }

        public override string ToString()
        {
            return $"{Iterations}/{Items}/{NestedItems}";
        }
    }
}