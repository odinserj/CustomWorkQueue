using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CustomWorkQueue
{
    public class CustomWorkQueueBase<TWorkItem>
        where TWorkItem : class
    {
        private static readonly WaitNode Tombstone = new WaitNode(null);

        private WorkStealingQueue<TWorkItem>[] _localQueues = new WorkStealingQueue<TWorkItem>[0];
        internal readonly ConcurrentQueue<TWorkItem> _queue = new ConcurrentQueue<TWorkItem>();
        private readonly WaitNode _waitHead = new WaitNode(null);

        public long PendingWorkItemCount
        {
            get
            {
                long count = _queue.Count;
                var localQueues = Volatile.Read(ref _localQueues);

                foreach (var localQueue in localQueues)
                {
                    count += localQueue.Count;
                }

                return count;
            }
        }

        public void UnsafeQueueUserWorkItem(TWorkItem work, bool preferLocal)
        {
            var localQueue = preferLocal ? GetLocalQueue() : null;

            if (localQueue != null)
            {
                localQueue.LocalPush(work);
            }
            else
            {
                _queue.Enqueue(work);
            }

            SignalOneThread();
        }

        internal virtual WorkStealingQueue<TWorkItem> GetLocalQueue()
        {
            return null;
        }

        internal bool TryPopCustomWorkItem(TWorkItem workItem)
        {
            var localQueue = GetLocalQueue();
            return localQueue != null && localQueue.LocalFindAndPop(workItem);
        }

        internal IEnumerable<TWorkItem> GetQueuedWorkItems()
        {
            // Enumerate global queue
            foreach (var workItem in _queue)
            {
                yield return workItem;
            }

            var queues = Volatile.Read(ref _localQueues);

            // Enumerate each local queue
            foreach (var wsq in queues)
            {
                if (wsq?.m_array != null)
                {
                    var items = wsq.m_array;
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = items[i].Value;
                        if (item != null)
                        {
                            yield return item;
                        }
                    }
                }
            }
        }

        internal bool TryDequeue(WorkQueueLocals locals, out TWorkItem callback, out bool missedSteal)
        {
            var localQueue = locals.Queue;
            missedSteal = false;

            if ((callback = localQueue.LocalPop()) != null) return true; // first try the local queue
            return TryDequeueGlobalOrSteal(localQueue, ref locals.Random, out callback, ref missedSteal);
        }

        internal bool TryDequeueGlobalOrSteal(
            WorkStealingQueue<TWorkItem> localQueue,
            ref FastRandom random, out TWorkItem callback, ref bool missedSteal)
        {
            if (!_queue.TryDequeue(out callback)) // then try the global queue
            {
                // finally try to steal from another thread's local queue
                var queues = Volatile.Read(ref _localQueues);
                int c = queues.Length;

                if (c > 0)
                {
                    int maxIndex = c - 1;
                    int i = random.Next(c);
                    while (c > 0)
                    {
                        i = i < maxIndex ? i + 1 : 0;
                        var otherQueue = queues[i];
                        if (otherQueue != localQueue && otherQueue.CanSteal)
                        {
                            callback = otherQueue.TrySteal(ref missedSteal);
                            if (callback != null)
                            {
                                break;
                            }
                        }

                        c--;
                    }
                }
            }

            return callback != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SignalOneThread()
        {
            if (Volatile.Read(ref _waitHead.Next) == null) return;
            SignalOneThreadSlow();
        }

        private void SignalOneThreadSlow()
        {
            var node = Interlocked.Exchange(ref _waitHead.Next, null);
            if (node == null) return;

            var tailNode = Interlocked.Exchange(ref node.Next, Tombstone);
            if (tailNode != null)
            {
                var waitHead = _waitHead;
                do
                {
                    waitHead = Interlocked.CompareExchange(ref waitHead.Next, tailNode, null);
                    if (ReferenceEquals(waitHead, Tombstone))
                    {
                        waitHead = _waitHead;
                    }
                } while (waitHead != null);
            }

            node.Value.Release();
        }

        internal void AddWaitNode(WaitNode node, ref SpinWait spinWait)
        {
            var headNext = node.Next = null;
            while (true)
            {
                var newNext = Interlocked.CompareExchange(ref _waitHead.Next, node, headNext);
                if (newNext == headNext) break;

                headNext = node.Next = newNext;
                spinWait.SpinOnce();
            }
        }

        internal WorkQueueLocals CreateLocals()
        {
            return new WorkQueueLocals(this);
        }

        internal sealed class WorkQueueLocals : IDisposable
        {
            public readonly CustomWorkQueueBase<TWorkItem> _workQueue;

            // Should not be readonly
            public FastRandom Random;
            public readonly WorkStealingQueue<TWorkItem> Queue;
            public readonly SemaphoreSlim Semaphore;
            public readonly WaitNode WaitNode;

            public WorkQueueLocals(CustomWorkQueueBase<TWorkItem> workQueue)
            {
                _workQueue = workQueue;

                Random = new FastRandom(Thread.CurrentThread.ManagedThreadId);
                Queue = new WorkStealingQueue<TWorkItem>();
                Semaphore = new SemaphoreSlim(0, 1);
                WaitNode = new WaitNode(Semaphore);

                WorkStealingQueueList.Add(ref _workQueue._localQueues, Queue);
            }

            public void Dispose()
            {
                WorkStealingQueueList.Remove(ref _workQueue._localQueues, Queue);
                Semaphore.Dispose();
            }
        }

        internal sealed class WaitNode
        {
            public WaitNode(SemaphoreSlim value)
            {
                Value = value;
            }

            public readonly SemaphoreSlim Value;
            public WaitNode Next;
        }
    }
}