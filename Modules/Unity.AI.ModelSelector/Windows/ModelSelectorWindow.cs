﻿using System;
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
    class ModelSelectorWindow : EditorWindow
    {
        public static async Task Open(IStore store)
        {
            var window = EditorWindowExtensions.CreateWindow<ModelSelectorWindow>(store, "Select AI Model", false);
            // if both values are equal, the window will be fully resizable
            window.minSize = new Vector2(950, 832);
            window.maxSize = new Vector2(950, 833);
            window.ShowAuxWindow();

            var tcs = new TaskCompletionSource<bool>();
            window.m_TaskCompletionSource = tcs;

            await tcs.Task;
        }

        public static async Task<string> Open(VisualElement parent, string selectedModelID, ModalityEnum modality, OperationSubTypeEnum[] operations)
        {
            // model selection is transient and needs to be exchanged with the current modality's slice
            parent.Dispatch(Services.Stores.Actions.ModelSelectorActions.setLastSelectedModelID, selectedModelID);
            parent.Dispatch(Services.Stores.Actions.ModelSelectorActions.setLastSelectedModality, modality);
            parent.Dispatch(Services.Stores.Actions.ModelSelectorActions.setLastOperationSubTypes, operations);
            await Open(parent.GetStore());
            return Services.Stores.Selectors.ModelSelectorSelectors.SelectLastSelectedModelID(parent.GetStore().State);
        }

        TaskCompletionSource<bool> m_TaskCompletionSource;

        void CreateGUI()
        {
            var view = new ModelView();
            view.onDismissRequested += Close;
            rootVisualElement.Add(view);
        }

        void OnDestroy() => m_TaskCompletionSource?.TrySetResult(true);
    }
}
