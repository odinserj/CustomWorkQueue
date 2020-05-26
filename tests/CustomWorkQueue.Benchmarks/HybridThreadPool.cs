using System;
using System.Threading;

namespace CustomWorkQueue.Benchmarks
{
    internal sealed class HybridThreadPool : IThreadPool<IThreadPoolWorkItem>, IDisposable
    {
        private static readonly Action<IThreadPoolWorkItem, CancellationToken> Empty = (work, cte) =>
        {
            work.Execute();
        };

        private const int WorkQueueCount = 4;
        private readonly HybridWorkQueue<IThreadPoolWorkItem>[] _workQueues;
        private readonly Thread[] _threads;

        public HybridThreadPool(int threadCount)
        {
            var queues = new HybridWorkQueue<IThreadPoolWorkItem>[WorkQueueCount];
            for (var i = 0; i < queues.Length; i++)
            {
                queues[i] = new HybridWorkQueue<IThreadPoolWorkItem>(queues, i);
            }

            _workQueues = queues;
            _threads = new Thread[threadCount];

            for (var i = 0; i < threadCount; i++)
            {
                _threads[i] = new Thread(RunThread)
                {
                    IsBackground = true,
                    Name = $"HybridWorker:{i + 1}"
                };

                _threads[i].Start((i / WorkQueueCount) % WorkQueueCount);
            }
        }

        public long PendingWorkItemCount
        {
            get
            {
                var count = 0L;
                foreach (var workQueue in _workQueues)
                {
                    count += workQueue.PendingWorkItemCount;
                }
                return count;
            }
        }

        public int ConcurrencyLevel => _threads.Length;

        [ThreadStatic]
        private static CustomWorkQueueBase<IThreadPoolWorkItem>.WorkQueueLocals Locals;

        private uint _nextQueue;

        public void UnsafeQueueUserWorkItem(IThreadPoolWorkItem work, bool preferLocal)
        {
            var locals = Locals;
            CustomWorkQueueBase<IThreadPoolWorkItem> nonSignaled = null;
            if (locals != null)
            {
                if (preferLocal)
                {
                    locals.Queue.LocalPush(work);
                    locals._workQueue.SignalOneThread();
                    return;
                }

                locals._workQueue._queue.Enqueue(work);
                if (!locals._workQueue.SignalOneThread()) nonSignaled = locals._workQueue;
            }
            else
            {
                var workQueue = _workQueues[_nextQueue / 32 % WorkQueueCount];
                _nextQueue = unchecked(_nextQueue + 1);

                workQueue._queue.Enqueue(work);
                if (!workQueue.SignalOneThread()) nonSignaled = workQueue;
            }

            /*if (nonSignaled != null)
            {
                foreach (var workQueue in _workQueues)
                {
                    if (workQueue != nonSignaled && workQueue.SignalOneThread()) break;
                }
            }*/
        }

        public override string ToString()
        {
            return $"HybridT{_threads.Length}";
        }

        public void Dispose()
        {
            foreach (var thread in _threads)
            {
                thread.Join();
            }
        }

        private void RunThread(object state)
        {
            var index = (int) state;
            var workQueue = _workQueues[index];

            using var locals = workQueue.CreateLocals();
            try
            {
                Locals = locals;

                var waitAdded = false;
                var spinWait = new SpinWait();

                while (true)
                {
                    if (workQueue.TryDequeue(locals, out var work, out var missedSteal))
                    {
                        if (!waitAdded) workQueue.SignalOneThread();

                        do
                        {
                            work.Execute();
                        } while (workQueue.TryDequeue(locals, out work, out missedSteal));
                    }

                    if (!waitAdded)
                    {
                        workQueue.AddWaitNode(locals.WaitNode, ref spinWait);
                        waitAdded = true;
                        continue;
                    }

                    if (missedSteal)
                    {
                        spinWait.SpinOnce();
                        continue;
                    }

                    locals.Semaphore.Wait();
                    spinWait.Reset();
                    waitAdded = false;
                }
            }
            finally
            {
                Locals = null;
            }
        }
    }
}