using System;
using System.Collections.Generic;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Generators.Asset;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Undo;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Generators.Redux.Toolkit;
using UnityEngine;

namespace Unity.AI.Image.Services.Stores.States
{
    [Serializable]
    record GenerationResult
    {
        public bool generationAllowed = true;
        public List<GenerationProgressData> generationProgress = new();
        public List<GenerationFeedbackData> generationFeedback = new();
        public List<TextureResult> generatedTextures = new();
        public List<TextureSkeleton> generatedSkeletons = new();
        public TextureResult selectedGeneration = new();
        public AssetUndoManager assetUndoManager;
        public int generationCount;
        public bool replaceWithoutConfirmation;
        public Action<AssetReference> promoteNewAssetPostAction = null;
        public SerializableDictionary<string, GeneratedResultSelectorSettings> generatedResultSelectorSettings = new();
        public GenerationValidationResult generationValidation = new(true, AiResultErrorEnum.Unknown, 0, new List<GenerationFeedbackData>());
    }

    [Serializable]
    record GeneratedResultSelectorSettings
    {
        public int itemCountHint = 0;
    }
}
