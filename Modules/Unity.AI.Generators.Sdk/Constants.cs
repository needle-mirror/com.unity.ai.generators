using System;

namespace Unity.AI.Generators.Sdk
{
    static class Constants
    {
        public static readonly TimeSpan noTimeout = TimeSpan.FromDays(1);
        public static readonly TimeSpan realtimeTimeout = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan mandatoryTimeout = TimeSpan.FromSeconds(60);
    }
}
