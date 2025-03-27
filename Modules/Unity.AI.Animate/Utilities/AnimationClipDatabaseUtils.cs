using System;
using System.IO;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine;

namespace Unity.AI.Animate.Services.Utilities
{
    static class AnimationClipDatabaseUtils
    {
        public struct SerializedData
        {
            public byte[] data;
            public string fileName;
        }

        /// <summary>
        /// Serializes an AnimationClip to a byte array via a temporary asset.
        /// Also returns the temporary asset filename that was used.
        /// </summary>
        public static SerializedData SerializeAnimationClip(AnimationClip clip)
        {
            if (!clip)
            {
                Debug.LogError("No data to serialize from AnimationClip.");
                return default;
            }

            using var temporaryAsset = TemporaryAssetUtilities.ImportAssets(new[] { clip });
            var fileName = temporaryAsset.assets[0].asset.GetPath();
            var bytes = FileIO.ReadAllBytes(temporaryAsset.assets[0].asset.GetPath());
            return new SerializedData { data = bytes, fileName = fileName };
        }

        /// <summary>
        /// Deserializes the byte array back into an AnimationClip using the provided filename.
        /// </summary>
        public static AnimationClip DeserializeAnimationClip(string fileName, byte[] data)
        {
            if (data == null)
            {
                Debug.LogError("No data to deserialize to an AnimationClip.");
                return null;
            }

            using var temporaryAsset = TemporaryAssetUtilities.ImportAssets(new[] { (fileName, data) });
            var animationClip = temporaryAsset.assets[0].asset.GetObject<AnimationClip>();
            return UnityEngine.Object.Instantiate(animationClip);
        }
    }
}
