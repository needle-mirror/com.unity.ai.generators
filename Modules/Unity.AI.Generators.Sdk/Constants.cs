using System;

namespace Unity.AI.Generators.Sdk
{
    static class Constants
    {
        public static readonly TimeSpan noTimeout = TimeSpan.FromDays(1);
        public static readonly TimeSpan realtimeTimeout = TimeSpan.FromMinutes(2);
        public static readonly TimeSpan mandatoryTimeout = TimeSpan.FromMinutes(5);

        public static readonly TimeSpan motionRetryTimeout = TimeSpan.FromSeconds(90);
        public static readonly TimeSpan imageRetryTimeout = TimeSpan.FromSeconds(45);
        public static readonly TimeSpan soundRetryTimeout = TimeSpan.FromSeconds(45);
        public const int retryCount = 9;

        public static readonly TimeSpan modelsFetchTimeout = TimeSpan.FromSeconds(30);
    }
}
