using System;

namespace CustomWorkQueue
{
    internal static class CustomWorkQueueNonGenericStore
    {
        [ThreadStatic]
        internal static object LocalQueue;
    }
}