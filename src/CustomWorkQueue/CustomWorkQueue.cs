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
                CustomWorkQueueNonGenericStore.Locals = null;
            }
        }

        internal override WorkQueueLocals GetLocals()
        {
            return Unsafe.As<WorkQueueLocals>(CustomWorkQueueNonGenericStore.Locals);
        }
    }
}