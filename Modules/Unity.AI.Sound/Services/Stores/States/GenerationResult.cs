using System;
using System.Collections.Generic;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Sound.Services.Undo;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Generators.Redux.Toolkit;
using Unity.AI.Generators.UI.Payloads;
using UnityEngine;

namespace Unity.AI.Sound.Services.Stores.States
{
    [Serializable]
    record GenerationResult
    {
        public bool generationAllowed = true;
        public List<GenerationProgressData> generationProgress = new();
        public List<GenerationFeedbackData> generationFeedback = new();
        public List<AudioClipResult> generatedAudioClips = new();
        public List<TextureSkeleton> generatedSkeletons = new();
        public AudioClipResult selectedGeneration = new();
        public AssetUndoManager assetUndoManager;
        public bool replaceWithoutConfirmation;
        public SerializableDictionary<string, GeneratedResultSelectorSettings> generatedResultSelectorSettings = new();
        public GenerationValidationResult generationValidation = new(false, AiResultErrorEnum.Unknown, 1, new List<GenerationFeedbackData>());
    }

    [Serializable]
    record GeneratedResultSelectorSettings
    {
        public int itemCountHint = 0;
    }
}
