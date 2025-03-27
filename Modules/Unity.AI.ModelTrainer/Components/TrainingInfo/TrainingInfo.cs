using Unity.AI.ModelTrainer.Services.Stores.Selectors;
using Unity.AI.ModelTrainer.Services.Stores.States;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.ModelTrainer.Components
{
    [UxmlElement]
    partial class TrainingInfo : VisualElement
    {
        const string k_Uxml =
            "Packages/com.unity.ai.generators/modules/Unity.AI.ModelTrainer/Components/TrainingInfo/TrainingInfo.uxml";

        float m_PreviousWidth;

        readonly Label m_ModelIdLabel;

        readonly Label m_ModelTypeLabel;

        readonly Label m_TrainingDurationTimeLabel;

        readonly Label m_TrainingStepsLabel;

        readonly Label m_TrainingImagesLabel;

        readonly Label m_LearningRateLabel;

        readonly VisualElement m_TrainingImagesContainer;

        public TrainingInfo()
        {
            var uxmlTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            uxmlTemplate.CloneTree(this);
            RegisterCallback<GeometryChangedEvent>(OnDetailsViewGeometryChanged);

            m_ModelIdLabel = this.Q<Label>("modelIdLabel");
            m_ModelTypeLabel = this.Q<Label>("modelTypeLabel");
            m_TrainingDurationTimeLabel = this.Q<Label>("trainingDurationTimeLabel");
            m_TrainingStepsLabel = this.Q<Label>("trainingStepsLabel");
            m_TrainingImagesLabel = this.Q<Label>("trainingImagesLabel");
            m_LearningRateLabel = this.Q<Label>("learningRateLabel");
            m_TrainingImagesContainer = this.Q<VisualElement>("trainingImagesContainer");

            this.Use(SessionSelectors.SelectSelectedModel, OnSelectedModelChanged);
        }

        void OnSelectedModelChanged(UserModel model)
        {
            m_ModelIdLabel.text = model?.id ?? "N/A";
            m_ModelTypeLabel.text = model?.type ?? "N/A";
            m_TrainingDurationTimeLabel.text = model != null ? (model.trainingEndDateTime - model.trainingStartDateTime).ToString() : "N/A";
            m_TrainingStepsLabel.text = model?.trainingSteps.ToString() ?? "N/A";
            m_LearningRateLabel.text = model?.learningRate.ToString() ?? "N/A";
            m_TrainingImagesLabel.text = model?.trainingImages.Length.ToString() ?? "N/A";

            m_TrainingImagesContainer.Clear();
            if (model == null)
                return;

            foreach (var image in model.trainingImages)
            {
                var thumbnail = new ThumbnailFieldItem();
                thumbnail.AddToClassList("details-view__thumbnail-field-item");
                thumbnail.text = image.prompt;
                thumbnail.thumbnail = image.texture;
                thumbnail.userData = image.id;
                m_TrainingImagesContainer.Add(thumbnail);
            }
        }

        void OnDetailsViewGeometryChanged(GeometryChangedEvent evt)
        {
            if (float.IsNaN(layout.width) || layout.width < 1)
                return;

            if (!Mathf.Approximately(m_PreviousWidth, layout.width))
            {
                m_PreviousWidth = layout.width;
                AdjustDetailsView();
            }
        }

        void AdjustDetailsView()
        {
            // set the width of details view keys to be the same (max)
            // as the width of the longest key

            var maxKeyWidth = 0f;
            var keys = this.Query<Label>(className: "details-view__key").Build();
            foreach (var key in keys)
            {
                maxKeyWidth = Mathf.Max(maxKeyWidth, key.layout.width);
            }

            foreach (var key in keys)
            {
                key.style.width = maxKeyWidth;
            }
        }
    }
}
