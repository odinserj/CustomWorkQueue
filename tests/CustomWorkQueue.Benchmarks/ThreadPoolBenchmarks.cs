using System;
using System.Collections.Generic;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Helios.Concurrency;
using Microsoft.Diagnostics.Runtime;

namespace CustomWorkQueue.Benchmarks
{
    [AsciiDocExporter]
    [WorkItemPerSecondMaxColumn, WorkItemPerSecondQ1Column, WorkItemPerSecondMedianColumn, WorkItemPerSecondQ3Column]
    [ThreadPoolOrdererAttribute]
    public class ThreadPoolBenchmarks
    {
        private readonly IThreadPool<IThreadPoolWorkItem>[] _pools =
        {
            new CustomThreadPool(Environment.ProcessorCount),
            new DedicatedThreadPool(new DedicatedThreadPoolSettings(Environment.ProcessorCount)),
            new ClrThreadPool()
        };

        [GlobalSetup]
        public void GlobalSetup()
        {
            ThreadPool.SetMaxThreads(Environment.ProcessorCount, Environment.ProcessorCount);
        }

        [Benchmark]
        [BenchmarkCategory("local", "partial")]
        [ArgumentsSource(nameof(LocalPartialLoadArgs))]
        public void Local_01_PartialLoad(IThreadPool<IThreadPoolWorkItem> pool, Counts counts)
        {
            for (var j = 0; j < counts.Iterations; j++)
            {
                using var countdown = new CountdownEvent((int)counts.Items);
                var feedJobs = new FeedJobsWorkItem(pool, countdown, counts.NestedItems, local: true);

                for (var i = 0; i < counts.Items; i++)
                {
                    pool.UnsafeQueueUserWorkItem(feedJobs, false);
                }

                countdown.Wait();
            }

            DrainQueues(pool);
        }

        public IEnumerable<object[]> LocalPartialLoadArgs()
        {
            return GenerateArgs(pool => new Counts(1000L, pool.ConcurrencyLevel / 2, pool.ConcurrencyLevel));
        }

        [Benchmark]
        [BenchmarkCategory("local", "full")]
        [ArgumentsSource(nameof(LocalFullLoadArgs))]
        public void Local_02_FullLoad(IThreadPool<IThreadPoolWorkItem> pool, Counts counts)
        {
            for (var j = 0; j < counts.Iterations; j++)
            {
                using var countdown = new CountdownEvent((int)counts.Items);
                var feedJobs = new FeedJobsWorkItem(pool, countdown, counts.NestedItems, local: true);

                for (var i = 0; i < counts.Items; i++)
                {
                    pool.UnsafeQueueUserWorkItem(feedJobs, false);
                }

                countdown.Wait();
            }

            DrainQueues(pool);
        }

        public IEnumerable<object[]> LocalFullLoadArgs()
        {
            return GenerateArgs(pool => new Counts(1L, pool.ConcurrencyLevel, 100_000L));
        }

        [Benchmark]
        [BenchmarkCategory("sequential", "partial")]
        [ArgumentsSource(nameof(SequentialPartialLoadArgs))]
        public void Sequential_01_PartialLoad(IThreadPool<IThreadPoolWorkItem> pool, Counts counts)
        {
            using var countdown = new CountdownEvent((int)counts.Iterations);

            for (var i = 0; i < counts.Iterations; i++)
            {
                var workItem = new SequentialWorkItem(pool, countdown, counts.Items);
                pool.UnsafeQueueUserWorkItem(workItem, false);
            }

            countdown.Wait();
            DrainQueues(pool);
        }

        public IEnumerable<object[]> SequentialPartialLoadArgs()
        {
            return GenerateArgs(pool => new Counts(pool.ConcurrencyLevel / 2,  1_000L));
        }

        [Benchmark]
        [BenchmarkCategory("sequential", "full")]
        [ArgumentsSource(nameof(SequentialFullLoadArgs))]
        public void Sequential_02_FullLoad(IThreadPool<IThreadPoolWorkItem> pool, Counts counts)
        {
            using var countdown = new CountdownEvent((int)counts.Iterations);

            for (var i = 0; i < counts.Iterations; i++)
            {
                var workItem = new SequentialWorkItem(pool, countdown, counts.Items);
                pool.UnsafeQueueUserWorkItem(workItem, false);
            }

            countdown.Wait();
            DrainQueues(pool);
        }

        public IEnumerable<object[]> SequentialFullLoadArgs()
        {
            return GenerateArgs(pool => new Counts(pool.ConcurrencyLevel, 1_000L));
        }

        [Benchmark]
        [BenchmarkCategory("sequential", "stress")]
        [ArgumentsSource(nameof(SequentialOverloadArgs))]
        public void Sequential_03_Overload(IThreadPool<IThreadPoolWorkItem> pool, Counts counts)
        {
            using var countdown = new CountdownEvent((int)counts.Iterations);

            for (var i = 0; i < counts.Iterations; i++)
            {
                var workItem = new SequentialWorkItem(pool, countdown, counts.Items);
                pool.UnsafeQueueUserWorkItem(workItem, false);
            }

            countdown.Wait();
            DrainQueues(pool);
        }

        public IEnumerable<object[]> SequentialOverloadArgs()
        {
            return GenerateArgs(pool => new Counts(pool.ConcurrencyLevel * 2, 1_000L));
        }

        [Benchmark]
        [BenchmarkCategory("global", "partial")]
        [ArgumentsSource(nameof(GlobalPartialLoadArgs))]
        public void Global_01_PartialLoad(IThreadPool<IThreadPoolWorkItem> pool, Counts counts)
        {
            for (var j = 0; j < counts.Iterations; j++)
            {
                using var countdown = new CountdownEvent(1);
                var signal = new RemainingWorkItem(countdown, counts.Items);

                for (var i = 0; i < counts.Items; i++)
                {
                    pool.UnsafeQueueUserWorkItem(signal, false);
                }

                countdown.Wait();
            }

            // TODO: Item adding is single threaded here
            DrainQueues(pool);
        }

        public IEnumerable<object[]> GlobalPartialLoadArgs()
        {
            return GenerateArgs(pool => new Counts(1000L, pool.ConcurrencyLevel / 2));
        }

        [Benchmark]
        [BenchmarkCategory("global", "full")]
        [ArgumentsSource(nameof(GlobalFullLoadArgs))]
        public void Global_02_FullLoad(IThreadPool<IThreadPoolWorkItem> pool, Counts counts)
        {
            for (var j = 0; j < counts.Iterations; j++)
            {
                using var countdown = new CountdownEvent(1);
                var remaining = new RemainingWorkItem(countdown, counts.Items);

                for (var i = 0; i < counts.Items; i++)
                {
                    pool.UnsafeQueueUserWorkItem(remaining, false);
                }

                countdown.Wait();
            }

            DrainQueues(pool);
        }

        public IEnumerable<object[]> GlobalFullLoadArgs()
        {
            return GenerateArgs(pool => new Counts(1, 10_000L));
        }

        [Benchmark]
        [BenchmarkCategory("global", "stress")]
        [ArgumentsSource(nameof(GlobalOverLoadArgs))]
        public void Global_03_OverLoad(IThreadPool<IThreadPoolWorkItem> pool, Counts counts)
        {
            for (var j = 0; j < counts.Iterations; j++)
            {
                using var countdown = new CountdownEvent((int)counts.Items);
                var feedJobs = new FeedJobsWorkItem(pool, countdown, counts.NestedItems, local: false);

                for (var i = 0; i < counts.Items; i++)
                {
                    pool.UnsafeQueueUserWorkItem(feedJobs, false);
                }

                countdown.Wait();
            }

            DrainQueues(pool);
        }

        public IEnumerable<object[]> GlobalOverLoadArgs()
        {
            return GenerateArgs(pool => new Counts(1L, pool.ConcurrencyLevel, 1_000L));
        }

        [Benchmark]
        [BenchmarkCategory("helios")]
        [ArgumentsSource(nameof(HeliosBenchmarkArgs))]
        public void HeliosBenchmark(IThreadPool<IThreadPoolWorkItem> pool, Counts counts)
        {
            using (var mre = new CountdownEvent(1))
            {
                var workItem = new RemainingWorkItem(mre, counts.Items);
                for (long i = 0; i < counts.Items; i++)
                {
                    pool.UnsafeQueueUserWorkItem(workItem, false);
                }
                mre.Wait();
            }
        }

        public IEnumerable<object[]> HeliosBenchmarkArgs()
        {
            return GenerateArgs(pool => new Counts(1L, 1_000L));
        }

        private static void DrainQueues(IThreadPool<IThreadPoolWorkItem> pool)
        {
                var spinWait = new SpinWait();
                while (pool.PendingWorkItemCount > 0)
                {
                    spinWait.SpinOnce();
                }
        }

        private IEnumerable<object[]> GenerateArgs(params Func<IThreadPool<IThreadPoolWorkItem>, Counts>[] counts)
        {
            foreach (var pool in _pools)
            {
                foreach (var count in counts)
                {
                    yield return new object[] { pool, count(pool) };
                }
            }
        }
    }
}