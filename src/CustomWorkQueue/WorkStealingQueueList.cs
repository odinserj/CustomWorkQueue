// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// https://github.com/dotnet/runtime/blob/6c4533b612a629e0b1cd0a5619aaaeabfe7fd228/src/libraries/System.Private.CoreLib/src/System/Threading/ThreadPool.cs

using System;
using System.Diagnostics;
using System.Threading;

namespace CustomWorkQueue
{
    internal static class WorkStealingQueueList
    {
        public static void Add<TQueue>(ref TQueue[] queues, TQueue queue)
            where TQueue : class
        {
            Debug.Assert(queue != null);
            while (true)
            {
                TQueue[] oldQueues = Volatile.Read(ref queues);
                Debug.Assert(Array.IndexOf(oldQueues, queue) == -1);

                var newQueues = new TQueue[oldQueues.Length + 1];
                Array.Copy(oldQueues, newQueues, oldQueues.Length);
                newQueues[newQueues.Length - 1] = queue;
                if (Interlocked.CompareExchange(ref queues, newQueues, oldQueues) == oldQueues)
                {
                    break;
                }
            }
        }

        public static void Remove<TQueue>(ref TQueue[] queues, TQueue queue)
            where TQueue : class
        {
            Debug.Assert(queue != null);
            while (true)
            {
                TQueue[] oldQueues = Volatile.Read(ref queues);
                if (oldQueues.Length == 0)
                {
                    return;
                }

                int pos = Array.IndexOf(oldQueues, queue);
                if (pos == -1)
                {
                    Debug.Fail("Should have found the queue");
                    return;
                }

                var newQueues = new TQueue[oldQueues.Length - 1];
                if (pos == 0)
                {
                    Array.Copy(oldQueues, 1, newQueues, 0, newQueues.Length);
                }
                else if (pos == oldQueues.Length - 1)
                {
                    Array.Copy(oldQueues, newQueues, newQueues.Length);
                }
                else
                {
                    Array.Copy(oldQueues, newQueues, pos);
                    Array.Copy(oldQueues, pos + 1, newQueues, pos, newQueues.Length - pos);
                }

                if (Interlocked.CompareExchange(ref queues, newQueues, oldQueues) == oldQueues)
                {
                    break;
                }
            }
        }
    }
}