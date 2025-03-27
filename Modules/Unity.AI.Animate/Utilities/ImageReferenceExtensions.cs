using System;
using Unity.AI.Animate.Services.Stores.Actions.Creators;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace Unity.AI.Animate.Services.Utilities
{
    interface IInputReference {}

    static class InputReferenceExtensions
    {
        public static void Bind<T>(this T element,
            AssetActionCreator<AssetReference> setInputReferenceAsset,
            Func<IState, VisualElement, AssetReference> selectInputReferenceAsset) where T: VisualElement, IInputReference
        {
            var objectField = element.Q<ObjectField>();
            //var objectFieldDisplayIcon = element.Q<Image>(className: "unity-object-field-display__icon");

            //objectFieldDisplayIcon.AddManipulator(new ScaleToFitImage());
            objectField.RegisterValueChangedCallback(evt =>
                element.Dispatch(setInputReferenceAsset, AssetReferenceExtensions.FromObject(evt.newValue as VideoClip)));

            element.Use(state => selectInputReferenceAsset(state, element), asset => objectField.value = asset.GetObject());
        }
    }
}
