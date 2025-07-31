using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.AI.Generators.UI.Utilities
{
    static class SkeletonExtensions
    {
        static int s_NextInternalId = 1;
        static readonly Queue<int> k_AvailableInternalIds = new();
        static readonly Dictionary<int, int> k_ExternalToInternalMap = new();
        internal const string skeletonUriPath = "file:///skeletons";

        public static void Acquire(int externalId)
        {
            var internalId = k_AvailableInternalIds.Count > 0 ? k_AvailableInternalIds.Dequeue() : s_NextInternalId++;
            k_ExternalToInternalMap[externalId] = internalId;
        }

        public static void Release(int externalId)
        {
            if (!k_ExternalToInternalMap.TryGetValue(externalId, out var internalId))
                return;
            k_AvailableInternalIds.Enqueue(internalId);
            k_ExternalToInternalMap.Remove(externalId);
        }

        public static int Peek(int externalId) => k_ExternalToInternalMap.GetValueOrDefault(externalId, 0);
    }

    static class SkeletonRenderingUtils
    {
        static Material s_ProgressDiskMaterial;

        static readonly Dictionary<Tuple<int, int, float>, RenderTexture> k_Cache = new();

        public static RenderTexture GetTemporary(float progress, int width, int height, float screenScaleFactor)
        {
            var texWidth = Mathf.RoundToInt(width * screenScaleFactor);
            var texHeight = Mathf.RoundToInt(height * screenScaleFactor);

            texWidth = Mathf.NextPowerOfTwo(Mathf.Clamp(texWidth, 128, 8191));
            texHeight = Mathf.NextPowerOfTwo(Mathf.Clamp(texHeight, 128, 8191));

            var bucketedProgress = progress <= 0 ? 0 : Mathf.Clamp(Mathf.Round(progress / 0.05f) * 0.05f, 0.05f, 1f);

            var key = Tuple.Create(texWidth, texHeight, bucketedProgress);
            if (k_Cache.TryGetValue(key, out var cached) && cached)
                return cached;

            var rt = RenderTexture.GetTemporary(texWidth, texHeight, 0);
            if (!s_ProgressDiskMaterial)
                s_ProgressDiskMaterial = new Material(Shader.Find("Hidden/AIToolkit/ProgressDisk")) { hideFlags = HideFlags.HideAndDontSave };

            var previousRT = RenderTexture.active;
            try
            {
                s_ProgressDiskMaterial.SetFloat("_Value", bucketedProgress);
                Graphics.Blit(null, rt, s_ProgressDiskMaterial);
            }
            finally
            {
                RenderTexture.active = previousRT;
            }

            k_Cache.Add(key, rt);
            return rt;
        }
    }

    record FulfilledSkeleton(int progressTaskID, string resultUri);
}
