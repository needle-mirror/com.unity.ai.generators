﻿using System;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class SelectedAssetTitleCard : AssetTitleCard
    {
        readonly Clickable m_Clickable;

        public SelectedAssetTitleCard()
        {
            m_Clickable = new Clickable(OnClick);
            this.AddManipulator(m_Clickable);
            this.UseAsset(SetAsset);
            this.Use(state => state.SelectAssetExists(this), OnAssetExistsChanged);
        }

        void OnClick()
        {
            var assetSettings = this.GetAsset();
            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetSettings.GetPath());

            if (asset)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        void OnAssetExistsChanged(bool _) => SetAsset(this.GetAsset());
    }
}
