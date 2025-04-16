using System;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux.Toolkit;

namespace Unity.AI.Image.Services.Stores.States
{
    [Serializable]
    record GenerationSetting
    {
        public SerializableDictionary<RefinementMode, ModelSelection> selectedModels = new();
        public float lastModelDiscoveryTime = 0;
        public string prompt = "";
        public string negativePrompt = "";
        public int variationCount = 4;
        public bool useCustomSeed;
        public int customSeed;
        public RefinementMode refinementMode;
        public string imageDimensions;
        public bool replaceBlankAsset = true;
        public bool replaceRefinementAsset = true;
        public int upscaleFactor = 2;

        public UnsavedAssetBytesSettings unsavedAssetBytes = new();

        // order must match ImageReferenceType enum
        public ImageReferenceSettings[] imageReferences = {
            new CompositionImageReference(),
            new DepthImageReference(),
            new FeatureImageReference(),
            new LineArtImageReference(),
            new InpaintMaskImageReference(),
            new PaletteImageReference(),
            new PoseImageReference(),
            new PromptImageReference(),
            new StyleImageReference()
        };

        public PixelateSettings pixelateSettings = new();

        public string pendingPing;
        public float historyDrawerHeight = 200;
    }

    [Serializable]
    record UnsavedAssetBytesSettings
    {
        public byte[] data = Array.Empty<byte>();
        public long timeStamp;
        public Uri uri;
    }

    [Serializable]
    record ImageReferenceSettings
    {
        public ImageReferenceSettings(float strength, bool isActive = false) { this.strength = strength; this.isActive = isActive; }
        public float strength = 0.25f;
        public AssetReference asset = new();
        public byte[] doodle = Array.Empty<byte>();
        public long doodleTimestamp;
        public ImageReferenceMode mode = ImageReferenceMode.Asset;
        public bool isActive;
    }

    record PromptImageReference() : ImageReferenceSettings(0.25f);
    record StyleImageReference() : ImageReferenceSettings(0.25f);
    record CompositionImageReference() : ImageReferenceSettings(0.75f);
    record PoseImageReference() : ImageReferenceSettings(0.90f);
    record DepthImageReference() : ImageReferenceSettings(0.75f);
    record LineArtImageReference() : ImageReferenceSettings(0.25f);
    record FeatureImageReference() : ImageReferenceSettings(0.25f);
    record PaletteImageReference() : ImageReferenceSettings(1, true);
    record InpaintMaskImageReference() : ImageReferenceSettings(0.1f, true);

    enum RefinementMode : int
    {
        Generation = 0,
        RemoveBackground = 1,
        Upscale = 2,
        Pixelate = 3,
        Recolor = 4,
        Inpaint = 5
    }

    enum ImageReferenceMode : int
    {
        Asset = 0,
        Doodle = 1
    }

    [Serializable]
    record ModelSelection
    {
        public string modelID = "";
    }
}
