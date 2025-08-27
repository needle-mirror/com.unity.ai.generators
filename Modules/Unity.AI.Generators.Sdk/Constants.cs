using System;

namespace Unity.AI.Generators.Sdk
{
    static class Constants
    {
        public static readonly TimeSpan noTimeout = TimeSpan.FromDays(1);
        public static readonly TimeSpan realtimeTimeout = TimeSpan.FromMinutes(2);
        public static readonly TimeSpan generateTimeout = TimeSpan.FromSeconds(30);

        public static readonly TimeSpan motionDownloadCreateUrlRetryTimeout = TimeSpan.FromSeconds(90);
        public static readonly TimeSpan imageDownloadCreateUrlRetryTimeout = TimeSpan.FromSeconds(45);
        public static readonly TimeSpan soundDownloadCreateUrlRetryTimeout = TimeSpan.FromSeconds(45);
        public static readonly TimeSpan statusCheckCreateUrlRetryTimeout = TimeSpan.FromSeconds(5);
        public const int retryCount = 15;

        public static readonly TimeSpan modelsFetchTimeout = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan referenceUploadCreateUrlTimeout = TimeSpan.FromSeconds(45);
    }
}
