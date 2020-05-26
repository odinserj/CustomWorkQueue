using System;

namespace CustomWorkQueue
{
    public sealed class HybridWorkQueue<TWorkItem> : CustomWorkQueueBase<TWorkItem>
        where TWorkItem : class
    {
        private readonly HybridWorkQueue<TWorkItem>[] _queues;
        private readonly int _index;

        public HybridWorkQueue(HybridWorkQueue<TWorkItem>[] queues, int index)
        {
            _queues = queues;
            _index = index;
        }

        internal override WorkStealingQueue<TWorkItem> GetLocalQueue()
        {
            throw new NotImplementedException();
        }

        internal override bool TryDequeue(WorkQueueLocals locals, out TWorkItem callback, out bool missedSteal)
        {
            if (base.TryDequeue(locals, out callback, out missedSteal))
            {
                return true;
            }

            foreach (var queue in _queues)
            {
                if (queue != this && queue._queue.TryDequeue(out callback))
                {
                    return true;
                }
            }

            return false;
        }
    }
}