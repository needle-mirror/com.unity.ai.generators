using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Material.Services.Stores.Actions.Payloads;
using Unity.AI.Material.Services.Stores.Selectors;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Material.Services.Utilities;
using Unity.AI.Material.Windows;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using AssetReferenceExtensions = Unity.AI.Material.Services.Utilities.AssetReferenceExtensions;

namespace Unity.AI.Material.Services.Stores.Actions
{
    static class SessionActions
    {
        public static readonly string slice = "sessions";

        public static readonly AsyncThunkCreatorWithArg<PromotedGenerationData> promoteGeneration = new($"{slice}/promoteGeneration", async (data, api) =>
        {
            var originalMaterialResult = data.result;
            if (!originalMaterialResult.IsValid() || !originalMaterialResult.uri.IsFile || originalMaterialResult.IsFailed())
                return;
            var destFileName = data.asset.GetPath();

            // clone the original asset
            if (!AssetUtils.supportedExtensions.Contains(Path.GetExtension(destFileName).ToLowerInvariant()))
                destFileName = Path.ChangeExtension(destFileName, AssetUtils.materialExtension);
            destFileName = AssetDatabase.GenerateUniqueAssetPath(destFileName);
            AssetDatabase.CopyAsset(data.asset.GetPath(), destFileName);

            await PromoteGenerationAsync(data, destFileName, api);
        });

        public static readonly AsyncThunkCreatorWithArg<PromotedGenerationData> promoteGenerationToMaterial = new($"{slice}/promoteGenerationToMaterial", async (data, api) =>
        {
            var originalMaterialResult = data.result;
            if (!originalMaterialResult.IsValid() || !originalMaterialResult.uri.IsFile || originalMaterialResult.IsFailed())
                return;
            var destFileName = data.asset.GetPath();

            // clone or mutate the original asset
            destFileName = Path.ChangeExtension(destFileName, AssetUtils.materialExtension);
            destFileName = AssetDatabase.GenerateUniqueAssetPath(destFileName);
            if (AssetDatabase.GetMainAssetTypeAtPath(data.asset.GetPath()) == typeof(UnityEngine.Material))
                AssetDatabase.CopyAsset(data.asset.GetPath(), destFileName);
            else
                AssetUtils.CreateBlankMaterial(destFileName);

            await PromoteGenerationAsync(data, destFileName, api);
        });

        public static readonly AsyncThunkCreatorWithArg<PromotedGenerationData> promoteGenerationToTerrainLayer = new($"{slice}/promoteGenerationToTerrainLayer", async (data, api) =>
        {
            var originalMaterialResult = data.result;
            if (!originalMaterialResult.IsValid() || !originalMaterialResult.uri.IsFile || originalMaterialResult.IsFailed())
                return;
            var destFileName = data.asset.GetPath();

            // clone or mutate the original asset
            destFileName = Path.ChangeExtension(destFileName, AssetUtils.terrainLayerExtension);
            destFileName = AssetDatabase.GenerateUniqueAssetPath(destFileName);
            if (AssetDatabase.GetMainAssetTypeAtPath(data.asset.GetPath()) == typeof(UnityEngine.TerrainLayer))
                AssetDatabase.CopyAsset(data.asset.GetPath(), destFileName);
            else
                AssetUtils.CreateBlankTerrainLayer(destFileName);

            await PromoteGenerationAsync(data, destFileName, api);
        });

        static async Task PromoteGenerationAsync(PromotedGenerationData data, string destFileName, IStoreApi api)
        {
            var originalMaterialResult = data.result;
            var promotedMaterialResult = MaterialResult.FromPath(originalMaterialResult.uri.GetLocalPath());
            var promotedAsset = new AssetReference { guid = AssetDatabase.AssetPathToGUID(destFileName) };

            var generativePath = promotedAsset.GetGeneratedAssetsPath();
            await promotedMaterialResult.CopyToProject(promotedMaterialResult.GetName(), await originalMaterialResult.GetMetadata(), generativePath);

            Selection.activeObject = promotedAsset.GetObject();
            MaterialGeneratorWindow.Display(destFileName);

            await api.Dispatch(GenerationResultsActions.selectGeneration, new(promotedAsset, promotedMaterialResult, true, false));
        }

        public static readonly Func<(DragAndDropGenerationData data, IStoreApi api), AssetReference> promoteGenerationUnsafe = args =>
        {
            var originalMaterialResult = args.data.result;
            if (!originalMaterialResult.IsValid() || !originalMaterialResult.uri.IsFile || originalMaterialResult.IsFailed())
                return new AssetReference();
            var destFileName = args.data.newAssetPath;

            // clone the original asset
            if (!AssetUtils.supportedExtensions.Contains(Path.GetExtension(destFileName).ToLowerInvariant()))
                destFileName = Path.ChangeExtension(destFileName, AssetUtils.materialExtension);
            AssetDatabase.CopyAsset(args.data.asset.GetPath(), destFileName);

            var promotedMaterialResult = MaterialResult.FromPath(originalMaterialResult.uri.GetLocalPath());
            var promotedAsset = new AssetReference { guid = AssetDatabase.AssetPathToGUID(destFileName) };
            var generativePath = promotedAsset.GetGeneratedAssetsPath();

            // async because it can take 4s easily to copy and import all the maps
            _ = SaveToProjectUnsafe();

            return promotedAsset;

            async Task SaveToProjectUnsafe()
            {
                await promotedMaterialResult.CopyToProject(promotedMaterialResult.GetName(), await originalMaterialResult.GetMetadata(), generativePath);

                // forcibly overwrites the asset, only ok when we create a new asset (as here)
                GenerationResultsActions.Replace(args.api.State, promotedAsset, promotedMaterialResult, args.api.State.SelectGeneratedMaterialMapping(args.data.asset));

                // set late because asset import clears the selection
                args.api.Dispatch(GenerationResultsActions.setSelectedGeneration, new PromotedGenerationData(promotedAsset, promotedMaterialResult));
            }
        };

        public static readonly Func<(DragAndDropFinalizeData data, IStoreApi api), string> moveAssetDependencies = args =>
        {
            var tempDropAsset = new AssetReference { guid = AssetDatabase.AssetPathToGUID(args.data.tempNewAssetPath) };

            // Get the maps path.
            var sourceMapsPath = tempDropAsset.GetMapsPath();
            var deatinationMapsPath = AssetReferenceExtensions.GetMapsPath(args.data.newAssetPath);
            if (!AssetDatabase.IsValidFolder(deatinationMapsPath))
            {
                deatinationMapsPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(Path.GetDirectoryName(deatinationMapsPath), Path.GetFileName(deatinationMapsPath)));
                if (string.IsNullOrEmpty(deatinationMapsPath))
                {
                    Debug.LogError("Failed to create new folder for material maps.");
                    return args.data.newAssetPath;
                }
            }

            // Get all the dependencies of the main asset.
            var dependencyPaths = AssetDatabase.GetDependencies(tempDropAsset.GetPath(), true);
            foreach (var dependencyPath in dependencyPaths)
            {
                // Skip the main asset file itself.
                if (string.Equals(dependencyPath, tempDropAsset.GetPath(), StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip assets that are not children of the maps path.
                if (!FileIO.IsFileDirectChildOfFolder(sourceMapsPath, dependencyPath))
                    continue;

                var destFilePath = Path.Combine(deatinationMapsPath, Path.GetFileName(dependencyPath));
                AssetDatabase.MoveAsset(dependencyPath, destFilePath);
            }

            return args.data.newAssetPath;
        };

        public static Creator<float> setPreviewSizeFactor => new($"{slice}/setPreviewSizeFactor");
    }
}
