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
            var locals = new WorkQueueLocals(this);
            using var semaphore = new SemaphoreSlim(0, 1);
            var waitNode = new WaitNode(semaphore);
            try
            {
                RegisterLocalQueue(locals.Queue);
                CustomWorkQueueNonGenericStore.Locals = locals;

                var waitAdded = false;
                var spinWait = new SpinWait();

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (TryDequeue(locals, out var work, out var missedSteal, waitAdded))
                    {
                        if (!waitAdded) SignalOneThread();

                        do
                        {
                            action(work, cancellationToken);
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

                    semaphore.Wait(cancellationToken);
                    spinWait.Reset();
                    waitAdded = false;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                UnregisterLocalQueue(locals.Queue);
                CustomWorkQueueNonGenericStore.Locals = null;
            }
        }

        internal override WorkQueueLocals GetLocals()
        {
            return Unsafe.As<WorkQueueLocals>(CustomWorkQueueNonGenericStore.Locals);
        }
    }
}