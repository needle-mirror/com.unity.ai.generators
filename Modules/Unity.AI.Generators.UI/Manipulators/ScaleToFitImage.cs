using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Generators.UI
{
    class ScaleToFitImage : Manipulator
    {
        Image image => target as Image;
        Texture texture => image.image;

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var aspectRatio = texture ? texture.height / (float) texture.width : 1;
            target.style.width = evt.newRect.height / aspectRatio;
        }
    }
}
