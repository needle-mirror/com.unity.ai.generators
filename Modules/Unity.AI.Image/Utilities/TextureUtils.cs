﻿using System;
using System.IO;
using Unity.AI.Generators.UI.Utilities;
using Unity.Collections;
using UnityEngine;

namespace Unity.AI.Image.Services.Utilities
{
    static class TextureUtils
    {
        const int k_PaletteSize = 6;
        const int k_Rows = 3;
        const int k_Columns = 2;
        const int k_ResizeSize = 128;

        public static byte[] CreatePaletteApproximation(byte[] paletteAsset)
        {
            if (paletteAsset == null || paletteAsset.Length == 0)
                return paletteAsset;

            Texture2D palette = null;
            var source = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                source.LoadImage(paletteAsset);

                var resizedTexture = source.CloneTexture(k_ResizeSize, k_ResizeSize);
                source.SafeDestroy();

                palette = resizedTexture.CreatePaletteApproximation();
                return palette.EncodeToPNG();
            }
            finally
            {
                source.SafeDestroy();
                palette?.SafeDestroy();
            }
        }

        public static Stream CreatePaletteApproximation(Stream paletteAssetStream)
        {
            if (paletteAssetStream == null || paletteAssetStream.Length == 0)
                return paletteAssetStream;

            Texture2D palette = null;
            var source = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var paletteAsset = paletteAssetStream.ReadFully();
                source.LoadImage(paletteAsset);

                var resizedTexture = source.CloneTexture(k_ResizeSize, k_ResizeSize);
                source.SafeDestroy();

                palette = resizedTexture.CreatePaletteApproximation();
                var pngBytes = palette.EncodeToPNG();
                return new MemoryStream(pngBytes);
            }
            finally
            {
                source.SafeDestroy();
                palette?.SafeDestroy();
            }
        }

        static Texture2D CreatePaletteApproximation(this Texture2D source)
        {
            // Create the palette texture
            var paletteTex = new Texture2D(k_Columns, k_Rows, TextureFormat.RGB24, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "PaletteTexture",
                filterMode = FilterMode.Point
            };

            // Get the colors from the source texture
            var colors = source.GetPixels32();
            var dataLength = colors.Length;

            var data = new NativeArray<Vector3>(dataLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            try
            {
                // Normalize RGB values and store them in a NativeArray<Vector3>
                for (var i = 0; i < dataLength; i++)
                {
                    var x = colors[i].r / 255.0f;
                    var y = colors[i].g / 255.0f;
                    var z = colors[i].b / 255.0f;
                    data[i] = new Vector3(x, y, z);
                }

                // Generate palette colors using K-Means clustering
                var centroids = Cluster3(data, k_PaletteSize, Allocator.Temp, 10);

                if (centroids.Length != k_PaletteSize)
                    Debug.LogError("Incorrect palette size generated");

                // Assign the palette colors to the palette texture
                var pixels = paletteTex.GetPixels32();
                for (var i = 0; i < pixels.Length; i++)
                {
                    var paletteColor = Vector3.zero;
                    var cIndex = i % k_PaletteSize;
                    var index = centroids[cIndex];

                    if (index < data.Length)
                        paletteColor = data[index];

                    pixels[i] = new Color(paletteColor.x, paletteColor.y, paletteColor.z, 1);
                }

                paletteTex.SetPixels32(pixels);
                paletteTex.Apply();
            }
            finally
            {
                // Clean up
                data.Dispose();
            }
            return paletteTex;
        }

        // Performs K-Means clustering on 3-dimensional data (RGB colors)
        public static int[] Cluster3(NativeArray<Vector3> data, int clusterCount, Allocator alloc, int maxIterations = 64)
        {
            var dataLength = data.Length;

            using var clusters = new NativeArray<int>(dataLength, alloc, NativeArrayOptions.UninitializedMemory);
            using var means = new NativeArray<Vector3>(clusterCount, alloc, NativeArrayOptions.ClearMemory);
            using var centroids = new NativeArray<int>(clusterCount, alloc, NativeArrayOptions.UninitializedMemory);
            using var clusterItems = new NativeArray<int>(clusterCount, alloc, NativeArrayOptions.UninitializedMemory);

            ClusterInternal(data, clusters, means, centroids, clusterItems, clusterCount, maxIterations);

            var returnData = centroids.ToArray();

            return returnData;
        }

        internal static Texture2D CloneTexture(this Texture2D texture2D, int targetWidth, int targetHeight)
        {
            if (texture2D.width == targetWidth && texture2D.height == targetHeight)
            {
                return texture2D;
            }

            var renderTexture = RenderTexture.GetTemporary(targetWidth, targetHeight);
            var result = new Texture2D(targetWidth, targetHeight);
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = renderTexture;
                Graphics.Blit(texture2D, renderTexture);
                result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                result.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
            }

            return result;
        }

        static void ClusterInternal(NativeArray<Vector3> data, NativeArray<int> clusters, NativeArray<Vector3> means,
            NativeArray<int> centroids, NativeArray<int> clusterItems, int clusterCount, int maxIterations)
        {
            var hasChanges = true;
            var iteration = 0;

            // Initialize clusters with random assignments
            var random = new System.Random(1);
            for (var i = 0; i < clusters.Length; ++i)
                clusters[i] = random.Next(0, clusterCount);

            // Main K-Means iteration loop
            while (hasChanges && iteration++ < maxIterations)
            {
                // Reset cluster item counts
                for (var i = 0; i < clusterItems.Length; i++)
                    clusterItems[i] = 0;

                // Recalculate means and centroids
                CalculateClustering(data, clusters, means, centroids, clusterCount, clusterItems);

                // Reassign clusters based on new centroids
                hasChanges = AssignClustering(data, clusters, centroids, clusterCount);
            }
        }

        // Calculates the new means and identifies centroids for each cluster
        static float CalculateClustering(NativeArray<Vector3> data, NativeArray<int> clusters, NativeArray<Vector3> means,
            NativeArray<int> centroids, int clusterCount, NativeArray<int> clusterItems)
        {
            // Reset means to zero
            for (var i = 0; i < means.Length; i++)
                means[i] = Vector3.zero;

            // Sum up data points for each cluster
            for (var i = 0; i < data.Length; i++)
            {
                var cluster = clusters[i];
                clusterItems[cluster]++;
                means[cluster] += data[i];
            }

            // Calculate mean (average) for each cluster
            for (var k = 0; k < means.Length; k++)
            {
                var itemCount = clusterItems[k];
                if (itemCount > 0)
                    means[k] /= itemCount;
            }

            // Identify the centroid (closest data point to the mean) for each cluster
            var totalDistance = 0.0f;
            var minDistances = new NativeArray<float>(clusterCount, Allocator.Temp);

            try
            {
                for (var i = 0; i < clusterCount; ++i)
                    minDistances[i] = float.MaxValue;

                for (var i = 0; i < data.Length; i++)
                {
                    var cluster = clusters[i];
                    var distance = Vector3.Distance(data[i], means[cluster]);
                    totalDistance += distance;

                    if (distance < minDistances[cluster])
                    {
                        minDistances[cluster] = distance;
                        centroids[cluster] = i;
                    }
                }
            }
            finally
            {
                minDistances.Dispose();
            }

            return totalDistance;
        }

        // Assigns data points to the closest centroid
        static bool AssignClustering(NativeArray<Vector3> data, NativeArray<int> clusters,
            NativeArray<int> centroids, int clusterCount)
        {
            var changed = false;

            for (var i = 0; i < data.Length; i++)
            {
                var minDistance = float.MaxValue;
                var minClusterIndex = -1;

                for (var k = 0; k < clusterCount; k++)
                {
                    var centroidIndex = centroids[k];
                    var distance = Vector3.Distance(data[i], data[centroidIndex]);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        minClusterIndex = k;
                    }
                }

                if (minClusterIndex != -1 && clusters[i] != minClusterIndex)
                {
                    changed = true;
                    clusters[i] = minClusterIndex;
                }
            }

            return changed;
        }

        public static bool AreAllPixelsSameColor(byte[] imageAsset)
        {
            Texture2D source = null;
            try
            {
                source = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                source.LoadImage(imageAsset);
                return source.AreAllPixelsSameColor();
            }
            finally
            {
                source?.SafeDestroy();
            }
        }

        public static bool AreAllPixelsSameColor(this Texture2D source)
        {
            var pixels = source.GetPixels32();
            if (pixels.Length <= 1)
                return true; // Treat empty or single-pixel image as having all pixels the same color

            var firstColor = pixels[0];

            for (var i = 1; i < pixels.Length; i++)
            {
                if (!pixels[i].Equals(firstColor))
                {
                    return false; // Found a pixel with a different color
                }
            }

            return true; // All pixels are the same color
        }
    }
}
