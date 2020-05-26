using System;
using System.Threading;
using System.Threading.Tasks;

namespace CustomWorkQueue
{
    public sealed class CustomWorkQueueAsync<TWorkItem> : CustomWorkQueueBase<TWorkItem>
        where TWorkItem : class
    {
        private static readonly AsyncLocal<WorkQueueLocals> LocalQueue = new AsyncLocal<WorkQueueLocals>();

        public async Task DispatchAsync(Func<TWorkItem, CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            var locals = new WorkQueueLocals(this);
            using var semaphore = new SemaphoreSlim(0, 1);
            var waitNode = new WaitNode(semaphore);
            try
            {
                RegisterLocalQueue(locals.Queue);
                LocalQueue.Value = locals;

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
                        AddWaitNode(waitNode, ref spinWait);
                        waitAdded = true;
                        continue;
                    }

                    if (missedSteal)
                    {
                        spinWait.SpinOnce();
                        continue;
                    }

                    await semaphore.WaitAsync(cancellationToken);
                    spinWait.Reset();
                    waitAdded = false;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                UnregisterLocalQueue(locals.Queue);
                LocalQueue.Value = null;
            }
        }

        internal override WorkQueueLocals GetLocals()
        {
            return LocalQueue.Value;
        }
    }
}