using System;
using System.Collections.Generic;
using Unity.AI.Image.Components;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Utilities;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.ModelSelector.Services.Utilities;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Windows
{
    class GenerationMetadataContent : VisualElement
    {
        public event Action OnDismissRequested;

        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Image/Windows/GenerationMetadataWindow/GenerationMetadataWindow.uxml";
        const string k_UxmlDoodleTemplate = "Packages/com.unity.ai.generators/Modules/Unity.AI.Image/Windows/GenerationMetadataWindow/GenerationMetadataDoodleTemplate.uxml";

        readonly Store m_Store;
        readonly GenerationMetadata m_GenerationMetadata;

        ModelSettings m_ModelSettings;
        List<Button> m_DismissButtons;

        public GenerationMetadataContent(IStore store, GenerationMetadata generationMetadata)
        {
            m_GenerationMetadata = generationMetadata;
            m_Store = (Store)store;

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            var useAllButton = this.Q<Button>("use-all-button");
            useAllButton.clicked += UseAll;

            InitRefinementMode();
            InitModel();
            InitPrompt();
            InitNegativePrompt();
            InitCustomSeed();
            InitUpscaleFactor();
            InitDoodles();

            m_DismissButtons = this.Query<Button>(className: "data-button").ToList();
            foreach (var button in m_DismissButtons)
            {
                button.clicked += OnDismiss;
            }
        }

        void InitRefinementMode()
        {
            var refinementModeContainer = this.Q<VisualElement>(className: "refinement-mode-container");
            var refinementModeMetadata = this.Q<Label>("refinement-mode-metadata");
            var refinementModeUseButton = this.Q<Button>("use-refinement-mode-button");

            var refinementMode = m_GenerationMetadata?.refinementMode;
            refinementModeMetadata.text = refinementMode.AddSpaceBeforeCapitalLetters();
            refinementModeUseButton.clicked += UseRefinementMode;
            refinementModeContainer.EnableInClassList("hidden", string.IsNullOrEmpty(refinementMode));

            InitPixelate();
        }

        void InitPixelate()
        {
            var refinementMode = m_GenerationMetadata?.refinementMode;
            var pixelateContainer = this.Q<VisualElement>(className: "pixelate-container");
            var showPixelate = !string.IsNullOrEmpty(refinementMode) && refinementMode == RefinementMode.Pixelate.ToString();
            pixelateContainer.EnableInClassList("hidden", !showPixelate);
            if (!showPixelate || m_GenerationMetadata == null)
                return;

            var size = this.Q<Label>("pixelate-size-metadata");
            size.text = m_GenerationMetadata.pixelateTargetSize.ToString();

            var keepSize = this.Q<Label>("pixelate-keep-image-size-metadata");
            keepSize.text = m_GenerationMetadata.pixelateKeepImageSize.ToString();

            var samplingSize = this.Q<Label>("pixelate-sampling-size-metadata");
            samplingSize.text = m_GenerationMetadata.pixelatePixelBlockSize.ToString();

            var pixelateMode = this.Q<Label>("pixelate-pixelate-mode-metadata");
            pixelateMode.text = m_GenerationMetadata.pixelateMode.ToString();

            var outlineThickness = this.Q<Label>("pixelate-outline-thickness-metadata");
            outlineThickness.text = m_GenerationMetadata.pixelateOutlineThickness.ToString();

            var pixelateUseButton = this.Q<Button>("use-pixelate-button");
            pixelateUseButton.clicked += UsePixelate;

            var pixelateCopyButton = this.Q<Button>("copy-pixelate-button");
            pixelateCopyButton.clicked += () =>
            {
                var pixelateSettings = GetPixelateSettingsFromGenerationMetadata(m_GenerationMetadata);
                EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(pixelateSettings);
            };
        }

        void InitModel()
        {
            m_ModelSettings = m_Store?.State?.SelectModelSettings(m_GenerationMetadata);
            var modelName = m_ModelSettings?.name;
            var modelContainer = this.Q<VisualElement>(className: "model-container");
            var modelMetadata = this.Q<Label>("model-metadata");
            var modelCopyButton = this.Q<Button>("copy-model-button");
            var modelUseButton = this.Q<Button>("use-model-button");

            modelMetadata.text = modelName;
            modelCopyButton.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = m_ModelSettings?.id;
            };
            modelUseButton.clicked += UseModel;
            modelContainer.EnableInClassList("hidden", string.IsNullOrEmpty(m_ModelSettings?.id));
        }

        void InitPrompt()
        {
            var promptContainer = this.Q<VisualElement>(className: "prompt-container");
            var promptMetadata = this.Q<Label>("prompt-metadata");
            var promptCopyButton = this.Q<Button>("copy-prompt-button");
            var promptUseButton = this.Q<Button>("use-prompt-button");

            var prompt = m_GenerationMetadata?.prompt;
            promptMetadata.text = prompt;
            promptCopyButton.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = prompt;
            };
            promptUseButton.clicked += UsePrompt;
            promptContainer.EnableInClassList("hidden", string.IsNullOrEmpty(prompt));
        }

        void InitNegativePrompt()
        {
            var negativePromptContainer = this.Q<VisualElement>(className: "negative-prompt-container");
            var negativePromptMetadata = this.Q<Label>("negative-prompt-metadata");
            var negativePromptCopyButton = this.Q<Button>("copy-negative-prompt-button");
            var negativePromptUseButton = this.Q<Button>("use-negative-prompt-button");

            var negativePrompt = m_GenerationMetadata?.negativePrompt;
            negativePromptMetadata.text = negativePrompt;
            negativePromptCopyButton.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = negativePrompt;
            };
            negativePromptUseButton.clicked += UseNegativePrompt;
            negativePromptContainer.EnableInClassList("hidden", string.IsNullOrEmpty(negativePrompt));
        }

        void InitCustomSeed()
        {
            var customSeedContainer = this.Q<VisualElement>(className: "custom-seed-container");
            var customSeedMetadata = this.Q<Label>("custom-seed-metadata");
            var customSeedCopyButton = this.Q<Button>("copy-custom-seed-button");
            var customSeedUseButton = this.Q<Button>("use-custom-seed-button");

            var customSeed = m_GenerationMetadata?.customSeed;
            customSeedMetadata.text = customSeed.ToString();
            customSeedCopyButton.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = customSeed.ToString();
            };
            customSeedUseButton.clicked += UseCustomSeed;
            customSeedContainer.EnableInClassList("hidden", customSeed == -1);
        }

        void InitUpscaleFactor()
        {
            var upscaleFactorContainer = this.Q<VisualElement>(className: "upscale-factor-container");
            var upscaleFactorMetadata = this.Q<Label>("upscale-factor-metadata");
            var upscaleFactorCopyButton = this.Q<Button>("copy-upscale-factor-button");
            var upscaleFactorUseButton = this.Q<Button>("use-upscale-factor-button");

            var upscaleFactor = m_GenerationMetadata?.upscaleFactor;
            upscaleFactorMetadata.text = upscaleFactor.ToString();
            upscaleFactorCopyButton.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = upscaleFactor.ToString();
            };
            upscaleFactorUseButton.clicked += UseUpscaleFactor;
            upscaleFactorContainer.EnableInClassList("hidden", upscaleFactor == 0);
        }

        void InitDoodles()
        {
            var doodlesContainer = this.Q<VisualElement>(className: "doodles-container");
            var hasDoodles = m_GenerationMetadata.doodles is { Length: > 0 };

            if (hasDoodles)
            {
                var doodleTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_UxmlDoodleTemplate);

                foreach(var doodleData in m_GenerationMetadata.doodles)
                {
                    var doodleUI = doodleTemplate.Instantiate();
                    doodlesContainer.Add(doodleUI);

                    var label = doodleUI.Q<Label>("doodle-type");
                    label.text = doodleData.label;

                    var doodleCopyButton = doodleUI.Q<Button>("copy-doodle-button");
                    doodleCopyButton.clicked += () =>
                    {
                        EditorGUIUtility.systemCopyBuffer = "MetadataDoodleBytes:" + Convert.ToBase64String(doodleData.doodle);
                    };

                    var doodleUseButton = doodleUI.Q<Button>("use-doodle-button");
                    var doodlePad = doodleUI.Q<DoodlePad>();
                    if (doodlePad != null && m_GenerationMetadata?.doodles is { Length: > 0 })
                    {
                        doodlePad.SetDoodle(doodleData.doodle);
                    }
                    doodleUseButton.clicked += () => { UseDoodle(doodleData); };
                }
            }

            doodlesContainer.EnableInClassList("hidden", !hasDoodles);
        }

        void UseRefinementMode()
        {
            var refinementMode = m_GenerationMetadata.refinementMode;

            if(Enum.TryParse<RefinementMode>(refinementMode, out var mode))
                this.Dispatch(GenerationSettingsActions.setRefinementMode, mode);
        }

        void UseModel()
        {
            if (m_ModelSettings.IsValid())
                this.Dispatch(GenerationSettingsActions.setSelectedModelID, (this.GetState().SelectRefinementMode(this), m_ModelSettings.id));
        }

        void UsePrompt()
        {
            var truncatedPrompt = PromptUtilities.TruncatePrompt(m_GenerationMetadata?.prompt);
            this.Dispatch(GenerationSettingsActions.setPrompt, truncatedPrompt);
        }

        void UseNegativePrompt()
        {
            var truncatedPrompt = PromptUtilities.TruncatePrompt(m_GenerationMetadata?.negativePrompt);
            this.Dispatch(GenerationSettingsActions.setNegativePrompt, truncatedPrompt);
        }

        void UseCustomSeed()
        {
            if (m_GenerationMetadata.customSeed != -1)
            {
                this.Dispatch(GenerationSettingsActions.setUseCustomSeed, true);
                this.Dispatch(GenerationSettingsActions.setCustomSeed, m_GenerationMetadata.customSeed);
            }
        }

        void UseUpscaleFactor()
        {
            if (m_GenerationMetadata.upscaleFactor != 0)
            {
                this.Dispatch(GenerationSettingsActions.setUpscaleFactor, m_GenerationMetadata.upscaleFactor);
            }
        }

        void UseDoodle(GenerationDataDoodle doodleData)
        {
            var doodlePad = this.Q<DoodlePad>();

            if (doodlePad != null)
            {
                UseRefinementMode();
                switch (doodleData.doodleReferenceType)
                {
                    case ImageReferenceType.CompositionImage:
                        ImageReferenceType.CompositionImage.SetDoodlePadData(doodlePad, doodleData.doodle);
                        break;
                    case ImageReferenceType.DepthImage:
                        ImageReferenceType.DepthImage.SetDoodlePadData(doodlePad, doodleData.doodle);
                        break;
                    case ImageReferenceType.FeatureImage:
                        ImageReferenceType.FeatureImage.SetDoodlePadData(doodlePad, doodleData.doodle);
                        break;
                    case ImageReferenceType.LineArtImage:
                        ImageReferenceType.LineArtImage.SetDoodlePadData(doodlePad, doodleData.doodle);
                        break;
                    case ImageReferenceType.InPaintMaskImage:
                        ImageReferenceType.InPaintMaskImage.SetDoodlePadData(doodlePad, doodleData.doodle);
                        break;
                    case ImageReferenceType.PaletteImage:
                        ImageReferenceType.PaletteImage.SetDoodlePadData(doodlePad, doodleData.doodle);
                        break;
                    case ImageReferenceType.PoseImage:
                        ImageReferenceType.PoseImage.SetDoodlePadData(doodlePad, doodleData.doodle);
                        break;
                    case ImageReferenceType.PromptImage:
                        ImageReferenceType.PromptImage.SetDoodlePadData(doodlePad, doodleData.doodle);
                        break;
                    case ImageReferenceType.StyleImage:
                        ImageReferenceType.StyleImage.SetDoodlePadData(doodlePad, doodleData.doodle);
                        break;
                    default:
                        break;
                }
            }
        }

        void UsePixelate()
        {
            if (m_GenerationMetadata?.refinementMode == RefinementMode.Pixelate.ToString())
            {
                var pixelateSettings = GetPixelateSettingsFromGenerationMetadata(m_GenerationMetadata);
                this.Dispatch(GenerationSettingsActions.setPixelateSettings, pixelateSettings);
                UseRefinementMode();
            }
        }

        PixelateSettings GetPixelateSettingsFromGenerationMetadata(GenerationMetadata generationMetadata)
        {
            var pixelateSettings = new PixelateSettings()
            {
                targetSize = generationMetadata.pixelateTargetSize,
                keepImageSize = generationMetadata.pixelateKeepImageSize,
                pixelBlockSize = generationMetadata.pixelatePixelBlockSize,
                mode = generationMetadata.pixelateMode,
                outlineThickness = generationMetadata.pixelateOutlineThickness
            };
            return pixelateSettings;
        }

        void UseAll()
        {
            UseRefinementMode();
            UseModel();
            UsePrompt();
            UseNegativePrompt();
            UseCustomSeed();
            UsePixelate();
            foreach (var doodleData in m_GenerationMetadata.doodles)
            {
                UseDoodle(doodleData);
            }
        }

        void OnDismiss()
        {
            OnDismissRequested?.Invoke();
        }
    }
}
