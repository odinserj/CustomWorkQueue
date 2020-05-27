using System;
using System.Threading;
using System.Threading.Tasks;

namespace CustomWorkQueue
{
    public sealed class CustomWorkQueueAsync<TWorkItem> : CustomWorkQueueBase<TWorkItem>
        where TWorkItem : class
    {
        private static readonly AsyncLocal<WorkStealingQueue<TWorkItem>> LocalQueue = new AsyncLocal<WorkStealingQueue<TWorkItem>>();

        public async Task DispatchAsync(Func<TWorkItem, CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            using var locals = new WorkQueueLocals(this);
            try
            {
                LocalQueue.Value = locals.Queue;

                var waitAdded = false;
                var spinWait = new SpinWait();

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (TryDequeue(locals, out var work, out var missedSteal, waitAdded))
                    {
                        if (!waitAdded) SignalOneThread();

                        do
                        {
                            await action(work, cancellationToken);
                        } while (TryDequeue(locals, out work, out missedSteal, waitAdded));
                    }

                    if (!waitAdded)
                    {
                        AddWaitNode(locals.WaitNode, ref spinWait);
                        waitAdded = true;
                        continue;
                    }

                    if (missedSteal)
                    {
                        spinWait.SpinOnce();
                        continue;
                    }

                    await locals.Semaphore.WaitAsync(cancellationToken);
                    spinWait.Reset();
                    waitAdded = false;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                LocalQueue.Value = null;
            }
        }

        internal override WorkStealingQueue<TWorkItem> GetLocalQueue()
        {
            return LocalQueue.Value;
        }
    }
}