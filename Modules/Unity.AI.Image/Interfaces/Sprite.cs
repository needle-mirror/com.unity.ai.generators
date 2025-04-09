using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Image.Components;
using Unity.AI.Image.Services.Contexts;
using Unity.AI.Image.Services.SessionPersistence;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Utilities;
using Unity.AI.Image.Windows;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Contexts;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.AI.Image.Interfaces
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public enum AIMode
    {
        Generation = RefinementMode.Generation,
        RemoveBackground = RefinementMode.RemoveBackground,
        Upscale = RefinementMode.Upscale,
        Pixelate = RefinementMode.Pixelate,
        Recolor = RefinementMode.Recolor,
        Inpaint = RefinementMode.Inpaint
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public enum ControlType
    {
        CompositionImage = ImageReferenceType.CompositionImage,
        DepthImage = ImageReferenceType.DepthImage,
        FeatureImage = ImageReferenceType.FeatureImage,
        LineArtImage = ImageReferenceType.LineArtImage,
        InPaintMaskImage = ImageReferenceType.InPaintMaskImage,
        PaletteImage = ImageReferenceType.PaletteImage,
        PoseImage = ImageReferenceType.PoseImage,
        PromptImage = ImageReferenceType.PromptImage,
        StyleImage = ImageReferenceType.StyleImage
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class SpriteEditor
    {
        public static string UnityAIGeneratedLabel => Toolkit.Compliance.Legal.UnityAIGeneratedLabel;

        public static string GetGeneratedAssetsPath(string assetGuid) =>
            Generators.Asset.AssetReferenceExtensions.GetGeneratedAssetsPath(assetGuid);

        public static void SetAIMode(this VisualElement ve, AIMode mode) =>
            ve.Dispatch(GenerationSettingsActions.setRefinementMode, (RefinementMode)mode);

        public static Func<bool> AddAIModeHandler(this VisualElement ve, Action<AIMode> handler) =>
            ve.Use(state => state.SelectRefinementMode(ve), result => handler?.Invoke((AIMode)result)).Invoke;

        public static Func<bool> AddGenerationSelectionHandler(this VisualElement ve, Action<Uri> handler) =>
            ve.Use(state => state.SelectSelectedGeneration(ve), result => handler?.Invoke(result.uri)).Invoke;

        public static void SetGenerationSelection(this VisualElement ve, Uri uri, bool replaceAsset = false, bool shouldAskForConfirmation = true) =>
            ve.Dispatch(GenerationResultsActions.selectGeneration, new SelectGenerationData(
                ve.GetAsset(), new TextureResult { uri = uri }, replaceAsset, shouldAskForConfirmation));

        public static Func<bool> AddGenerationSelectionHandler(this VisualElement ve, Action<Task<Texture2D>> handler) =>
            ve.Use(state => state.SelectSelectedGeneration(ve), result => handler?.Invoke(TextureCache.GetTexture(result.uri))).Invoke;

        public static VisualElement GetImageReferenceElement(this ControlType type, VisualElement rootUI) =>
            rootUI.Q<VisualElement>(type.ToImageReferenceType().GetImageReferenceName());

        public static ObjectField GetObjectFieldElement(this ControlType type, VisualElement imageReference) => imageReference.Q<ObjectField>();

        public static VisualElement GetDoodlePadElement(this ControlType type, VisualElement imageReference) => imageReference.Q<DoodlePad>();

        public static Button GetDoodlePadEditButton(this ControlType type, VisualElement imageReference) => imageReference.Q<Button>("edit-image-reference");

        public static void SetImageReferenceAsSelected(this ControlType type, VisualElement imageReference, bool selected) =>
            imageReference.SetSelected(selected);

        public static Func<bool> AddIsActiveChangedHandler(this ControlType type, VisualElement imageReference, Action<bool> handler)
        {
            if (handler == null)
                return () => true;

            var selector = type.ToImageReferenceType().GetIsActiveSelectorForType();
            return imageReference.Use(state => selector(state, imageReference), handler).Invoke;
        }

        public static void EnableReplaceBlankAsset(VisualElement ve, bool enable) =>
            ve.Dispatch(GenerationSettingsActions.setReplaceBlankAsset, enable);

        public static void EnableReplaceRefinementAsset(VisualElement ve, bool enable) =>
            ve.Dispatch(GenerationSettingsActions.setReplaceRefinementAsset, enable);

        /// <summary>
        /// GetUnsavedAssetBytes
        /// </summary>
        /// <returns>Bytes as a Base64 string.</returns>
        public static byte[] GetUnsavedAssetBytes(this ControlType type, VisualElement imageReference) =>
            imageReference.GetState().SelectUnsavedAssetBytes(imageReference);

        /// <summary>
        /// Set unsaved asset bytes
        /// </summary>
        /// <param name="type">The type of control.</param>
        /// <param name="imageReference">The visual element that's in context.</param>
        /// <param name="data">Bytes of data as Base64 string.</param>
        public static void SetUnsavedAssetBytes(this ControlType type, VisualElement imageReference, byte[] data) =>
            imageReference.Dispatch(GenerationSettingsActions.setUnsavedAssetBytes, new UnsavedAssetBytesData(imageReference.GetAsset(), data));

        /// <summary>
        /// Change Handler getting called when unsaved asset bytes changes.
        /// </summary>
        /// <param name="type">The type of control.</param>
        /// <param name="imageReference">The visual element that's in context.</param>
        /// <param name="handler">Receives the bytes which are a base64 string.</param>
        /// <returns></returns>
        public static Func<bool> AddUnsavedAssetBytesChangedHandler(this ControlType type, VisualElement imageReference,
            Action<byte[]> handler) =>
            imageReference.Use(state => state.SelectUnsavedAssetBytes(imageReference), handler).Invoke;

        public static byte[] GetDoodlePadData(this ControlType type, VisualElement imageReference) =>
            type.ToImageReferenceType().GetDoodlePadData(imageReference);

        public static void SetDoodlePadData(this ControlType type, VisualElement imageReference, byte[] data) =>
            type.ToImageReferenceType().SetDoodlePadData(imageReference, data);

        public static Func<bool> AddDoodleDataChangedHandler(this ControlType type, VisualElement imageReference,
            Action<byte[]> handler)
        {
            if (handler == null)
                return () => true;

            var selector = type.ToImageReferenceType().GetDoodleSelectorForType();
            if (selector == null)
                return () => true;
            return imageReference.Use(state => selector(state, imageReference), handler).Invoke;
        }

        public static AIMode GetMode(this ControlType type) => (AIMode)type.ToImageReferenceType().GetRefinementModeForType().FirstOrDefault();

        public static string GetDisplayName(this ControlType type) => type.ToImageReferenceType().GetDisplayNameForType();

        public static bool IsActive(this ControlType type, VisualElement imageReference) =>
            type.ToImageReferenceType().GetIsActiveSelectorForType()(imageReference.GetState(), imageReference);

        public static Func<bool> AddGenerationTriggeredHandler(this VisualElement ve, Action handler)
        {
            if (handler == null)
                return () => true;
            var previousValue = ve.GetState().SelectGenerationCount(ve);
            return ve.Use(state => state.SelectGenerationCount(ve), newValue =>
            {
                // Call the handler only when the generation count changes.
                // Use() will call the callback even if the selector returns the same value
                // when the UI is re-attached to a Panel.
                if (newValue != previousValue)
                {
                    previousValue = newValue;
                    handler();
                }
            }).Invoke;
        }

        public static void OpenGeneratorWindow(string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath) && AssetDatabase.AssetPathExists(assetPath))
                TextureGeneratorInspectorButton.OpenGenerationWindow(assetPath);
        }

        internal static ImageReferenceType ToImageReferenceType(this ControlType type) => (ImageReferenceType)type;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class VisualElementExtensions
    {
        public static void SetObjectContext(this VisualElement ve, Object obj)
        {
            var assetPath = AssetDatabase.GetAssetPath(obj);
            var asset = new AssetReference { guid = AssetDatabase.AssetPathToGUID(assetPath) };
            ve.ProvideContext(StoreExtensions.storeKey, SharedStore.Store);
            ve.ProvideContext(ExternalDoodleEditor.key, new ExternalDoodleEditor(true));
            ve.SetAssetContext(asset);
        }
    }
}
