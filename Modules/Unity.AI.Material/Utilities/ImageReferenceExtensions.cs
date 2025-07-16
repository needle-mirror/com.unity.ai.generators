﻿using System;
using Unity.AI.Material.Services.Stores.Actions.Creators;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Material.Services.Utilities
{
    interface IImageReference {}

    static class ImageReferenceExtensions
    {
        public static void Bind<T>(this T element,
            AssetActionCreator<AssetReference> setImageReferenceAsset,
            Func<IState, VisualElement, AssetReference> selectImageReferenceAsset) where T: VisualElement, IImageReference
        {
            var objectField = element.Q<ObjectField>();
            var settingsButton = element.Q<Button>("image-reference-settings-button");

            objectField.AddManipulator(new ScaleToFitObjectFieldImage());
            objectField.RegisterValueChangedCallback(evt =>
                element.Dispatch(setImageReferenceAsset, AssetReferenceExtensions.FromObject(evt.newValue as Texture)));

            settingsButton.clicked += () => ShowMenu();
            objectField.RegisterCallback<ContextClickEvent>(_ => ShowMenu(true));

            element.Use(state => selectImageReferenceAsset(state, element), asset => objectField.value = asset.GetObject());
            return;

            void ShowMenu(bool isContextClick = false)
            {
                var menu = new GenericMenu();
                if (objectField.value)
                    menu.AddItem(new GUIContent("Clear"), false, Clear);
                else
                    menu.AddDisabledItem(new GUIContent("Clear"));

                if (isContextClick)
                    menu.ShowAsContext();
                else
                    menu.DropDown(settingsButton.worldBound);
            }

            void Clear()
            {
                objectField.value = null;
            }
        }

        public static void BindWithStrength<T>(this T element,
            AssetActionCreator<float> setImageReferenceStrength,
            Func<IState, VisualElement, float> selectImageReferenceStrength,
            bool invertStrength = false) where T: VisualElement, IImageReference
        {
            var strengthSlider = element.Q<SliderInt>(classes: "image-reference-strength-slider");

            strengthSlider.RegisterValueChangedCallback(evt =>
                element.Dispatch(setImageReferenceStrength, invertStrength ? 1 - evt.newValue / 100.0f : evt.newValue / 100.0f));

            element.Use(state => selectImageReferenceStrength(state, element), strength => {
                strengthSlider.SetValueWithoutNotify(Mathf.RoundToInt((invertStrength ? 1 - strength : strength) * 100));
            });
        }
    }
}
