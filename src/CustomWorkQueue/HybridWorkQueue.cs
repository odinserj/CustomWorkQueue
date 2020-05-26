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

            var c = _queues.Length - 1;
            var maxIndex = c;
            var i = _index + 1;
            while (c > 0)
            {
                i = i < maxIndex ? i + 1 : 0;
                if (_queues[i]._queue.TryDequeue(out callback))
                {
                    return true;
                }

                c--;
            }

            return false;
        }
    }
}