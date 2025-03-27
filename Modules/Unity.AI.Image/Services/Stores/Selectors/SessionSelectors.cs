using System;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Generators.Redux;
using UnityEngine;
using Session = Unity.AI.Image.Services.Stores.States.Session;

namespace Unity.AI.Image.Services.Stores.Selectors
{
    static partial class Selectors
    {
        public static Session SelectSession(this IState state) => state.Get<Session>(SessionActions.slice);
        public static float SelectPreviewSizeFactor(this IState state) => state.SelectSession().settings.previewSettings.sizeFactor;
    }
}
