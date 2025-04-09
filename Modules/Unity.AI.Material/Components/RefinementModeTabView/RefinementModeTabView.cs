﻿using System;
using Unity.AI.Material.Services.Stores.Actions;
using Unity.AI.Material.Services.Stores.Selectors;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Material.Components
{
    [UxmlElement]
    partial class RefinementModeTabView : TabView
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Material/Components/RefinementModeTabView/RefinementModeTabView.uxml";

        public RefinementModeTabView()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            AddToClassList("refinement-mode-tabview");

            activeTabChanged += (_, _) =>
            {
                if (this.GetStoreApi() == null)
                    return;
                this.Dispatch(GenerationSettingsActions.setRefinementMode,
                    (RefinementMode)Math.Clamp(selectedTabIndex, 0, Enum.GetNames(typeof(RefinementMode)).Length));
            };

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                var refinementMode = this.GetState().SelectRefinementMode(this);
                selectedTabIndex = (int)refinementMode;
            });

            this.Use(state => state.SelectRefinementMode(this), refinementMode => selectedTabIndex = (int)refinementMode);
        }
    }
}
