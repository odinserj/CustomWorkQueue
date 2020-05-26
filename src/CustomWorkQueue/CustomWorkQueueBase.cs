using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CustomWorkQueue
{
    public abstract class CustomWorkQueueBase<TWorkItem>
        where TWorkItem : class
    {
        private static readonly WaitNode Tombstone = new WaitNode(null);

        private WorkStealingQueue<TWorkItem>[] _localQueues = new WorkStealingQueue<TWorkItem>[0];
        private readonly ConcurrentQueue<TWorkItem>[] _globalQueues = new ConcurrentQueue<TWorkItem>[4];
        private readonly WaitNode _waitHead = new WaitNode(null);

        public CustomWorkQueueBase()
        {
            for (var i = 0; i < _globalQueues.Length; i++)
            {
                _globalQueues[i] = new ConcurrentQueue<TWorkItem>();
            }
        }

        public long PendingWorkItemCount
        {
            get
            {
                long count = 0L;

                foreach (var globalQueue in _globalQueues)
                {
                    count += globalQueue.Count;
                }

                var localQueues = Volatile.Read(ref _localQueues);

                foreach (var localQueue in localQueues)
                {
                    count += localQueue.Count;
                }

                return count;
            }
        }

        private int _nextGlobal;

        public void UnsafeQueueUserWorkItem(TWorkItem work, bool preferLocal)
        {
            var locals = GetLocals();

            if (locals != null)
            {
                if (preferLocal)
                {
                    locals.Queue.LocalPush(work);
                }
                else
                {
                    locals.PreferredQueue.Enqueue(work);
                }
            }
            else
            {
                _globalQueues[_nextGlobal / 64 % _globalQueues.Length].Enqueue(work);
                _nextGlobal++;
            }

            SignalOneThread();
        }

        internal abstract WorkQueueLocals GetLocals();

        internal bool TryPopCustomWorkItem(TWorkItem workItem)
        {
            var localQueue = GetLocals();
            return localQueue != null && localQueue.Queue.LocalFindAndPop(workItem);
        }

        internal IEnumerable<TWorkItem> GetQueuedWorkItems()
        {
            // Enumerate global queues
            foreach (var globalQueue in _globalQueues)
            {
                foreach (var workItem in globalQueue)
                {
                    yield return workItem;
                }
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

        internal bool TryDequeue(WorkQueueLocals locals, out TWorkItem callback, out bool missedSteal, bool steal)
        {
            var localQueue = locals.Queue;
            missedSteal = false;

            if ((callback = localQueue.LocalPop()) != null ||
                locals.PreferredQueue.TryDequeue(out callback))
            {
                return true;
            }

            int c = _globalQueues.Length - 1;

            int maxIndex = c - 1;
            int i = locals.PreferredIndex;
            while (c > 0)
            {
                i = i < maxIndex ? i + 1 : 0;
                if (_globalQueues[i].TryDequeue(out callback))
                {
                    return true;
                }
                c--;
            }

            if (steal) // then try the global queue
            {
                TrySteal(locals, ref callback, ref missedSteal, localQueue);
            }

            return callback != null;
        }

        private void TrySteal(WorkQueueLocals locals, ref TWorkItem callback, ref bool missedSteal,
            WorkStealingQueue<TWorkItem> localQueue)
        {
            // finally try to steal from another thread's local queue
            var queues = Volatile.Read(ref _localQueues);
            int c = queues.Length;

            int maxIndex = c - 1;
            int i = locals.Random.Next(c);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SignalOneThread()
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

        private int _nextIndex;

        internal sealed class WorkQueueLocals : IDisposable
        {
            private readonly CustomWorkQueueBase<TWorkItem> _workQueue;

            // Should not be readonly
            public FastRandom Random;
            public readonly WorkStealingQueue<TWorkItem> Queue;
            public readonly int PreferredIndex;
            public readonly ConcurrentQueue<TWorkItem> PreferredQueue;
            public readonly SemaphoreSlim Semaphore;
            public readonly WaitNode WaitNode;

            public WorkQueueLocals(CustomWorkQueueBase<TWorkItem> workQueue)
            {
                _workQueue = workQueue;

                Random = new FastRandom(Thread.CurrentThread.ManagedThreadId);
                Queue = new WorkStealingQueue<TWorkItem>();
                PreferredIndex = (Interlocked.Increment(ref _workQueue._nextIndex) - 1) % _workQueue._globalQueues.Length;
                PreferredQueue = _workQueue._globalQueues[PreferredIndex];
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