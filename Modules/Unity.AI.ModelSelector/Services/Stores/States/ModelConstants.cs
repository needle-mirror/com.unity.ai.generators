using System;
using System.Collections.Generic;
using AiEditorToolsSdk.Components.Common.Enums;

namespace Unity.AI.ModelSelector.Services.Stores.States
{
    static class ModelConstants
    {
        public static class Providers
        {
            public const string None = "None";
            public const string Unity = "Unity";
            public const string Scenario = "Scenario";
            public const string Layer = "Layer";
            public const string Kinetix = "Kinetix";

            public static IEnumerable<string> EnumerateAll()
            {
                yield return None;
                yield return Unity;
                yield return Scenario;
                yield return Layer;
                yield return Kinetix;
            }
        }

        public static class Modalities
        {
            public const string None = "None";
            public const string Image = "Image";
            public const string Texture2d = "Texture2d";
            public const string Sound = "Sound";
            public const string Animate = "Animate";

            public static IEnumerable<string> EnumerateAll()
            {
                yield return None;
                yield return Image;
                yield return Texture2d;
                yield return Sound;
                yield return Animate;
            }
        }

        public static class Operations
        {
            public const string None = "None";

            ////////////////////////////////////// Generative

            /// <summary>
            /// Generic text prompt
            /// </summary>
            public const string TextPrompt = "TextPrompt";

            /// <summary>
            /// Generic single reference
            /// </summary>
            public const string ReferencePrompt = "ReferencePrompt";

            // Specific Image references
            public const string StyleReference = "StyleReference";
            public const string CompositionReference = "CompositionReference";
            public const string PoseReference = "PoseReference";
            public const string DepthReference = "DepthReference";
            public const string LineArtReference = "LineArtReference";
            public const string FeatureReference = "FeatureReference";
            public const string MaskReference = "MaskReference";
            public const string RecolorReference = "RecolorReference";
            //public const string GenerativeUpscale = "GenerativeUpscale";

            // Specific Animate references
            public const string MotionFrameReference = "MotionFrameReference";

            // Specific Texture2D references
            public const string Pbr = "Pbr";

            // Specific Style Training references
            public const string StyleTraining = "StyleTraining";
            public const string StyleTrainingStop = "StyleTrainingStop";
            public const string StyleTrainingDeletion = "StyleTrainingDeletion";

            ////////////////////////////////////// Transformative
            public const string Pixelate = "Pixelate";
            public const string RemoveBackground = "RemoveBackground";
            public const string Upscale = "Upscale";


            public static IEnumerable<string> EnumerateAll()
            {
                yield return None;
                yield return TextPrompt;
                yield return ReferencePrompt;
                yield return StyleReference;
                yield return CompositionReference;
                yield return PoseReference;
                yield return DepthReference;
                yield return LineArtReference;
                yield return FeatureReference;
                yield return MaskReference;
                yield return RecolorReference;
                //yield return GenerativeUpscale;
                yield return MotionFrameReference;
                yield return Pbr;
                yield return StyleTraining;
                yield return StyleTrainingStop;
                yield return StyleTrainingDeletion;
                yield return Pixelate;
                yield return RemoveBackground;
                yield return Upscale;
            }
        }

        public static ProviderEnum ConvertToProvider(string provider)
        {
            return Enum.TryParse<ProviderEnum>(provider, out var result) ? result : ProviderEnum.None;
        }

        public static ModalityEnum ConvertToModality(string modality)
        {
            return Enum.TryParse<ModalityEnum>(modality, out var result) ? result : ModalityEnum.None;
        }

        public static OperationSubTypeEnum ConvertToOperation(string operation)
        {
            return Enum.TryParse<OperationSubTypeEnum>(operation, out var result) ? result : OperationSubTypeEnum.None;
        }
    }
}
