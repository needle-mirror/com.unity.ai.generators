using System;
using System.Collections.Generic;
using Unity.AI.Image.Services.Stores.Actions.Creators;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UIElements.Core;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Services.Utilities
{
    static class SplitterExtensions
    {
        public static void Bind(this Splitter splitter,
            VisualElement generatorUI,
            AssetActionCreator<float> setHistoryDrawerHeight,
            Func<IState, VisualElement, float> selectHistoryDrawerHeight,
            Func<IState, VisualElement, IEnumerable<string>> selectActiveReferences)
        {
            var topPane = generatorUI.Q<VisualElement>("top-pane");
            var bottomPane = generatorUI.Q<VisualElement>("bottom-pane");
            var paneContainer = generatorUI.Q<VisualElement>("pane-container");
            Unsubscribe referencesSubscription = null;

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

            generatorUI.UseStore(store =>
            {
                referencesSubscription?.Invoke();
                if (store != null)
                {
                    var references = selectActiveReferences(generatorUI.GetState(), generatorUI);
                    referencesSubscription = generatorUI.UseArray(state => selectActiveReferences(state, generatorUI), _ =>
                    {
                        splitter.Reset();
                    }, new UseSelectorOptions<IEnumerable<string>>
                    {
                        selectImmediately = false,
                        waitForValue = true,
                        initialValue = references
                    });
                }
            });
        }
    }
}
