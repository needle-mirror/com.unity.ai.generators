using System.Collections.Generic;
using Unity.AI.ModelTrainer.Services.Stores.Actions;
using Unity.AI.ModelTrainer.Services.Stores.Selectors;
using Unity.AI.ModelTrainer.Services.Stores.States;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.ModelTrainer.Components
{
    [UxmlElement]
    partial class DetailsView : VisualElement
    {
        const string k_EmptyViewUxml = "Packages/com.unity.ai.generators/modules/Unity.AI.ModelTrainer/Components/DetailsView/EmptyDetailsView.uxml";

        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.ModelTrainer/Components/DetailsView/ModelDetailsView.uxml";

        readonly VisualElement m_DetailsView;

        readonly VisualElement m_EmptyView;

        readonly Button m_DeleteButton;

        readonly Button m_TrainButton;

        readonly TextField m_NameField;

        readonly ToggleButtonGroup m_BaseModelToggleGroup;

        readonly VisualElement m_ModelAuthoringView;

        readonly TrainingInfo m_TrainingInfo;

        public DetailsView()
        {
            m_DetailsView = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml).Instantiate();
            m_EmptyView = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_EmptyViewUxml).Instantiate();

            m_DeleteButton = m_DetailsView.Q<Button>("deleteButton");
            m_DeleteButton.clicked += () =>
            {
                var delete =  EditorUtility.DisplayDialog(
                    "Delete Model",
                    "Are you sure you want to delete this model?",
                    "Yes", "No");
                if (delete)
                    this.GetStoreApi().Dispatch(SessionActions.deleteModel);
            };

            m_TrainButton = m_DetailsView.Q<Button>("trainButton");
            m_TrainButton.clicked += () =>
            {
                var selectedModel = this.GetState().SelectSelectedModel();
                var validationErrorMessages = IsValidForTraining(selectedModel);
                if (validationErrorMessages.Count > 0)
                {
                    var message = string.Join("\n", validationErrorMessages);
                    EditorUtility.DisplayDialog("Invalid Model", message, "Ok");
                    return;
                }

                this.GetStoreApi().Dispatch(SessionActions.trainModel.Invoke(selectedModel));
                m_TrainButton.SetEnabled(false);
            };

            m_NameField = m_DetailsView.Q<TextField>("nameField");
            m_NameField.RegisterValueChangedCallback(evt =>
            {
                this.GetStoreApi().Dispatch(SessionActions.setName.Invoke(evt.newValue));
            });

            m_BaseModelToggleGroup = m_DetailsView.Q<ToggleButtonGroup>("baseModelToggleGroup");
            m_TrainingInfo = m_DetailsView.Q<TrainingInfo>("trainingInfo");
            m_ModelAuthoringView = m_DetailsView.Q<VisualElement>("modelAuthoring");

            Add(m_EmptyView);

            this.Use(SessionSelectors.SelectSelectedModel, OnSelectedModelChanged);
            this.Use(SessionSelectors.SelectBaseModels, OnBaseModelsChanged);
            this.Use(SessionSelectors.SelectName, OnNameChanged);
            this.Use(SessionSelectors.SelectBaseModelId, OnBaseModelChanged);
            this.Use(SessionSelectors.SelectTrainingStatus, OnTrainingStatusChanged);
        }

        static List<string> IsValidForTraining(UserModel model)
        {
            var validationErrorMessages = new List<string>();
            if (string.IsNullOrEmpty(model.name))
                validationErrorMessages.Add("Model name cannot be empty");
            if (string.IsNullOrEmpty(model.baseModelId))
                validationErrorMessages.Add("Base model must be selected");
            if (model.trainingImages is not { Length: >= 5 and <= 100 })
                validationErrorMessages.Add("Model must have between 5 and 100 training images");
            return validationErrorMessages;
        }

        void OnTrainingStatusChanged(TrainingStatus status)
        {
            m_TrainingInfo.EnableInClassList("unity-hidden", status is not (TrainingStatus.Succeeded or TrainingStatus.Failed));
            m_ModelAuthoringView.EnableInClassList("unity-hidden", status is not TrainingStatus.NotStarted);
            m_TrainButton.SetEnabled(status is TrainingStatus.NotStarted);
            m_BaseModelToggleGroup.SetEnabled(status is TrainingStatus.NotStarted);
            m_NameField.SetEnabled(status is TrainingStatus.NotStarted);
        }

        void OnToggleGroupChanged(ChangeEvent<ToggleButtonGroupState> evt)
        {
            for (var i = 0; i < evt.newValue.length; i++)
            {
                if (evt.newValue[i])
                {
                    var baseModelId = (string)m_BaseModelToggleGroup[i].userData;
                    this.GetStoreApi().Dispatch(SessionActions.setBaseModel.Invoke(baseModelId));
                    break;
                }
            }
        }

        void OnBaseModelsChanged(IEnumerable<BaseModel> baseModels)
        {
            m_BaseModelToggleGroup.UnregisterValueChangedCallback(OnToggleGroupChanged);
            m_BaseModelToggleGroup.Clear();
            foreach (var baseModel in baseModels)
            {
                var toggle = new Button { text = baseModel.name };
                toggle.userData = baseModel.id;
                m_BaseModelToggleGroup.Add(toggle);
            }
            if (m_BaseModelToggleGroup.childCount > 0)
                // we need to wait 1 frame because ToggleButtonGroup forces a value changed event when mounted
                m_BaseModelToggleGroup.schedule.Execute(() =>
                    m_BaseModelToggleGroup.RegisterValueChangedCallback(OnToggleGroupChanged));
        }

        void OnBaseModelChanged(string baseModelId)
        {
            var state = m_BaseModelToggleGroup.value;
            for (var i = 0; i < m_BaseModelToggleGroup.childCount; i++)
            {
                var toggle = m_BaseModelToggleGroup[i];
                state[i] = (string)toggle.userData == baseModelId;
            }
            m_BaseModelToggleGroup.SetValueWithoutNotify(state);
        }

        void OnNameChanged(string text)
        {
            m_NameField.SetValueWithoutNotify(text);
        }

        void OnSelectedModelChanged(UserModel model)
        {
            if (model != null && m_EmptyView.parent != null)
            {
                m_EmptyView.RemoveFromHierarchy();
                Add(m_DetailsView);
            }
            else if (model == null && m_EmptyView.parent == null)
            {
                m_DetailsView.RemoveFromHierarchy();
                Add(m_EmptyView);
            }
        }
    }
}
