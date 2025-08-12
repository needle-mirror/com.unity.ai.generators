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
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.AI.Image.Services.Utilities
{
    static class TextureResultExtensions
    {
        public static async Task CopyToProject(this TextureResult textureResult, GenerationMetadata generationMetadata, string cacheDirectory)
        {
            try
            {
                if (!textureResult.uri.IsFile)
                    throw new ArgumentException("CopyToProject should only be used for local files.", nameof(textureResult));

                var path = textureResult.uri.GetLocalPath();
                var extension = Path.GetExtension(path);
                if (!ImageFileUtilities.knownExtensions.Any(suffix => suffix.Equals(extension, StringComparison.OrdinalIgnoreCase)))
                {
                    await using var fileStream = FileIO.OpenReadAsync(path);
                    extension = FileIO.GetFileExtension(fileStream);
                }

                var fileName = Path.GetFileName(path);
                if (!File.Exists(path))
                    throw new FileNotFoundException($"The file {path} does not exist.", path);
                if (string.IsNullOrEmpty(cacheDirectory))
                    throw new ArgumentException("Cache directory must be specified.", nameof(cacheDirectory));

                Directory.CreateDirectory(cacheDirectory);
                var newPath = Path.Combine(cacheDirectory, fileName);
                newPath = Path.ChangeExtension(newPath, extension);
                var newUri = new Uri(Path.GetFullPath(newPath));
                if (newUri == textureResult.uri)
                    return;

                await FileIO.CopyFileAsync(path, newPath, overwrite: true);
                Generators.Asset.AssetReferenceExtensions.ImportAsset(newPath);
                textureResult.uri = newUri;

                try
                {
                    await FileIO.WriteAllTextAsync($"{textureResult.uri.GetLocalPath()}.json",
                        JsonUtility.ToJson(generationMetadata with { fileName = fileName }, true));
                }
                catch (Exception e)
                {
                    // log an error but not absolutely critical as generations can be used without metadata
                    Debug.LogException(e);
                }
            }
            finally
            {
                GenerationFileSystemWatcher.nudge?.Invoke();
            }
        }

        public static async Task DownloadToProject(this TextureResult textureResult, GenerationMetadata generationMetadata, string cacheDirectory, HttpClient httpClient)
        {
            try
            {
                if (textureResult.uri.IsFile)
                    throw new ArgumentException("DownloadToProject should only be used for remote files.", nameof(textureResult));

                if (string.IsNullOrEmpty(cacheDirectory))
                    throw new ArgumentException("Cache directory must be specified for remote files.", nameof(cacheDirectory));
                Directory.CreateDirectory(cacheDirectory);

                var newUri = await UriExtensions.DownloadFile(textureResult.uri, cacheDirectory, httpClient);
                if (newUri == textureResult.uri)
                    return;

                textureResult.uri = newUri;

                try
                {
                    var path = textureResult.uri.GetLocalPath();
                    var fileName = Path.GetFileName(path);

                    await FileIO.WriteAllTextAsync($"{textureResult.uri.GetLocalPath()}.json",
                        JsonUtility.ToJson(generationMetadata with { fileName = fileName }, true));
                }
                catch (Exception e)
                {
                    // log an error but not absolutely critical as generations can be used without metadata
                    Debug.LogException(e);
                }
            }
            finally
            {
                GenerationFileSystemWatcher.nudge?.Invoke();
            }
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
                        modelName = setting.SelectSelectedModelName(),
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
                await FileIO.CopyFileAsync(sourceFileName, destFileName, overwrite: true);

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
                {
                    var invertStrength = type.SelectImageReferenceInvertStrength();
                    doodles.Add(new GenerationDataDoodle(type, imageReference.doodle, type.GetInternalDisplayNameForType(), imageReference.strength, invertStrength));
                }
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
        public float strength;
        public bool invertStrength;

        public GenerationDataDoodle(ImageReferenceType referenceType, byte[] doodleData, string label, float strength, bool invertStrength)
        {
            doodleReferenceType = referenceType;
            doodle = doodleData;
            this.label = label;
            this.strength = strength;
            this.invertStrength = invertStrength;
        }
    }
}
