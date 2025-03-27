using System;
using System.IO;
using Unity.AI.Image.Services.Stores.Selectors;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AI.Generators.Contexts;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class BaseImageReference : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Image/Components/ImageReference/BaseImageReference.uxml";

        readonly DoodlePad m_DoodlePad;

        readonly Texture2D m_Texture;

        ~BaseImageReference() => m_Texture?.SafeDestroy();

        public BaseImageReference()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_Texture = new Texture2D(1, 1) {hideFlags = HideFlags.HideAndDontSave};

            AddToClassList("base-image-reference");
            m_DoodlePad = this.Q<DoodlePad>();
            m_DoodlePad.backGroundImageOpacity = 1;
            this.Use(state => state.SelectBaseImageBytesTimestamp(this), UpdateImage);
        }

        async void UpdateImage(Timestamp imageTimestamp)
        {
            var settings = this.GetState().SelectUnsavedAssetBytesSettings(this);
            if (settings.uri != null)
            {
                // Hit the cache if a cached uri was provided
                var height = resolvedStyle.height;
                if (height is <= 0 or float.NaN)
                    height = 128;

                var screenScaleFactor = this.GetContext<ScreenScaleFactor>()?.value ?? 1f;
                m_DoodlePad.backgroundImage = await TextureCache.GetPreview(settings.uri, (int)(height * screenScaleFactor));
            }
            else
            {
                // Load the image from the asset stream that we would send to the server if we were to call generate
                await using var assetStream = this.GetState().SelectUnsavedAssetStreamWithFallback(this);
                using var memoryStream = new MemoryStream();
                await assetStream.CopyToAsync(memoryStream);
                m_Texture.LoadImage(memoryStream.ToArray());
                m_DoodlePad.backgroundImage = m_Texture;
            }
        }
    }
}
