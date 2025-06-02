using System;
using System.Threading.Tasks;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.ModelSelector.Components;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.ModelSelector.Windows
{
    class ModelSelectorWindow : EditorWindow, IAssetEditorWindow
    {
        TaskCompletionSource<bool> m_TaskCompletionSource;
        ModelView m_View;
        static IStore s_LastStore;

        public static async Task Open(IStore store)
        {
            var window = EditorWindowExtensions.CreateWindow<ModelSelectorWindow>(store, "Select AI Model", false);
            window.store = store;
            window.minSize = new Vector2(950, 832);
            window.maxSize = new Vector2(950, 832);
            window.ShowAuxWindow();

            var tcs = new TaskCompletionSource<bool>();
            window.m_TaskCompletionSource = tcs;

            await tcs.Task;
        }

        public static async Task<string> Open(VisualElement parent, string selectedModelID, ModalityEnum[] modality, OperationSubTypeEnum[] operations)
        {
            // model selection is transient and needs to be exchanged with the current modality's slice
            parent.Dispatch(Services.Stores.Actions.ModelSelectorActions.setLastSelectedModelID, selectedModelID);
            parent.Dispatch(Services.Stores.Actions.ModelSelectorActions.setLastSelectedModalities, modality);
            parent.Dispatch(Services.Stores.Actions.ModelSelectorActions.setLastOperationSubTypes, operations);
            await Open(parent.GetStore());
            return Services.Stores.Selectors.ModelSelectorSelectors.SelectLastSelectedModelID(parent.GetStore().State);
        }

        public static async Task<string> Open(VisualElement parent, string selectedModelID, ModalityEnum modality, OperationSubTypeEnum[] operations) =>
            await Open(parent, selectedModelID, new[] { modality }, operations);

        void CreateGUI()
        {
            this.EnsureContext();
            if (m_View == null)
            {
                m_View = new ModelView();
                m_View.onDismissRequested += Close;
            }
            if (!rootVisualElement.Contains(m_View))
                rootVisualElement.Add(m_View);
        }

        void OnDestroy() => m_TaskCompletionSource?.TrySetResult(true);

        public AssetReference asset
        {
            get => new ();
            set {}
        }

        public bool isLocked
        {
            get => false;
            set {}
        }

        public IStore store
        {
            get => s_LastStore;
            private set => s_LastStore = value;
        }
    }
}
