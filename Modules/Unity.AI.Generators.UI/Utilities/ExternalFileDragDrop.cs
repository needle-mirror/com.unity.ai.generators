using System;
using System.IO;
using Unity.AI.Generators.Asset;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Generators.UI.Utilities
{
    static class ExternalFileDragDropConstants
    {
        public const string handlerType = "ai_toolkit_drag_and_drop_handler_type";
        public const string dropFileName = "ai_toolkit_drag_and_drop_file_name";
        public const string tempAssetPath = "ai_toolkit_drag_and_drop_temporary_asset_path";
        public const string externalFilePath = "ai_toolkit_drag_and_drop_external_file_path";
        public const string cacheHit = "ai_toolkit_drag_and_drop_cache_hit";
        public const string moveDepsFun = "ai_toolkit_drag_and_drop_move_deps_fun";
    }

    static class ExternalFileDragDrop
    {
        static readonly string k_TemporaryAssetDirectory = Path.Combine("Assets", "AI Toolkit", "Temp");
        static readonly string k_CacheDirectory = Path.Combine("Assets", "AI Toolkit", "Cache");

        static DragAndDropVisualMode s_PreviousLastVisualMode = DragAndDropVisualMode.None;
        static DragAndDropVisualMode s_LastVisualMode = DragAndDropVisualMode.None;
        static int s_DragOverFrameCount;

        public static bool tempAssetDragged { get; private set; }

        [InitializeOnLoadMethod]
        static void Init()
        {
            DragAndDrop.AddDropHandler(HandleDropHierarchy);
            DragAndDrop.AddDropHandler(HandleDropProjectBrowser);
            GeneratedAssetCache.instance.EnsureSaved();
        }

        static void EnsureDirectoriesExist()
        {
            var parentDirectory = Path.Combine("Assets", "AI Toolkit");
            if (!AssetDatabase.IsValidFolder(parentDirectory))
                AssetDatabase.CreateFolder("Assets", "AI Toolkit");
            if (!AssetDatabase.IsValidFolder(k_TemporaryAssetDirectory))
                AssetDatabase.CreateFolder("Assets/AI Toolkit", "Temp");
            if (!AssetDatabase.IsValidFolder(k_CacheDirectory))
                AssetDatabase.CreateFolder("Assets/AI Toolkit", "Cache");
        }

        static void StopTracking()
        {
            EditorApplication.update -= OnTrackingUpdate;
            tempAssetDragged = false;
        }

        static void StartTracking()
        {
            tempAssetDragged = true;
            EditorApplication.update += OnTrackingUpdate;

            s_PreviousLastVisualMode = DragAndDrop.visualMode;
            s_LastVisualMode = DragAndDrop.visualMode;
        }

        static bool HasTemporaryAssetInDrag()
        {
            if (DragAndDrop.GetGenericData(ExternalFileDragDropConstants.handlerType) as string != nameof(ExternalFileDragDrop))
                return false;
            return !string.IsNullOrEmpty(DragAndDrop.GetGenericData(ExternalFileDragDropConstants.tempAssetPath) as string);
        }

        static void OnTrackingUpdate()
        {
            // When a UI element accepts the drag, and the mouse is released, objects are dropped and
            // cleared from DragAndDrop.objectReferences.
            // OnTrackingUpdate() will always know about the end of a drag operation after a RegisterValueChangedCallback
            // for an element that accepts the drag.
            // If the drag is canceled, OnTrackingUpdate() will also know about it
            // by monitoring the previous frame DragAndDrop visual mode.
            var hasTemporaryAssetInDrag = HasTemporaryAssetInDrag();
            if (!hasTemporaryAssetInDrag || DragAndDrop.objectReferences is not {Length: > 0})
            {
                // Note that having no more objectReferences doesn't mean the drag is over.
                // There's a mysterious thing in imGUI when you drag items from a window to another where
                // the objectReferences are cleared for 2 frames but the drag is still ongoing.
                // We need to wait for 3 frames to be sure the drag is over.
                s_DragOverFrameCount++;
                if (s_DragOverFrameCount < 3)
                    return;

                StopTracking();
                // Note that accepting/canceling the drag does not clear the DragAndDrop GenericData hash table.
                if (hasTemporaryAssetInDrag)
                {
                    var accepted = s_PreviousLastVisualMode is not (DragAndDropVisualMode.None or DragAndDropVisualMode.Rejected);
                    if (!accepted)
                    {
                        var assetPath = DragAndDrop.GetGenericData(ExternalFileDragDropConstants.tempAssetPath) as string;
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            AssetDatabase.DeleteAsset(assetPath);
                        }
                    }
                    ClearGenericData();
                }
                return;
            }

            s_PreviousLastVisualMode = s_LastVisualMode;
            s_LastVisualMode = DragAndDrop.visualMode;
            s_DragOverFrameCount = 0;
        }

        // Creates a temporary asset at drag start; if dropped in Project, it's moved at drop time.
        public static void StartDragFromExternalPath(string externalFilePath, string dropFileName = "")
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

            var createdAsset = CreateTemporaryAssetInProject(externalFilePath, dropFileName, out var cacheHit);
            if (!createdAsset)
                return;

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.paths = Array.Empty<string>();
            DragAndDrop.objectReferences = new[] { createdAsset };

            // Store the temporary asset path and optional dropFileName for final location
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.handlerType, nameof(ExternalFileDragDrop));
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.tempAssetPath, AssetDatabase.GetAssetPath(createdAsset));
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.dropFileName, dropFileName);
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.externalFilePath, externalFilePath);
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.cacheHit, cacheHit);
            DragAndDrop.SetGenericData(ExternalFileDragDropConstants.moveDepsFun, null);

            DragAndDrop.StartDrag("Promote Generation to Project");

            StartTracking();
        }

        static DragAndDropVisualMode HandleDropProjectBrowser(int dragInstanceId, string dropUponPath, bool perform)
        {
            if (!HasTemporaryAssetInDrag())
                return DragAndDropVisualMode.None;

            if (perform)
            {
                StopTracking();
                DragAndDrop.AcceptDrag();
                OnProjectWindowDragPerform(dropUponPath);
                ClearGenericData();
            }

            return DragAndDropVisualMode.Copy;
        }

        static DragAndDropVisualMode HandleDropHierarchy(int dropTargetInstanceID, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            if (!HasTemporaryAssetInDrag())
                return DragAndDropVisualMode.None;

            if (perform)
            {
                StopTracking();
                DragAndDrop.AcceptDrag();
                ClearGenericData();
            }

            return DragAndDropVisualMode.Copy;
        }

        static void OnProjectWindowDragPerform(string dropTargetPath)
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

            var tempFolder = Path.GetDirectoryName(tempPath);
            var newFolder = Path.GetDirectoryName(newPath);
            var sameFolder = tempFolder == newFolder;

            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
            if (sameFolder)
                return;
            AssetDatabase.MoveAsset(tempPath, newPath);
            AssetDatabase.Refresh();
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

        static Object CreateTemporaryAssetInProject(string externalPath, string newFileName, out bool cacheHit)
        {
            EnsureDirectoriesExist();

            cacheHit = false;
            var currentDirectory = Directory.GetCurrentDirectory();
            var relativeExternalPath = Path.GetRelativePath(currentDirectory, externalPath);
            if (GeneratedAssetCache.instance.assetCacheEntries.TryGetValue(relativeExternalPath, out var cachedGuid))
            {
                var cachedPath = AssetDatabase.GUIDToAssetPath(cachedGuid);
                if (!string.IsNullOrEmpty(cachedPath) && FileIO.AreFilesIdentical(cachedPath, externalPath))
                {
                    var cachedAsset = AssetDatabase.LoadAssetAtPath<Object>(cachedPath);
                    if (cachedAsset != null)
                    {
                        cacheHit = true;
                        return GetTemporaryAsset(cachedAsset);
                    }
                }
                GeneratedAssetCache.instance.assetCacheEntries.Remove(relativeExternalPath);
            }

            newFileName = Path.GetFileName(!string.IsNullOrEmpty(newFileName) ? newFileName : externalPath);
            var newPath = Path.Combine(k_CacheDirectory, newFileName);
            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);

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
            GeneratedAssetCache.instance.assetCacheEntries[relativeExternalPath] = assetGuid;
            GeneratedAssetCache.instance.EnsureSaved();
            return GetTemporaryAsset(asset);
        }

        static Object GetTemporaryAsset(Object cachedAsset)
        {
            var cachedPath = AssetDatabase.GetAssetPath(cachedAsset);
            var fileName = Path.GetFileName(cachedPath);
            var newPath = Path.Combine(k_TemporaryAssetDirectory, fileName);
            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
            AssetDatabase.CopyAsset(cachedPath, newPath);

            return AssetDatabase.LoadAssetAtPath<Object>(newPath);
        }
    }
}
