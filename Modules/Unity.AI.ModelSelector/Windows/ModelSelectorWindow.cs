using System;
using System.Threading.Tasks;
using Unity.AI.ModelSelector.Components;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.ModelSelector.Windows
{
    class ModelSelectorWindow : EditorWindow
    {
        public static async Task Open(IStore store)
        {
            var window = EditorWindowExtensions.CreateWindow<ModelSelectorWindow>(store, "Select Model", false);
            // if both values are equal, the window will be fully resizable
            window.minSize = new Vector2(950, 832);
            window.maxSize = new Vector2(950, 833);
            window.ShowAuxWindow();

            var tcs = new TaskCompletionSource<bool>();
            window.m_TaskCompletionSource = tcs;

            await tcs.Task;
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
