using System;
using System.Linq;
using Unity.AI.ModelSelector.Services.Stores.Selectors;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.ModelSelector.Services.Utilities;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using TextureSizeHint = Unity.AI.Generators.UI.Utilities.TextureSizeHint;

namespace Unity.AI.ModelSelector.Components
{
    class ModelTile : VisualElement
    {
        ModelSettings m_Model;

        readonly ModelTileCarousel m_ModelTileCarousel;
        readonly Image m_PartnerIcon;
        readonly ModelTitleCard m_ModelTitleCard;
        readonly Clickable m_Clickable;

        internal Action<ModelSettings> showModelDetails;

        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.ModelSelector/Components/ModelSelector/ModelTile.uxml";

        public ModelSettings model => m_Model;

        public ModelTile()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            AddToClassList("model-tile");

            m_ModelTileCarousel = this.Q<ModelTileCarousel>();
            m_ModelTitleCard = this.Q<ModelTitleCard>();
            m_PartnerIcon = this.Q<Image>(className: "model-tile-icon");

            this.Use(state => state.SelectSelectedModel(), OnModelSelected);

            m_Clickable = new Clickable(OnClick);
            m_ModelTitleCard.AddManipulator(m_Clickable);
        }

        void OnClick() => showModelDetails?.Invoke(m_Model);

        void OnModelSelected(ModelSettings modelSettings) => EnableInClassList("is-selected", m_Model != null && modelSettings != null && m_Model.id == modelSettings.id);

        public async void SetModel(ModelSettings modelSettings)
        {
            m_Model = modelSettings;
            _ = m_ModelTitleCard.SetModelAsync(m_Model);
            tooltip = m_Model.description;
            if (Unsupported.IsDeveloperMode())
                tooltip += $"\n{m_Model.id}";

            if (this.GetState() != null)
                OnModelSelected(this.GetState().SelectSelectedModel());

            m_ModelTileCarousel.SetImages(m_Model.thumbnails.Select(s => new Uri(s)));

            if (m_PartnerIcon != null && !string.IsNullOrEmpty(modelSettings.icon))
                m_PartnerIcon.image = await TextureCache.GetPreview(new Uri(modelSettings.icon), (int)TextureSizeHint.Partner);
        }
    }
}
