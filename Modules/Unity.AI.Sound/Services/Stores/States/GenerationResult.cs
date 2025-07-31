using System;
using System.Collections.Generic;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Sound.Services.Undo;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Generators.Redux.Toolkit;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UI.Utilities;
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

        /// <summary>
        /// Maps in-progress skeletons to their completed texture results.
        ///
        /// When a generation starts, a TextureSkeleton is created to represent the in-progress task.
        /// When the generation completes, a TextureResult is created with the result URI.
        /// FulfilledSkeletons links these two by storing:
        /// - progressTaskID: Matches with TextureSkeleton.taskID
        /// - resultUri: Matches with TextureResult.uri.AbsoluteUri
        ///
        /// This mapping allows UI to properly transition from showing in-progress skeletons to completed results.
        /// </summary>
        public List<FulfilledSkeleton> fulfilledSkeletons = new();

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
