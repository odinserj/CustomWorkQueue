using System;
using System.Threading;

namespace CustomWorkQueue.Benchmarks
{
    public interface IThreadPool<in T>
    {
        long PendingWorkItemCount { get; }
        int ConcurrencyLevel { get; }

        void UnsafeQueueUserWorkItem(T workItem, bool preferLocal);
    }

    public sealed class ClrThreadPool : IThreadPool<IThreadPoolWorkItem>
    {
#if NET48
        private static readonly WaitCallback CallbackDelegate = Callback;
#endif

        public void UnsafeQueueUserWorkItem(IThreadPoolWorkItem workItem, bool preferLocal)
        {
#if NET48
            ThreadPool.UnsafeQueueUserWorkItem(CallbackDelegate, workItem);
#else
            ThreadPool.UnsafeQueueUserWorkItem(workItem, preferLocal);
#endif
        }

#if NET48
        private static void Callback(object state)
        {
            ((IThreadPoolWorkItem)state).Execute();
        }
#endif

        public long PendingWorkItemCount => ThreadPool.PendingWorkItemCount;
        public int ConcurrencyLevel => Environment.ProcessorCount;

        public override string ToString()
        {
            return $"ClrT{Environment.ProcessorCount}";
        }
    }
}