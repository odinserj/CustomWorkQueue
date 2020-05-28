using System.Runtime.InteropServices;
using System.Threading;

namespace CustomWorkQueue.Benchmarks
{
    [StructLayout(LayoutKind.Explicit, Size = 2 * CacheLineSize)]
    internal struct PaddedInt64
    {
        private const int CacheLineSize = 128;

        [FieldOffset(CacheLineSize)]
        internal long Value;
    }

    public sealed class RemainingWorkItem : IThreadPoolWorkItem
    {
        private readonly CountdownEvent _mre;
        private PaddedInt64 _itemsRemaining;

        public RemainingWorkItem(CountdownEvent mre, long itemsRemaining)
        {
            _mre = mre;
            _itemsRemaining = new PaddedInt64 { Value = itemsRemaining };
        }

        public void Execute()
        {
            if (Interlocked.Decrement(
                ref _itemsRemaining.Value) == 0)
                _mre.Signal();
        }
    }

    public sealed class SignalWorkItem : IThreadPoolWorkItem
    {
        private readonly CountdownEvent _countdown;

        public SignalWorkItem(CountdownEvent countdown)
        {
            _countdown = countdown;
        }

        public void Execute()
        {
            _countdown.Signal();
        }
    }

    public sealed class EmptyWorkItem : IThreadPoolWorkItem
    {
        public void Execute()
        {
        }
    }

    internal sealed class SequentialWorkItem : IThreadPoolWorkItem
    {
        private readonly IThreadPool<IThreadPoolWorkItem> _pool;
        private readonly CountdownEvent _signal;
        private PaddedInt64 _count;

        public SequentialWorkItem(IThreadPool<IThreadPoolWorkItem> pool, CountdownEvent signal, long count)
        {
            _pool = pool;
            _signal = signal;
            _count = new PaddedInt64 { Value = count };
        }

        public void Execute()
        {
            if (_count.Value-- > 0)
            {
                _pool.UnsafeQueueUserWorkItem(this, true);
            }
            else
            {
                _signal?.Signal();
            }
        }
    }

    internal sealed class FeedJobsWorkItem : IThreadPoolWorkItem
    {
        private readonly IThreadPool<IThreadPoolWorkItem> _pool;
        private readonly CountdownEvent _countdown;
        private readonly long _count;
        private readonly bool _local;

        public FeedJobsWorkItem(IThreadPool<IThreadPoolWorkItem> pool, CountdownEvent countdown, long count, bool local)
        {
            _pool = pool;
            _countdown = countdown;
            _count = count;
            _local = local;
        }

        public void Execute()
        {
            var remaining = new RemainingWorkItem(_countdown, _count);

            for (var j = 0; j < _count; j++)
            {
                _pool.UnsafeQueueUserWorkItem(remaining, _local);
            }
        }
    }

    internal sealed class SetCompletedEvent : IThreadPoolWorkItem
    {
        private readonly ManualResetEvent _mre;

        public SetCompletedEvent(ManualResetEvent mre)
        {
            _mre = mre;
        }

        public void Execute()
        {
            _mre.Set();
        }
    }
}