﻿using System;
using System.IO;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class AssetTitleCard : VisualElement
    {
        Object m_Asset;

        readonly UnityEngine.UIElements.Image m_AssetImage;
        readonly Label m_AssetName;

        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Image/Components/AssetTitleCard/AssetTitleCard.uxml";

        public AssetTitleCard()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_AssetImage = this.Q<UnityEngine.UIElements.Image>();
            m_AssetName = this.Q<Label>();

            AssetRenameWatcher.OnAssetMoved += (oldPath, newPath) =>
            {
                if (this.GetAsset().GetPath() == oldPath)
                {
                    var newName = Path.GetFileNameWithoutExtension(newPath);
                    m_AssetName.text = m_Asset ? newName : $"{newName} (deleted)";
                }
            };
        }

        public void SetAsset(AssetReference assetReference)
        {
            m_Asset = AssetDatabase.LoadAssetAtPath<Object>(assetReference.GetPath());
            m_AssetName.text = m_Asset ? m_Asset.name : $"{Path.GetFileNameWithoutExtension(assetReference.GetPath())} (deleted)";

            var content = EditorGUIUtility.ObjectContent(m_Asset, m_Asset ? m_Asset.GetType() : typeof(Texture2D));
            m_AssetImage.image = content.image;

            EnableInClassList("hide", !assetReference.IsValid());
            EnableInClassList("flex", assetReference.IsValid());
        }
    }
}
