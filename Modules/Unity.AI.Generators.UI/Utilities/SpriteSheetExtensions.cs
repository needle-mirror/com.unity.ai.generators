using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;
using UnityEditor;
using UnityEditor.U2D.Sprites;

namespace Unity.AI.Generators.UI.Utilities
{
    /// <summary>
    /// Provides video conversion extension methods for VideoClip.
    /// </summary>
    static class SpriteSheetExtensions
    {
        public const string defaultAssetExtension = ".mp4";

        /// <summary>
        /// Generates a grid of rectangles based on the texture size and frame count.
        /// </summary>
        static Rect[] GenerateGridRects(Texture2D target, int frameCount = 16)
        {
            if (frameCount <= 0)
                throw new ArgumentException("Frame count must be greater than zero.", nameof(frameCount));

            var gridSize = Mathf.CeilToInt(Mathf.Sqrt(frameCount));
            var columnCount = gridSize;
            var rowCount = gridSize;

            var cellWidth = target.width / (float)columnCount;
            var cellHeight = target.height / (float)rowCount;
            if (cellWidth <= 0 || cellHeight <= 0)
                throw new InvalidOperationException("Texture dimensions and calculated column/row counts result in zero-sized or invalid cells.");

            var rects = new Rect[frameCount];
            for (var i = 0; i < frameCount; i++)
            {
                var col = i % columnCount;
                var row = i / columnCount;
                var destX = col * cellWidth;
                var destY = (rowCount - 1 - row) * cellHeight; // Y-flip for top-down logic
                rects[i] = new Rect(destX, destY, cellWidth, cellHeight);
            }
            return rects;
        }

        /// <summary>
        /// Converts a video clip to a new in-memory Texture2D sprite sheet and returns the texture along with its corresponding sprite rects.
        /// </summary>
        /// <param name="clip">The video clip to process.</param>
        /// <param name="totalCells"></param>
        /// <returns>A Task that resolves to a tuple containing the new Texture2D and the array of SpriteRects used to create it.</returns>
        public static async Task<(Texture2D texture, SpriteRect[] rects)> ConvertToSpriteSheetAsync(this VideoClip clip, int totalCells = 16)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));

            var gridSize = Mathf.CeilToInt(Mathf.Sqrt(totalCells));
            var targetWidth = (int)clip.width * 2;
            var targetHeight = (int)clip.height * 2;

            // Align dimensions to grid size to avoid rounding errors
            if (targetWidth % gridSize != 0) targetWidth += gridSize - (targetWidth % gridSize);
            if (targetHeight % gridSize != 0) targetHeight += gridSize - (targetHeight % gridSize);

            var targetTexture = new Texture2D(targetWidth, targetHeight);

            var finalRects = GenerateGridRects(targetTexture, totalCells);

            var jobCompletionSource = new TaskCompletionSource<object>();
            var job = new SpriteSheetJob(clip, jobCompletionSource, targetTexture, totalCells, DestinationRectProvider, 0.0, -1.0);
            job.Start();
            await jobCompletionSource.Task;

            var spriteRects = new SpriteRect[finalRects.Length];
            for (var i = 0; i < finalRects.Length; i++)
            {
                spriteRects[i] = new SpriteRect
                {
                    name = $"frame_{i}",
                    spriteID = GUID.Generate(),
                    rect = finalRects[i],
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                };
            }

            return (targetTexture, spriteRects);

            Rect? DestinationRectProvider(long frameIndex)
            {
                if (frameIndex < 0 || frameIndex >= finalRects.Length) return null;
                return finalRects[frameIndex];
            }
        }

        /// <summary>
        /// Applies a given array of SpriteRects to a Texture2D asset's import settings.
        /// This method avoids a costly re-import if the new rects (count, order, size, and position)
        /// and importer settings are functionally identical to the existing ones.
        /// </summary>
        /// <param name="targetTexture">The texture asset to modify.</param>
        /// <param name="newSpriteRects">The array of SpriteRects to apply.</param>
        public static void SetSpriteRects(this Texture2D targetTexture, SpriteRect[] newSpriteRects)
        {
            if (targetTexture == null)
            {
                Debug.LogError("Target Texture2D cannot be null.");
                return;
            }

            var path = AssetDatabase.GetAssetPath(targetTexture);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("Cannot set sprite rects. Target texture is not a persistent asset.");
                return;
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"Failed to get a TextureImporter for asset at path: {path}");
                return;
            }

            // Check if importer settings need to change.
            var settingsAreDifferent = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Multiple;

            // Must be done before new SpriteDataProviderFactories(importer)
            if (settingsAreDifferent)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (dataProvider == null)
            {
                Debug.LogError("Failed to get SpriteEditorDataProvider from TextureImporter.");
                return;
            }

            dataProvider.InitSpriteEditorDataProvider();

            var existingRects = dataProvider.GetSpriteRects();

            // Use LINQ's SequenceEqual to compare only the 'rect' property of each SpriteRect.
            var rectsAreDifferent = !existingRects.Select(r => r.rect).SequenceEqual(newSpriteRects.Select(r => r.rect));

            // If nothing has changed, we can stop here to avoid a needless reimport.
            if (!rectsAreDifferent && !settingsAreDifferent)
                return;

            // Sanitize names and IDs on the new rects before applying them.
            for (var i = 0; i < newSpriteRects.Length; i++)
            {
                if (string.IsNullOrEmpty(newSpriteRects[i].name))
                    newSpriteRects[i].name = $"{(!string.IsNullOrEmpty(targetTexture.name) ? targetTexture.name : "frame")}_{i}";
                if (newSpriteRects[i].spriteID == default)
                    newSpriteRects[i].spriteID = GUID.Generate();
            }

            dataProvider.SetSpriteRects(newSpriteRects);
            var spriteNameFileIdDataProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            var nameFileIdPairs = newSpriteRects.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)).ToList();
            spriteNameFileIdDataProvider.SetNameFileIdPairs(nameFileIdPairs);
            dataProvider.Apply();

            importer.SaveAndReimport();
        }
    }
}
