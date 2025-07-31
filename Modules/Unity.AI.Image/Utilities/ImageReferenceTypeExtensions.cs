using System;
using System.Collections.Generic;
using System.Reflection;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Utilities
{
    enum ImageReferenceType
    {
        [DisplayOrder(2)]
        [ImageReferenceName("composition")]
        [RefinementModes(RefinementMode.Generation)]
        [DisplayName("Composition")]
        [OperationSubTypes(OperationSubTypeEnum.CompositionReference)]
        CompositionImage,

        [DisplayOrder(4)]
        [ImageReferenceName("depth")]
        [RefinementModes(RefinementMode.Generation)]
        [DisplayName("Depth")]
        [OperationSubTypes(OperationSubTypeEnum.DepthReference)]
        DepthImage,

        [DisplayOrder(6)]
        [ImageReferenceName("feature")]
        [RefinementModes(RefinementMode.Generation)]
        [DisplayName("Feature")]
        [OperationSubTypes(OperationSubTypeEnum.FeatureReference)]
        FeatureImage,

        [DisplayOrder(5)]
        [ImageReferenceName("colorSketch")]
        [RefinementModes(RefinementMode.Generation)]
        [DisplayName("Line Art")]
        [OperationSubTypes(OperationSubTypeEnum.LineArtReference)]
        LineArtImage,

        [DisplayOrder(8)]
        [ImageReferenceName("inPaintMask")]
        [RefinementModes(RefinementMode.Inpaint)]
        [DisplayName("In-paint Mask")]
        [OperationSubTypes(OperationSubTypeEnum.MaskReference)]
        InPaintMaskImage,

        [DisplayOrder(7)]
        [ImageReferenceName("palette")]
        [RefinementModes(RefinementMode.Recolor)]
        [DisplayName("Palette")]
        [OperationSubTypes(OperationSubTypeEnum.RecolorReference)]
        PaletteImage,

        [DisplayOrder(3)]
        [ImageReferenceName("pose")]
        [RefinementModes(RefinementMode.Generation)]
        [DisplayName("Pose")]
        [OperationSubTypes(OperationSubTypeEnum.PoseReference)]
        PoseImage,

        [DisplayOrder(0)]
        [ImageReferenceName("imagePrompt")]
        [RefinementModes(RefinementMode.Generation)]
        [DisplayName("Prompt")]
        [InternalDisplayName("Image")]
        [OperationSubTypes(OperationSubTypeEnum.TextPrompt)]
        PromptImage,

        [DisplayOrder(1)]
        [ImageReferenceName("style")]
        [RefinementModes(RefinementMode.Generation)]
        [DisplayName("Style")]
        [OperationSubTypes(OperationSubTypeEnum.StyleReference)]
        StyleImage
    }

    [AttributeUsage(AttributeTargets.Field)]
    sealed class DisplayOrderAttribute : Attribute
    {
        public int order { get; }
        public DisplayOrderAttribute(int order) => this.order = order;
    }

    [AttributeUsage(AttributeTargets.Field)]
    sealed class ImageReferenceNameAttribute : Attribute
    {
        public string name { get; }
        public ImageReferenceNameAttribute(string name) => this.name = name;
    }

    [AttributeUsage(AttributeTargets.Field)]
    sealed class RefinementModesAttribute : Attribute
    {
        public RefinementMode[] modes { get; }
        public RefinementModesAttribute(params RefinementMode[] modes) => this.modes = modes;
    }

    [AttributeUsage(AttributeTargets.Field)]
    sealed class OperationSubTypesAttribute : Attribute
    {
        public OperationSubTypeEnum[] subTypes { get; }
        public OperationSubTypesAttribute(params OperationSubTypeEnum[] subTypes) => this.subTypes = subTypes;
    }

    [AttributeUsage(AttributeTargets.Field)]
    sealed class DisplayNameAttribute : Attribute
    {
        public string name { get; }
        public DisplayNameAttribute(string name) => this.name = name;
    }

    [AttributeUsage(AttributeTargets.Field)]
    sealed class InternalDisplayNameAttribute : Attribute
    {
        public string name { get; }
        public InternalDisplayNameAttribute(string name) => this.name = name;
    }

    static class ImageReferenceTypeExtensions
    {
        public static byte[] GetDoodlePadData(this ImageReferenceType type, VisualElement imageReference)
        {
            var selector = GetDoodleSelectorForType(type);
            return selector?.Invoke(imageReference.GetState(), imageReference);
        }

        public static void SetDoodlePadData(this ImageReferenceType type, VisualElement imageReference, byte[] data)
        {
            imageReference.Dispatch(GenerationSettingsActions.setImageReferenceDoodle, new (type, data));
            imageReference.Dispatch(GenerationSettingsActions.setImageReferenceMode, new (type, ImageReferenceMode.Doodle));
            imageReference.Dispatch(GenerationSettingsActions.setImageReferenceActive, new (type, true));
        }

        public static Func<IState, VisualElement, bool> GetIsActiveSelectorForType(this ImageReferenceType type) =>
            (state, element) => state.SelectGenerationSetting(element).imageReferences[(int)type].isActive;

        public static Func<IState, VisualElement, byte[]> GetDoodleSelectorForType(this ImageReferenceType type) =>
            (state, element) => state.SelectGenerationSetting(element).imageReferences[(int)type].doodle;

        static bool TryGetAttribute<T>(this ImageReferenceType type, out T attribute) where T : Attribute
        {
            attribute = null;

            var memberInfo = type.GetType().GetMember(type.ToString());
            if (memberInfo.Length > 0)
                attribute = memberInfo[0].GetCustomAttribute<T>();

            return attribute != null;
        }

        public static string GetImageReferenceName(this ImageReferenceType type) =>
            !type.TryGetAttribute<ImageReferenceNameAttribute>(out var attr) ? null : attr.name;

        public static HashSet<RefinementMode> GetRefinementModeForType(this ImageReferenceType type) =>
            !type.TryGetAttribute<RefinementModesAttribute>(out var attr) ? new HashSet<RefinementMode>() : new HashSet<RefinementMode>(attr.modes);

        public static string GetDisplayNameForType(this ImageReferenceType type) =>
            !type.TryGetAttribute<DisplayNameAttribute>(out var attr) ? null : attr.name;

        public static string GetInternalDisplayNameForType(this ImageReferenceType type) =>
            !type.TryGetAttribute<InternalDisplayNameAttribute>(out var attr) ? GetDisplayNameForType(type) : attr.name;

        public static int GetDisplayOrder(this ImageReferenceType type) => !type.TryGetAttribute<DisplayOrderAttribute>(out var attr) ? 0 : attr.order;
        public static HashSet<OperationSubTypeEnum> GetOperationSubTypeEnumForType(this ImageReferenceType type) =>
            !type.TryGetAttribute<OperationSubTypesAttribute>(out var attr) ? new HashSet<OperationSubTypeEnum>() : new HashSet<OperationSubTypeEnum>(attr.subTypes);
    }
}
