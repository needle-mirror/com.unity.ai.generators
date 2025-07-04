﻿using System;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Utilities;
using Unity.AI.Generators.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class FeatureImageReference : VisualElement, IImageReference
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Image/Components/ImageReference/FeatureImageReference.uxml";

        public FeatureImageReference()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            AddToClassList("feature-image-reference");
            this.AddManipulator(new PingManipulator());
            this.Bind<FeatureImageReference, Image.Services.Stores.States.FeatureImageReference>();
        }

        public ImageReferenceType type => ImageReferenceType.FeatureImage;

        public bool showBaseImageByDefault => false;

        public bool invertStrength => false;

        public bool allowEdit => false;
    }
}
