using System;
using System.Threading.Tasks;
using Unity.AI.Animate.Services.Stores.States;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine;

namespace Unity.AI.Animate.Services.Utilities
{
    record TextureSkeleton(int taskID, int counter) : AnimationClipResult(new Uri($"{SkeletonExtensions.skeletonUriPath}/{taskID}/{counter}", UriKind.Absolute));

    static class TextureSkeletonExtensions
    {
        public static async Task<RenderTexture> GetPreview(
            this TextureSkeleton skeleton,
            float screenScaleFactor = 1f)
        {
            // ignore size and counter and remap the taskID
            return await Unity.AI.Generators.UI.Utilities.TextureCache.GetPreview(
                new Uri($"{SkeletonExtensions.skeletonUriPath}/{SkeletonExtensions.Peek(skeleton.taskID)}", UriKind.Absolute),
                Mathf.RoundToInt((int)TextureSizeHint.Carousel * screenScaleFactor));
        }
    }    
}
