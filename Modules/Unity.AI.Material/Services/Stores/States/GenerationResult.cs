using System;
using System.Collections.Generic;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Material.Services.Stores.Actions.Payloads;
using Unity.AI.Material.Services.Undo;
using Unity.AI.Material.Services.Utilities;
using Unity.AI.Generators.Redux.Toolkit;
using UnityEngine;

namespace Unity.AI.Material.Services.Stores.States
{
    [Serializable]
    record GenerationResult
    {
        public const string noneMapping = "None";

        public bool generationAllowed = true;
        public List<GenerationProgressData> generationProgress = new();
        public List<GenerationFeedbackData> generationFeedback = new();
        public List<MaterialResult> generatedMaterials = new();
        public List<MaterialSkeleton> generatedSkeletons = new();
        public MaterialResult selectedGeneration = new();
        public SerializableDictionary<MapType, string> generatedMaterialMapping = new()
        {
            { MapType.Preview, noneMapping },
            { MapType.Height, noneMapping },
            { MapType.Normal, noneMapping },
            { MapType.Emission, noneMapping },
            { MapType.Metallic, noneMapping },
            { MapType.Roughness, noneMapping },
            { MapType.Delighted, noneMapping },
            { MapType.Occlusion, noneMapping },
            { MapType.Smoothness, noneMapping },
            { MapType.MetallicSmoothness, noneMapping },
            { MapType.NonMetallicSmoothness, noneMapping },
            { MapType.MaskMap, noneMapping }
        };
        public AssetUndoManager assetUndoManager;
        public bool replaceWithoutConfirmation = true; // AI.Material and AI.Animate are unable to detect differences between asset and generation so we default to true for easier workflows
        public SerializableDictionary<string, GeneratedResultSelectorSettings> generatedResultSelectorSettings = new();
        public GenerationValidationResult generationValidation = new(false, AiResultErrorEnum.UnknownError, 1, new List<GenerationFeedbackData>());
    }

    [Serializable]
    record GeneratedResultSelectorSettings
    {
        public int itemCountHint = 0;
    }
}
