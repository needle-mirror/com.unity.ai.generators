using System;
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Generate;
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Generate.OperationSubTypes;

namespace Unity.AI.Image.Services.Utilities
{
    static class SuperProxyExtensions
    {
        public static ImageGenerateRequest GenerateWithReferences(this ImageGenerateRequestBuilder requestBuilder, TextPrompt textPrompt,
            ImagePrompt imagePrompt,
            StyleReference styleReference,
            CompositionReference compositionReference,
            PoseReference poseReference,
            DepthReference depthReference,
            LineArtReference lineArtReference,
            FeatureReference featureReference)
        {
            // Determine which GUIDs are provided.
            var hasPrompt = imagePrompt != null;
            var hasStyle = styleReference != null;
            var hasComposition = compositionReference != null;
            var hasPose = poseReference != null;
            var hasDepth = depthReference != null;
            var hasLineArt = lineArtReference != null;
            var hasFeature = featureReference != null;

            // Count how many references were provided.
            var providedCount = 0;
            providedCount += hasPrompt ? 1 : 0;
            providedCount += hasStyle ? 1 : 0;
            providedCount += hasComposition ? 1 : 0;
            providedCount += hasPose ? 1 : 0;
            providedCount += hasDepth ? 1 : 0;
            providedCount += hasLineArt ? 1 : 0;
            providedCount += hasFeature ? 1 : 0;

            return providedCount switch
            {
                // Strict: only the following cases are allowed:
                // •   0 references: use plain Generate(textPrompt)
                // •   1 reference: exactly one of the candidates
                // •   2 references: either prompt paired with one of the others OR exactly two of the non-prompt references.
                // Any other case returns failure.
                0 => requestBuilder.Generate(textPrompt),
                1 when hasPrompt => requestBuilder.GenerateWithReference(textPrompt, imagePrompt),
                1 when hasStyle => requestBuilder.GenerateWithReference(textPrompt, styleReference),
                1 when hasComposition => requestBuilder.GenerateWithReference(textPrompt, compositionReference),
                1 when hasPose => requestBuilder.GenerateWithReference(textPrompt, poseReference),
                1 when hasDepth => requestBuilder.GenerateWithReference(textPrompt, depthReference),
                1 when hasLineArt => requestBuilder.GenerateWithReference(textPrompt, lineArtReference),
                1 when hasFeature => requestBuilder.GenerateWithReference(textPrompt, featureReference),

                // When two references are given, we expect exactly one pair.
                // Case I: one is prompt and one is another type.
                2 when hasPrompt && hasStyle => requestBuilder.GenerateWithReferences(textPrompt, imagePrompt, styleReference),
                2 when hasPrompt && hasComposition => requestBuilder.GenerateWithReferences(textPrompt, imagePrompt, compositionReference),
                2 when hasPrompt && hasPose => requestBuilder.GenerateWithReferences(textPrompt, imagePrompt, poseReference),
                2 when hasPrompt && hasDepth => requestBuilder.GenerateWithReferences(textPrompt, imagePrompt, depthReference),
                2 when hasPrompt && hasLineArt => requestBuilder.GenerateWithReferences(textPrompt, imagePrompt, lineArtReference),
                2 when hasPrompt && hasFeature => requestBuilder.GenerateWithReferences(textPrompt, imagePrompt, featureReference),

                // Case II: No prompt, so two non-prompt references.
                2 when hasStyle && hasComposition => requestBuilder.GenerateWithReferences(textPrompt, styleReference, compositionReference),
                2 when hasStyle && hasPose => requestBuilder.GenerateWithReferences(textPrompt, styleReference, poseReference),
                2 when hasStyle && hasDepth => requestBuilder.GenerateWithReferences(textPrompt, styleReference, depthReference),
                2 when hasStyle && hasLineArt => requestBuilder.GenerateWithReferences(textPrompt, styleReference, lineArtReference),
                2 when hasStyle && hasFeature => requestBuilder.GenerateWithReferences(textPrompt, styleReference, featureReference),
                2 when hasComposition && hasPose => requestBuilder.GenerateWithReferences(textPrompt, compositionReference, poseReference),
                2 when hasComposition && hasDepth => requestBuilder.GenerateWithReferences(textPrompt, compositionReference, depthReference),
                2 when hasComposition && hasLineArt => requestBuilder.GenerateWithReferences(textPrompt, compositionReference, lineArtReference),
                2 when hasComposition && hasFeature => requestBuilder.GenerateWithReferences(textPrompt, compositionReference, featureReference),
                2 when hasPose && hasDepth => requestBuilder.GenerateWithReferences(textPrompt, poseReference, depthReference),
                2 when hasPose && hasLineArt => requestBuilder.GenerateWithReferences(textPrompt, poseReference, lineArtReference),
                2 when hasPose && hasFeature => requestBuilder.GenerateWithReferences(textPrompt, poseReference, featureReference),
                2 when hasDepth && hasLineArt => requestBuilder.GenerateWithReferences(textPrompt, depthReference, lineArtReference),
                2 when hasDepth && hasFeature => requestBuilder.GenerateWithReferences(textPrompt, depthReference, featureReference),
                2 when hasLineArt && hasFeature => requestBuilder.GenerateWithReferences(textPrompt, lineArtReference, featureReference),

                // In all other cases (more than 2 provided or an unhandled combination)
                // return the failure constant.
                _ => throw new UnhandledReferenceCombinationException()
            };
        }
    }
}
