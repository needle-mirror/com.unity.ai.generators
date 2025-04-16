using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.AI.Image.Services.Utilities
{
    static class TextureResultExtensions
    {
        public static async Task CopyToProject(this TextureResult textureResult, GenerationMetadata generationMetadata, string cacheDirectory)
        {
            if (!textureResult.uri.IsFile)
                return; // DownloadToProject should be used for remote files

            var path = textureResult.uri.GetLocalPath();
            var extension = Path.GetExtension(path);
            if (!ImageFileUtilities.knownExtensions.Any(suffix => suffix.Equals(extension, StringComparison.OrdinalIgnoreCase)))
            {
                await using var fileStream = FileIO.OpenReadAsync(path);
                extension = FileIO.GetFileExtension(fileStream);
            }

            var fileName = Path.GetFileName(path);
            if (!File.Exists(path) || string.IsNullOrEmpty(cacheDirectory))
                return;

            Directory.CreateDirectory(cacheDirectory);
            var newPath = Path.Combine(cacheDirectory, fileName);
            newPath = Path.ChangeExtension(newPath, extension);
            var newUri = new Uri(Path.GetFullPath(newPath));
            if (newUri == textureResult.uri)
                return;

            File.Copy(path, newPath, overwrite: true);
            Generators.Asset.AssetReferenceExtensions.ImportAsset(newPath);
            textureResult.uri = newUri;

            await FileIO.WriteAllTextAsync($"{textureResult.uri.GetLocalPath()}.json",
                JsonUtility.ToJson(generationMetadata with { fileName = fileName }, true));
        }

        public static async Task DownloadToProject(this TextureResult textureResult, GenerationMetadata generationMetadata, string cacheDirectory, HttpClient httpClient)
        {
            if (textureResult.uri.IsFile)
                return; // CopyToProject should be used for local files

            if (string.IsNullOrEmpty(cacheDirectory))
                return;
            Directory.CreateDirectory(cacheDirectory);

            var newUri = await UriExtensions.DownloadFile(textureResult.uri, cacheDirectory, httpClient);
            if (newUri == textureResult.uri)
                return;

            textureResult.uri = newUri;

            var path = textureResult.uri.GetLocalPath();
            var fileName = Path.GetFileName(path);

            await FileIO.WriteAllTextAsync($"{textureResult.uri.GetLocalPath()}.json",
                JsonUtility.ToJson(generationMetadata with { fileName = fileName }, true));
        }

        public static async Task<GenerationMetadata> GetMetadata(this TextureResult textureResult)
        {
            var data = new GenerationMetadata();
            try { data = JsonUtility.FromJson<GenerationMetadata>(await FileIO.ReadAllTextAsync($"{textureResult.uri.GetLocalPath()}.json")); }
            catch { /*Debug.LogWarning($"Could not read {textureResult.uri.GetLocalPath()}.json");*/ }
            return data;
        }

        public static GenerationMetadata MakeMetadata(this GenerationSetting setting, AssetReference asset)
        {
            if (setting == null)
                return new GenerationMetadata { asset = asset.guid };

            switch (setting.refinementMode)
            {
                case RefinementMode.Generation:
                    var customSeed = setting.useCustomSeed ? setting.customSeed : -1;

                    return new GenerationMetadata
                    {
                        prompt = setting.prompt,
                        negativePrompt = setting.negativePrompt,
                        refinementMode = setting.refinementMode.ToString(),
                        model = setting.SelectSelectedModelID(),
                        asset = asset.guid,
                        customSeed = customSeed,
                        doodles = GetDoodlesForGenerationMetadata(setting).ToArray()
                    };
                case RefinementMode.RemoveBackground:
                    return new GenerationMetadata
                    {
                        refinementMode = setting.refinementMode.ToString(),
                        asset = asset.guid
                    };
                case RefinementMode.Upscale:
                    return new GenerationMetadata
                    {
                        refinementMode = setting.refinementMode.ToString(),
                        model = setting.SelectSelectedModelID(),
                        asset = asset.guid,
                        upscaleFactor = setting.upscaleFactor
                    };
                case RefinementMode.Pixelate:
                    return new GenerationMetadata
                    {
                        refinementMode = setting.refinementMode.ToString(),
                        asset = asset.guid,
                        pixelateTargetSize = setting.pixelateSettings.targetSize,
                        pixelateKeepImageSize = setting.pixelateSettings.keepImageSize,
                        pixelatePixelBlockSize = setting.pixelateSettings.pixelBlockSize,
                        pixelateMode = setting.pixelateSettings.mode,
                        pixelateOutlineThickness = setting.pixelateSettings.outlineThickness
                    };
                case RefinementMode.Recolor:
                    return new GenerationMetadata
                    {
                        refinementMode = setting.refinementMode.ToString(),
                        asset = asset.guid,
                        doodles = GetDoodlesForGenerationMetadata(setting).ToArray()
                    };
                case RefinementMode.Inpaint:
                    return new GenerationMetadata
                    {
                        prompt = setting.prompt,
                        negativePrompt = setting.negativePrompt,
                        refinementMode = setting.refinementMode.ToString(),
                        model = setting.SelectSelectedModelID(),
                        asset = asset.guid,
                        doodles = GetDoodlesForGenerationMetadata(setting).ToArray()
                    };
                default:
                    return new GenerationMetadata { asset = asset.guid };
            }
        }

        public static bool IsValid(this TextureResult textureResult) => textureResult?.uri != null && textureResult.uri.IsAbsoluteUri;

        public static bool IsFailed(this TextureResult result)
        {
            if (!IsValid(result))
                return false;

            if (string.IsNullOrEmpty(result.uri.GetLocalPath()))
                return true;

            var localPath = result.uri.GetLocalPath();
            return FileIO.AreFilesIdentical(localPath, Path.GetFullPath(FileUtilities.failedDownloadPath));
        }

        public static async Task<bool> CopyTo(this TextureResult generatedTexture, string destFileName)
        {
            var sourceFileName = generatedTexture.uri.GetLocalPath();
            if (!File.Exists(sourceFileName))
                return false;

            var destExtension = Path.GetExtension(destFileName).ToLower();
            var sourceExtension = Path.GetExtension(sourceFileName).ToLower();
            if (destExtension != sourceExtension)
            {
                await using Stream imageStream = FileIO.OpenReadAsync(generatedTexture.uri.GetLocalPath());
                if (!ImageFileUtilities.TryConvert(imageStream, out var convertedStream, destExtension))
                    return false;

                await using var stream = convertedStream != imageStream ? convertedStream : null;
                await FileIO.WriteAllBytesAsync(destFileName, convertedStream);
            }
            else
                File.Copy(sourceFileName, destFileName, true);
            Generators.Asset.AssetReferenceExtensions.ImportAsset(destFileName);
            return true;
        }

        public static async Task<byte[]> GetFile(this TextureResult textureResult)
        {
            await using var stream = await textureResult.GetCompatibleImageStreamAsync();
            return await stream.ReadFullyAsync();
        }

        public static async Task<Stream> GetCompatibleImageStreamAsync(this TextureResult textureResult) =>
            await ImageFileUtilities.GetCompatibleImageStreamAsync(textureResult.uri);

        static List<GenerationDataDoodle> GetDoodlesForGenerationMetadata(GenerationSetting setting)
        {
            var doodles = new List<GenerationDataDoodle>();
            // Determine which image reference types are relevant based on the refinement mode.
            var imageTypes = setting.refinementMode switch
            {
                RefinementMode.Recolor => new[] { ImageReferenceType.PaletteImage },
                RefinementMode.Inpaint => new[] { ImageReferenceType.InPaintMaskImage },
                RefinementMode.Generation => new[]
                {
                    ImageReferenceType.PromptImage, ImageReferenceType.StyleImage, ImageReferenceType.PoseImage, ImageReferenceType.DepthImage,
                    ImageReferenceType.CompositionImage, ImageReferenceType.LineArtImage, ImageReferenceType.FeatureImage
                },
                _ => Enumerable.Empty<ImageReferenceType>()
            };
            foreach (var type in imageTypes)
            {
                // Retrieve the image reference (and its doodle) for this type.
                var imageReference = setting.SelectImageReference(type);
                if (imageReference?.doodle?.Length > 0)
                    doodles.Add(new GenerationDataDoodle(type, imageReference.doodle, type.GetInternalDisplayNameForType()));
            }
            return doodles;
        }
    }

    // We duplicate variable names instead of using GenerationSettings directly because we want to control
    // the serialization and not have problems if a variable name changes.
    [Serializable]
    record GenerationMetadata : GeneratedAssetMetadata
    {
        public string refinementMode;
        public int pixelateTargetSize;
        public bool pixelateKeepImageSize;
        public int pixelatePixelBlockSize;
        public PixelateMode pixelateMode;
        public int pixelateOutlineThickness;
        public ImmutableArray<GenerationDataDoodle> doodles = ImmutableArray<GenerationDataDoodle>.Empty;
        public int upscaleFactor;
    }

    [Serializable]
    record GenerationDataDoodle
    {
        [FormerlySerializedAs("doodleControlType")]
        public ImageReferenceType doodleReferenceType;
        public byte[] doodle;
        public string label;

        public GenerationDataDoodle(ImageReferenceType referenceType, byte[] doodleData, string label)
        {
            doodleReferenceType = referenceType;
            doodle = doodleData;
            this.label = label;
        }
    }
}
