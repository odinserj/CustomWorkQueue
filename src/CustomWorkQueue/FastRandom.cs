// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// https://github.com/dotnet/runtime/blob/6c4533b612a629e0b1cd0a5619aaaeabfe7fd228/src/libraries/System.Private.CoreLib/src/System/Threading/ThreadPool.cs

using System.Diagnostics;

namespace CustomWorkQueue
{
    // Simple random number generator. We don't need great randomness, we just need a little and for it to be fast.
    public struct FastRandom // xorshift prng
    {
        private uint _w, _x, _y, _z;

        public FastRandom(int seed)
        {
            _x = (uint)seed;
            _w = 88675123;
            _y = 362436069;
            _z = 521288629;
        }

        public int Next(int maxValue)
        {
            Debug.Assert(maxValue > 0);

            uint t = _x ^ (_x << 11);
            _x = _y; _y = _z; _z = _w;
            _w = _w ^ (_w >> 19) ^ (t ^ (t >> 8));

            return (int)(_w % (uint)maxValue);
        }
    }
}