using System;
using Unity.AI.Material.Services.Stores.Actions.Creators;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Material.Services.Utilities
{
    static class SplitterExtensions
    {
        public static void Bind(this Splitter splitter,
            VisualElement generatorUI,
            AssetActionCreator<float> setHistoryDrawerHeight,
            Func<IState, VisualElement, float> selectHistoryDrawerHeight)
        {
            var topPane = generatorUI.Q<VisualElement>("top-pane");
            var bottomPane = generatorUI.Q<VisualElement>("bottom-pane");
            var paneContainer = generatorUI.Q<VisualElement>("pane-container");

            splitter.topPane = topPane;
            splitter.bottomPane = bottomPane;
            splitter.paneContainer = paneContainer;
            splitter.RegisterValueChangedCallback(evt =>
                generatorUI.Dispatch(setHistoryDrawerHeight, evt.newValue));
            paneContainer.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var height = selectHistoryDrawerHeight(generatorUI.GetState(), generatorUI);
                splitter.SetValueWithoutNotify(height);
            });
        }
    }
}
