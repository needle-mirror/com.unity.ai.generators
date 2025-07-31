using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class AddToPromptButton : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Image/Components/AddToPromptButton/AddToPromptButton.uxml";

        const int k_ValidationDebounceDelayMs = 125;

        readonly Button m_AddToPrompt;

        readonly Dictionary<ImageReferenceType, bool> m_TypesValidationResults = new();

        CancellationTokenSource m_DebouncedValidationCts;

        public AddToPromptButton()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_AddToPrompt = this.Q<Button>("add-to-prompt-button");
            m_AddToPrompt.SetEnabled(false); // Start disabled

            m_AddToPrompt.clickable = new Clickable(() => {
                var asset = this.GetAsset();
                if (asset == null)
                    return;
                this.GetStoreApi().Dispatch(GenerationSettingsActions.openAddToPromptWindow, new AddToPromptWindowArgs(asset, this, m_TypesValidationResults));
            });

            this.UseAsset(_ => MarkDirty());
            this.Use(state => state.SelectSelectedModel(this)?.id, _ => MarkDirty());
            this.UseArray(state => state.SelectActiveReferencesTypes(this), _ => MarkDirty());

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent _) => MarkDirty();

        void OnDetachFromPanel(DetachFromPanelEvent _)
        {
            m_DebouncedValidationCts?.Cancel();
            m_DebouncedValidationCts?.Dispose();
            m_DebouncedValidationCts = null;
        }

        void MarkDirty()
        {
            if (!this.GetAsset().IsValid())
            {
                m_AddToPrompt.SetEnabled(false);
                m_AddToPrompt.tooltip = "No asset to validate.";
                return;
            }

            var typesToValidate = GetTypesToValidate();
            if (typesToValidate.Length == 0)
            {
                // No types to validate - just rebuild with empty results
                Rebuild(typesToValidate, Array.Empty<bool>());
                return;
            }

            // Try fast path with cached results first
            var (success, results) =
                Services.Stores.Actions.Backend.Validation.canAddReferencesToPromptCached((new AddImageReferenceTypeData(this.GetAsset(), typesToValidate),
                    this.GetStoreApi()));
            if (success)
            {
                Rebuild(typesToValidate, results);
                return;
            }

            // Not cached, need to do async validation with debouncing
            m_AddToPrompt.SetEnabled(false);
            m_AddToPrompt.tooltip = "Validating rules...";

            // Cancel any previous debouncing/validation attempt
            m_DebouncedValidationCts?.Cancel();
            m_DebouncedValidationCts?.Dispose();

            m_DebouncedValidationCts = new CancellationTokenSource();
            _ = PerformDebouncedValidationAsync(typesToValidate, m_DebouncedValidationCts.Token, m_DebouncedValidationCts);
        }

        async Task PerformDebouncedValidationAsync(ImageReferenceType[] typesToValidate, CancellationToken cancellationToken, CancellationTokenSource owningCts)
        {
            try
            {
                // Wait for the debounce period
                await EditorTask.Delay(k_ValidationDebounceDelayMs, cancellationToken);

                // If cancellation was requested during the delay, bail out
                if (cancellationToken.IsCancellationRequested)
                    return;

                var validationResults = await Services.Stores.Actions.Backend.Validation.canAddReferencesToPromptAsync(
                    (new AddImageReferenceTypeData(this.GetAsset(), typesToValidate), this.GetStoreApi()),
                    cancellationToken);

                // If cancellation was requested during validation, bail out
                if (cancellationToken.IsCancellationRequested)
                    return;

                Rebuild(typesToValidate, validationResults);
            }
            catch (OperationCanceledException)
            {
                // Debounce or validation was cancelled, do nothing.
                // A new call to MarkDirty will have started a new process.
            }
            catch (Exception)
            {
                m_AddToPrompt.SetEnabled(false); // Keep disabled on error
                m_AddToPrompt.tooltip = "Error during validation.";
            }
            finally
            {
                if (m_DebouncedValidationCts == owningCts)
                {
                    m_DebouncedValidationCts.Dispose();
                    m_DebouncedValidationCts = null;
                }
            }
        }

        void Rebuild(ImageReferenceType[] typesToValidate, bool[] results)
        {
            var hasItems = false;

            if (results != null && typesToValidate.Length == results.Length) // Ensure results are valid
            {
                m_TypesValidationResults.Clear();
                for (var i = 0; i < typesToValidate.Length; i++)
                {
                    if (m_TypesValidationResults.TryAdd(typesToValidate[i], results[i]))
                    {
                        hasItems = hasItems || results[i];
                    }
                }
            }

            m_AddToPrompt.SetEnabled(hasItems);
            m_AddToPrompt.tooltip = !hasItems ? "No controls available to add for this model." : "Add additional controls to guide generation using images as references.";
        }

        static ImageReferenceType[] GetTypesToValidate()
        {
            var typesToValidate = new List<ImageReferenceType>();
            foreach (var type in Enum.GetValues(typeof(ImageReferenceType)).Cast<ImageReferenceType>().OrderBy(t => t.GetDisplayOrder()))
            {
                if (type.GetRefinementModeForType().Contains(RefinementMode.Generation))
                    typesToValidate.Add(type);
            }
            return typesToValidate.ToArray();
        }
    }
}
