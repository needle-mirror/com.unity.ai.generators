using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.AI.Generators.Asset;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using Unity.AI.Toolkit.Compliance;

namespace Unity.AI.Generators.UI.Utilities
{
    static class GeneratedAssetSearchProvider
    {
        [MenuItem("internal:AI Toolkit/Internals/Search Generated Assets (Unity AI)")]
        static void UnityAIAssetSearchMenu() => SearchService.ShowWindow(new SearchContext(SearchService.GetActiveProviders(), $"l:{Legal.UnityAIGeneratedLabel}"));

        [MenuItem("Window/AI/Search Generated Assets (Unity AI)")]
        static void UnityAIAssetSearchWindowMenu() => UnityAIAssetSearchMenu();

        [MenuItem("Assets/Search Generated Assets %&G", false, 10)]
        static void UnityAIAssetSearchAssetsMenu() => UnityAIAssetSearchMenu();

        static readonly string k_GeneratedAssetsPath = Path.GetFullPath(AssetReferenceExtensions.GetGeneratedAssetsRoot());
        static readonly List<string> k_AcceptedExtensions = new() { ".png", ".jpg", ".fbx", ".wav" };

        const string k_ProviderId = "unity.ai.file.search";
        const string k_FilterPrefix = "unityai:";

        [SearchItemProvider]
        static SearchProvider CreateProvider()
        {
            return new(k_ProviderId, AssetReferenceExtensions.GetGeneratedAssetsRoot())
            {
                filterId = k_FilterPrefix,
                priority = 85,
                fetchItems = FetchGeneratedAssets,
                trackSelection = TrackGeneratedAssetSelection,
                fetchThumbnail = FetchThumbnail,
                isEnabledForContextualSearch = () => false,
                active = true,
                showDetails = true
            };
        }

        static Texture2D FetchThumbnail(SearchItem item, SearchContext context) => FetchThumbnail(item.data as string);

        static Texture2D FetchThumbnail(string fileName)
        {
            try
            {
                switch (Path.GetExtension(fileName))
                {
                    case ".png":
                    case ".jpg":
                        var uri = new Uri(fileName, UriKind.Absolute);
                        var texture = TextureCache.GetPreviewTexture(uri, (int)TextureSizeHint.Generation, FetchBaseThumbnail(fileName));
                        if (texture)
                            return texture;
                        break;
                }
            }
            catch { /* ignored */ }

            return FetchBaseThumbnail(fileName);
        }

        static Texture2D FetchBaseThumbnail(string fileName)
        {
            return Path.GetExtension(fileName) switch
            {
                ".png" or ".jpg" => EditorGUIUtility.ObjectContent(null, typeof(Texture2D)).image as Texture2D,
                ".fbx" => EditorGUIUtility.ObjectContent(null, typeof(GameObject)).image as Texture2D,
                ".wav" => EditorGUIUtility.ObjectContent(null, typeof(AudioClip)).image as Texture2D,
                _ => null
            };
        }

        static IEnumerable<SearchItem> FetchGeneratedAssets(SearchContext context, List<SearchItem> itemsToFill, SearchProvider provider)
        {
            try
            {
                var rootPath = k_GeneratedAssetsPath;
                if (!Directory.Exists(rootPath))
                    return null;

                var foundFiles = new List<string>();
                try { foundFiles.AddRange(Directory.GetFiles(rootPath, "*.*", SearchOption.TopDirectoryOnly)); } catch { /* ignored */ }
                try
                {
                    var subDirectories = Directory.GetDirectories(rootPath, "*", SearchOption.TopDirectoryOnly);
                    foreach (var subDir in subDirectories)
                    {
                        try { foundFiles.AddRange(Directory.GetFiles(subDir, "*.*", SearchOption.TopDirectoryOnly)); } catch { /* ignored */ }
                    }
                }
                catch { /* ignored */ }

                var acceptedFullPaths = foundFiles
                    .Where(filePath => k_AcceptedExtensions.Contains(Path.GetExtension(filePath).ToLowerInvariant()))
                    .ToList();

                var metadataByPath = acceptedFullPaths.ToDictionary(path => path, path => UriExtensions.GetGenerationMetadata(new Uri(path, UriKind.Absolute)));
                List<string> filteredFullPaths = new List<string>();
                var specificQuery = context.searchQuery?.Trim();
                if (!string.IsNullOrEmpty(specificQuery) && specificQuery.Contains(Legal.UnityAIGeneratedLabel, StringComparison.OrdinalIgnoreCase))
                    filteredFullPaths = acceptedFullPaths;
                if (!string.IsNullOrEmpty(specificQuery))
                {
                    specificQuery = specificQuery.Replace($"l:{Legal.UnityAIGeneratedLabel}", "").Trim();
                    if (!string.IsNullOrEmpty(specificQuery))
                    {
                        Regex regex = null;
                        if (specificQuery.Contains('*') || specificQuery.Contains('?'))
                        {
                            var regexPattern = Regex.Escape(specificQuery)
                                .Replace("\\*", ".*")
                                .Replace("\\?", ".");
                            regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
                        }

                        filteredFullPaths = acceptedFullPaths.Where(path => {
                            var filename = Path.GetFileName(path);
                            var filenameMatches = regex?.IsMatch(filename) ?? filename.IndexOf(specificQuery, StringComparison.OrdinalIgnoreCase) >= 0;
                            if (filenameMatches)
                                return true;

                            if (!metadataByPath.TryGetValue(path, out var metadata))
                                return false;

                            if (!string.IsNullOrEmpty(metadata.prompt))
                            {
                                var promptMatches = regex?.IsMatch(metadata.prompt) ??
                                    metadata.prompt.IndexOf(specificQuery, StringComparison.OrdinalIgnoreCase) >= 0;

                                if (promptMatches)
                                    return true;
                            }

                            if (!string.IsNullOrEmpty(metadata.negativePrompt))
                            {
                                var negativePromptMatches = regex?.IsMatch(metadata.negativePrompt) ??
                                    metadata.negativePrompt.IndexOf(specificQuery, StringComparison.OrdinalIgnoreCase) >= 0;

                                if (negativePromptMatches)
                                    return true;
                            }

                            return false;
                        }).ToList();
                    }
                }

                var searchItems = new List<SearchItem>();
                foreach (var fullPath in filteredFullPaths)
                {
                    if (!TryMakeRelative(fullPath, rootPath, out var displayPath))
                        continue;
                    displayPath = displayPath.Replace('\\', '/');

                    var description = $"{AssetReferenceExtensions.GetGeneratedAssetsRoot()}/{displayPath}";
                    if (metadataByPath.TryGetValue(fullPath, out var metadata) && !string.IsNullOrEmpty(metadata.prompt))
                        description = $"{description} \"{metadata.prompt}\"";

                    searchItems.Add(provider.CreateItem(context, displayPath, 0, Path.GetFileName(displayPath), description, FetchBaseThumbnail(fullPath), fullPath));
                }
                return searchItems;

            }
            catch
            {
                return Array.Empty<SearchItem>();
            }
        }

        static bool TryMakeRelative(string fileName, string directoryName, out string relativePath)
        {
            relativePath = null;

            if (string.IsNullOrWhiteSpace(fileName))
                return false;
            if (string.IsNullOrWhiteSpace(directoryName))
                return false;

            try
            {
                var fullFile = Path.GetFullPath(fileName);
                var fullDirectory = Path.GetFullPath(directoryName);
                var dirWithSeparator = fullDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!fullFile.StartsWith(dirWithSeparator, StringComparison.OrdinalIgnoreCase))
                    return false;

                relativePath = Path.GetRelativePath(fullDirectory, fullFile);
                if (string.IsNullOrEmpty(relativePath) || Path.IsPathFullyQualified(relativePath))
                {
                    relativePath = null;
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        static void TrackGeneratedAssetSelection(SearchItem item, SearchContext context)
        {
            if (item?.data is not string fileName || string.IsNullOrEmpty(fileName))
                return;

            if (TryMakeRelative(fileName, k_GeneratedAssetsPath, out var relativePath))
            {
                var assetGuid = Path.GetDirectoryName(relativePath);
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    if (EditorApplication.ExecuteMenuItem("Assets/Generate"))
                        return;
                }
            }

            if (File.Exists(fileName))
                EditorUtility.RevealInFinder(fileName);
        }
    }
}
