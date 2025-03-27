using System;
using Unity.AI.Material.Services.Stores.Actions;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Generators.Redux;
using UnityEngine;

namespace Unity.AI.Material.Services.Stores.Selectors
{
    static partial class Selectors
    {
        public static Session SelectSession(this IState state) => state.Get<Session>(SessionActions.slice);
        public static float SelectPreviewSizeFactor(this IState state) => state.SelectSession().settings.previewSettings.sizeFactor;
    }
}
