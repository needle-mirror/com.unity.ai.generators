using System;
using Unity.AI.Image.Services.SessionPersistence;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Toolkit;
using UnityEditor;

namespace Unity.AI.Image.Services.Stores.Actions
{
    static class AppActions
    {
        internal static Creator<AppData> init => new("init");
        internal static Creator<AssetReference> deleteAsset => new("deleteAsset");
    }

    /// <summary>
    /// remove the redux state for an asset once deleted
    /// </summary>
    class AssetReferenceDeletionProcessor : AssetModificationProcessor
    {
        static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var asset = new AssetReference { guid = guid };

            DelayedDispatch(asset);
            return AssetDeleteResult.DidNotDelete;

            // once file is actually deleted
            async void DelayedDispatch(AssetReference assetReference)
            {
                await EditorTask.Yield();
                SharedStore.Store.Dispatch(AppActions.deleteAsset, assetReference);
            }
        }
    }

    /// <summary>
    /// reset auto replace flag on asset reimport (external modification), this doesn't get trigger when we replace ourselves because we get and re-set the flag on replace
    /// </summary>
    class AssetReferenceReimportMonitor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                var asset = new AssetReference { guid = AssetDatabase.AssetPathToGUID(path) };
                var generationResults = SharedStore.Store.State.SelectGenerationResults();
                if (generationResults.generationResults.ContainsKey(asset))
                {
                    SharedStore.Store.Dispatch(GenerationResultsActions.setReplaceWithoutConfirmation, new ReplaceWithoutConfirmationData(asset, false));
                    SharedStore.Store.Dispatch(GenerationResultsActions.setSelectedGeneration, new SelectedGenerationData(asset, new TextureResult()));
                }
            }
        }
    }
}
