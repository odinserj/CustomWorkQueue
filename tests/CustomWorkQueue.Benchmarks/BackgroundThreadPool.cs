using System;
using System.Threading;

namespace CustomWorkQueue.Benchmarks
{
    internal sealed class CustomThreadPool : IThreadPool<IThreadPoolWorkItem>, IDisposable
    {
        private static readonly Action<IThreadPoolWorkItem, CancellationToken> Empty = (work, cte) =>
        {
            work.Execute();
        };

        private readonly CustomWorkQueue<IThreadPoolWorkItem> _workQueue;
        private readonly Thread[] _threads;

        public CustomThreadPool(int threadCount)
        {
            _workQueue = new CustomWorkQueue<IThreadPoolWorkItem>();
            _threads = new Thread[threadCount];

            for (var i = 0; i < threadCount; i++)
            {
                _threads[i] = new Thread(RunThread)
                {
                    IsBackground = true,
                    Name = $"BackgroundWorker:{i + 1}"
                };

                _threads[i].Start();
            }
        }

        public long PendingWorkItemCount => _workQueue.PendingWorkItemCount;
        public int ConcurrencyLevel => _threads.Length;

        public void UnsafeQueueUserWorkItem(IThreadPoolWorkItem work, bool preferLocal)
        {
            _workQueue.UnsafeQueueUserWorkItem(work, preferLocal);
        }

        public override string ToString()
        {
            return $"BackgroundT{_threads.Length}";
        }

        public void Dispose()
        {
            foreach (var thread in _threads)
            {
                thread.Join();
            }
        }

        private void RunThread()
        {
            _workQueue.Dispatch(Empty, CancellationToken.None);
        }
    }
}