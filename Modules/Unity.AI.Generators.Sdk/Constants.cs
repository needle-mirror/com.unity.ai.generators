using System;

namespace Unity.AI.Generators.Sdk
{
    static class Constants
    {
        public static readonly TimeSpan noTimeout = TimeSpan.FromDays(1);
        public static readonly TimeSpan realtimeTimeout = TimeSpan.FromMinutes(2);
        public static readonly TimeSpan mandatoryTimeout = TimeSpan.FromMinutes(5);
    }
}
