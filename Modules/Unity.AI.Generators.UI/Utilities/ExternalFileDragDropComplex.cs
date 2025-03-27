using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Generators.Asset;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Generators.UI.Utilities
{
    record CopyFunctionData(string sourcePath, string destinationPath);
    record MoveFunctionData(string sourcePath, string destinationPath);
    static class ExternalFileDragDropComplex
    {
        // Cache external file paths to Unity asset GUIDs for reuse of temporary assets
        static readonly Dictionary<string, string> k_TemporaryAssetCache = new();

        [InitializeOnLoadMethod]
        static void Init() => DragAndDrop.AddDropHandler(HandleDropProjectBrowser);

        static bool HasTemporaryAssetInDrag()
        {
            if (DragAndDrop.GetGenericData(ExternalFileDragDropConstants.handlerType) as string != nameof(ExternalFileDragDropComplex))
                return false;
            return !string.IsNullOrEmpty(DragAndDrop.GetGenericData(ExternalFileDragDropConstants.tempAssetPath) as string);
        }

        // Creates a temporary asset at drag start; if dropped in Project, it's moved at drop time.
        public static void StartDragFromExternalPath(string externalFilePath, string dropFileName = "",
            Func<CopyFunctionData, string> copyFunction = null,
            Func<MoveFunctionData, string> moveDependencies = null)
        {
            if (!File.Exists(externalFilePath))
            {
                Debug.LogError("External file path not found: " + externalFilePath);
                return;
            }

            // if a dropFileName was provided without extension, use the extension of the external file
            if (!string.IsNullOrEmpty(dropFileName) && string.IsNullOrEmpty(Path.GetExtension(dropFileName)))
            {
                var extension = Path.GetExtension(externalFilePath);
                dropFileName = Path.ChangeExtension(dropFileName, extension);
            }

            var createdAsset = CreateTemporaryAssetInProject(externalFilePath, dropFileName, out var cacheHit, copyFunction);
            if (!createdAsset)
                return;

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.paths = new[] { AssetDatabase.GetAssetPath(createdAsset) };
            DragAndDrop.objectReferences = new[] { createdAsset };

            // Store the temporary asset path and optional dropFileName for final location
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.handlerType, nameof(ExternalFileDragDropComplex));
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.tempAssetPath, AssetDatabase.GetAssetPath(createdAsset));
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.dropFileName, dropFileName);
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.externalFilePath, externalFilePath);
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.cacheHit, cacheHit);
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.moveDepsFun, moveDependencies);

            DragAndDrop.StartDrag("Promote Generation to Project");
        }

        static DragAndDropVisualMode HandleDropProjectBrowser(int dragInstanceId, string dropUponPath, bool perform)
        {
            if (!HasTemporaryAssetInDrag())
                return DragAndDropVisualMode.None;

            if (perform)
            {
                DragAndDrop.AcceptDrag();
                OnProjectWindowDragPerform(dropUponPath, MoveFunction());
                ClearGenericData();
            }

            return DragAndDropVisualMode.Copy;

            Func<MoveFunctionData, string> MoveFunction() => DragAndDrop.GetGenericData(ExternalFileDragDropConstants.moveDepsFun) as Func<MoveFunctionData, string>;
        }

        /// <summary>
        /// Move the temporary asset to its final location in the Project
        /// </summary>
        /// <param name="dropTargetPath"></param>
        /// <param name="moveDependenciesFunction"></param>
        static void OnProjectWindowDragPerform(string dropTargetPath, Func<MoveFunctionData, string> moveDependenciesFunction)
        {
            var tempPath = DragAndDrop.GetGenericData(ExternalFileDragDropConstants.tempAssetPath) as string;
            if (string.IsNullOrEmpty(tempPath))
                return;

            var fileName = Path.GetFileName(tempPath);
            var extension = Path.GetExtension(tempPath);

            var dropFileName = DragAndDrop.GetGenericData(ExternalFileDragDropConstants.dropFileName) as string;
            if (!string.IsNullOrEmpty(dropFileName))
                fileName = Path.ChangeExtension(dropFileName, extension);

            if (string.IsNullOrEmpty(dropTargetPath))
                dropTargetPath = "Assets";

            string newPath;
            if (AssetDatabase.IsValidFolder(dropTargetPath))
                newPath = Path.Combine(dropTargetPath, fileName);
            else
            {
                var folderPath = Path.GetDirectoryName(dropTargetPath);
                newPath = Path.Combine(folderPath ?? "Assets", fileName);
            }

            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
            // If the asset was already use before, copy it instead of moving it
            var cacheHit = (bool)DragAndDrop.GetGenericData(ExternalFileDragDropConstants.cacheHit);
            if (cacheHit)
                AssetDatabase.CopyAsset(tempPath, newPath);
            else
            {
                if (moveDependenciesFunction != null)
                    newPath = moveDependenciesFunction(new MoveFunctionData(tempPath, newPath));
                AssetDatabase.MoveAsset(tempPath, newPath);
            }
            AssetDatabase.Refresh();

            // Update the cache so that the new asset is the cached version.
            var externalFilePath = DragAndDrop.GetGenericData(ExternalFileDragDropConstants.externalFilePath) as string;
            if (!string.IsNullOrEmpty(externalFilePath))
            {
                var newAssetGuid = AssetDatabase.AssetPathToGUID(newPath);
                k_TemporaryAssetCache[externalFilePath] = newAssetGuid;
            }
        }

        static void ClearGenericData()
        {
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.handlerType, null);
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.tempAssetPath, null);
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.dropFileName, null);
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.externalFilePath, null);
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.cacheHit, null);
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.moveDepsFun, null);
        }

        static Object CreateTemporaryAssetInProject(string externalPath, string newFileName, out bool cacheHit, Func<CopyFunctionData, string> copyFunction = null)
        {
            cacheHit = false;
            if (k_TemporaryAssetCache.TryGetValue(externalPath, out var cachedGuid))
            {
                var cachedPath = AssetDatabase.GUIDToAssetPath(cachedGuid);
                var cachedAsset = AssetDatabase.LoadAssetAtPath<Object>(cachedPath);
                if (cachedAsset != null)
                {
                    cacheHit = true;
                    return cachedAsset;
                }
                k_TemporaryAssetCache.Remove(externalPath);
            }

            newFileName = Path.GetFileName(!string.IsNullOrEmpty(newFileName) ? newFileName : externalPath);

            var newPath = Path.Combine("Assets", newFileName);
            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);

            if (copyFunction != null)
                newPath = copyFunction(new CopyFunctionData(externalPath, newPath));
            else
                File.Copy(externalPath, newPath);
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<Object>(newPath);
            if (!asset)
            {
                Debug.LogError($"Failed to load newly created asset at '{newPath}'");
                return null;
            }

            asset.EnableGenerationLabel();

            var assetGuid = AssetDatabase.AssetPathToGUID(newPath);
            k_TemporaryAssetCache[externalPath] = assetGuid;
            return asset;
        }
    }
}
