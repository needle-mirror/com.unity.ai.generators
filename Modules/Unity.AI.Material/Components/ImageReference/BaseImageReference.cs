﻿using System;
using Unity.AI.Material.Services.Stores.Selectors;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AI.Generators.UIElements.Extensions;

namespace Unity.AI.Material.Components
{
    [UxmlElement]
    partial class BaseImageReference : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Material/Components/ImageReference/BaseImageReference.uxml";

        readonly VisualElement m_Element;

        public BaseImageReference()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            AddToClassList("base-image-reference");
            m_Element = this.Q<VisualElement>("base-image-reference");;
            this.Use(state => state.SelectBaseImageReferenceBackground(this)?.GetInstanceID() ?? -1, UpdateImage);
        }

        void UpdateImage(int _) => m_Element.style.backgroundImage = this.GetState().SelectBaseImageReferenceBackground(this);
    }
}
