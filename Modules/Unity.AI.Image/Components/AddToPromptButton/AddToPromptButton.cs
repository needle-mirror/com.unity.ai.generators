using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Actions.Creators;
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

        DropdownMenu m_AllowedOperatorsMenu;
        readonly Button m_AddToPrompt;

        CancellationTokenSource m_DebouncedValidationCts;

        public AddToPromptButton()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_AddToPrompt = this.Q<Button>("add-to-prompt-button");
            m_AddToPrompt.SetEnabled(false); // Start disabled
            m_AddToPrompt.clicked += () => {
                if (m_AllowedOperatorsMenu != null && m_AllowedOperatorsMenu.MenuItems().Any())
                    m_AllowedOperatorsMenu.Show(m_AddToPrompt.worldBound);
            };

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
            var typesToValidate = GetTypesToValidate();
            if (typesToValidate.Length == 0)
            {
                // No types to validate - just rebuild with empty results
                Rebuild(typesToValidate, Array.Empty<bool>());
                return;
            }

            // Try fast path with cached results first
            var (success, results) =
                GenerationResultsSuperProxyValidations.canAddReferencesToPromptCached((new AddImageReferenceTypeData(this.GetAsset(), typesToValidate),
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

                var validationResults = await GenerationResultsSuperProxyValidations.canAddReferencesToPrompt(
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

        bool AddItemToMenu(ImageReferenceType type, bool enabled, bool active, AssetActionCreator<ImageReferenceActiveData> setImageReferenceIsActive)
        {
            var text = $"{type.GetInternalDisplayNameForType()} Reference";
            if (enabled)
            {
                m_AllowedOperatorsMenu.AppendAction(text, _ => {
                    this.Dispatch(setImageReferenceIsActive, new ImageReferenceActiveData(type, !active));
                    if (!active)
                        this.Dispatch(GenerationSettingsActions.setPendingPing, type.GetImageReferenceName());
                }, _ => DropdownMenuAction.Status.Normal, active);
            }
            else
            {
                m_AllowedOperatorsMenu.AppendAction(text, _ => { }, _ => DropdownMenuAction.Status.Disabled, active);
            }
            return true;
        }

        void Rebuild(ImageReferenceType[] typesToValidate, bool[] results)
        {
            var hasItems = false;
            m_AllowedOperatorsMenu = new DropdownMenu(); // Create new menu

            if (results != null && typesToValidate.Length == results.Length) // Ensure results are valid
            {
                for (var i = 0; i < typesToValidate.Length; i++)
                {
                    var isActive = this.GetState().SelectImageReferenceIsActive(this, typesToValidate[i]);
                    if (AddItemToMenu(typesToValidate[i], results[i], isActive, GenerationSettingsActions.setImageReferenceActive))
                        hasItems = true;
                }
            }

            m_AddToPrompt.SetEnabled(hasItems);
            m_AddToPrompt.tooltip = !hasItems ? "No items available to add." : "Use to guide generation using an existing image as reference.";
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
