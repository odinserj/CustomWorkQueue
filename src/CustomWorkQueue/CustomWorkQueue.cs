using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CustomWorkQueue
{
    public sealed class CustomWorkQueue<TWorkItem> : CustomWorkQueueBase<TWorkItem>
        where TWorkItem : class
    {
        public void Dispatch(Action<TWorkItem, CancellationToken> action, CancellationToken cancellationToken)
        {
            using var locals = new WorkQueueLocals(this);
            try
            {
                CustomWorkQueueNonGenericStore.LocalQueue = locals.Queue;

                var waitAdded = false;
                var spinWait = new SpinWait();

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (TryDequeue(locals, out var work, out var missedSteal))
                    {
                        if (!waitAdded) SignalOneThread();

                        do
                        {
                            action(work, cancellationToken);
                        } while (TryDequeue(locals, out work, out missedSteal));
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

                    locals.Semaphore.Wait(cancellationToken);
                    spinWait.Reset();
                    waitAdded = false;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                CustomWorkQueueNonGenericStore.LocalQueue = null;
            }
        }

        internal override WorkStealingQueue<TWorkItem> GetLocalQueue()
        {
            return Unsafe.As<WorkStealingQueue<TWorkItem>>(CustomWorkQueueNonGenericStore.LocalQueue);
        }
    }
}