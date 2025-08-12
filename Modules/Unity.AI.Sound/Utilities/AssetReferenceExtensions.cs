using System;
using System.Threading.Tasks;
using Unity.AI.Sound.Services.Stores.States;
using Unity.AI.Generators.Asset;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Sound.Services.Utilities
{
    static class AssetReferenceExtensions
    {
        public static async Task<bool> Replace(this AssetReference asset, AudioClipResult generatedAudioClip)
        {
            if (await generatedAudioClip.CopyToAsync(asset.GetPath()))
            {
                asset.EnableGenerationLabel();
                return true;
            }

            return false;
        }

        public static Task<bool> IsBlank(this AssetReference asset) => Task.FromResult(GetObject(asset) is AudioClip { length: < 0.05f });

        public static AudioClipResult ToResult(this AssetReference asset) => AudioClipResult.FromPath(asset.GetPath());

        public static Object GetObject(this AssetReference asset)
        {
            var path = asset.GetPath();
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Object>(path);
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
