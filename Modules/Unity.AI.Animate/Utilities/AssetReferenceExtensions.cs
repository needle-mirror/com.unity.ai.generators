using System;
using System.IO;
using System.Threading.Tasks;
using Unity.AI.Animate.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Animate.Services.Utilities
{
    static class AssetReferenceExtensions
    {
        public static async Task<bool> ReplaceAsync(this AssetReference asset, AnimationClipResult generatedAnimationClip)
        {
            if (await generatedAnimationClip.CopyToAsync(asset))
            {
                asset.EnableGenerationLabel();
                return true;
            }

            return false;
        }

        public static bool Replace(this AssetReference asset, AnimationClipResult generatedAnimationClip)
        {
            if (generatedAnimationClip.CopyTo(asset))
            {
                asset.EnableGenerationLabel();
                return true;
            }

            return false;
        }

        public static Task<bool> IsBlank(this AssetReference asset) => Task.FromResult(GetObject<AnimationClip>(asset).IsBlank());

        public static AnimationClipResult ToResult(this AssetReference asset) => AnimationClipResult.FromPath(asset.GetPath());

        public static Object GetObject(this AssetReference asset)
        {
            var path = asset.GetPath();
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Object>(path);
        }

        public static T GetObject<T>(this AssetReference asset) where T : Object
        {
            var path = asset.GetPath();
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        public static AssetReference FromObject(Object obj)
        {
            var assetPath = AssetDatabase.GetAssetPath(obj);
            return new AssetReference { guid = AssetDatabase.AssetPathToGUID(assetPath) };
        }

        public static async Task<bool> SaveToGeneratedAssets(this AssetReference asset)
        {
            try
            {
                await asset.ToResult().CopyToProject(new GenerationSetting().MakeMetadata(asset), asset.GetGeneratedAssetsPath());
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
