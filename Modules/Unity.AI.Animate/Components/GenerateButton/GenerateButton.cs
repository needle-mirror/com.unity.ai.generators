using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Animate.Services.Stores.Actions;
using Unity.AI.Animate.Services.Stores.Actions.Payloads;
using Unity.AI.Animate.Services.Stores.Selectors;
using Unity.AI.Animate.Services.Utilities;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AI.Toolkit.Accounts.Manipulators;

namespace Unity.AI.Animate.Components
{
    [UxmlElement]
    partial class GenerateButton : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Animate/Components/GenerateButton/GenerateButton.uxml";
        const int k_ReenableDelay = 5000;

        readonly Button m_Button;
        readonly Label m_Label;
        readonly Label m_PointsIndicator;
        CancellationTokenSource m_CancellationTokenSource;

        [UxmlAttribute]
        public string text
        {
            get => m_Label.text;
            set => m_Label.text = value;
        }

        [UxmlAttribute]
        public bool quoteMonitor { get; set; } = true;

        public GenerateButton()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            this.AddManipulator(new GeneratorsSessionStatusTracker());

            m_Button = this.Q<Button>();
            m_Label = this.Q<Label>();
            m_PointsIndicator = this.Q<Label>("points-indicator");
            m_Button.clickable = new Clickable(() =>
                this.GetStoreApi().Dispatch(GenerationResultsActions.generateAnimationsMain, this.GetAsset()));

            this.Use(state => state.SelectGenerationAllowed(this), OnGenerationAllowedChanged);
            // ReSharper disable once AsyncVoidLambda
            this.UseAsset(async asset => {
                if (!quoteMonitor)
                    return;

                // ReSharper disable once AsyncVoidLambda
                this.Use(state => state.SelectGenerationValidationSettings(this), async settings => {
                    await EditorTask.Yield();
                    _ = this.GetStoreApi().Dispatch(GenerationResultsActions.quoteAnimationsMain, settings.asset);
                });

                await EditorTask.Yield();
                _ = this.GetStoreApi().Dispatch(GenerationResultsActions.checkDownloadRecovery, asset);
            });
            this.Use(state => state.SelectGenerationValidationResult(this), OnGenerationValidationResultsChanged);
        }

        void OnGenerationValidationResultsChanged(GenerationValidationResult result)
        {
            m_PointsIndicator.SetShown(result.cost > 0);
            m_PointsIndicator.text = result.cost.ToString();

            tooltip = result.feedback.Count > 0 ? string.Join("\n", result.feedback.Select(f => f.message)) : string.Empty;
        }

        void OnGenerationAllowedChanged(bool allowed)
        {
            m_Button.SetEnabled(allowed);

            m_CancellationTokenSource?.Cancel();
            m_CancellationTokenSource?.Dispose();
            m_CancellationTokenSource = null;
            if (!allowed)
            {
                m_CancellationTokenSource = new();
                _ = ReenableGenerateButton(m_CancellationTokenSource.Token);
            }
        }
        async Task ReenableGenerateButton(CancellationToken token)
        {
            try
            {
                await EditorTask.Delay(k_ReenableDelay, token);
                if (!token.IsCancellationRequested)
                    this.Dispatch(GenerationActions.setGenerationAllowed, new(this.GetAsset(), true));
            }
            catch (TaskCanceledException)
            {
                // The token was cancelled, so do nothing
            }
        }
    }
}
