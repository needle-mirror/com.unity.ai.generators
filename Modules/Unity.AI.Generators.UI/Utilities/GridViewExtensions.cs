using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Generators.UI.Utilities
{
    static class GridViewExtensions
    {
        public static void MakeTileGrid(this GridView gridView, Func<float> preferredSize)
        {
            gridView.fixedItemHeight = gridView.fixedItemWidth = Mathf.NextPowerOfTwo((int)TextureSizeHint.Generation);
            var contentContainer = gridView.Q<VisualElement>("unity-content-container");
            if (contentContainer != null)
                contentContainer.RegisterCallback<GeometryChangedEvent>(_ =>
                    TileGridGeometryChanged(new GeometryChangedEvent { target = gridView }, preferredSize()));
            else
                gridView.RegisterCallback<GeometryChangedEvent>(evt => TileGridGeometryChanged(evt, preferredSize()));
        }

        public static void TileSizeChanged(this GridView gridView, float preferredSize) =>
            TileGridGeometryChanged(new GeometryChangedEvent { target = gridView }, preferredSize);

        static void TileGridGeometryChanged(GeometryChangedEvent evt, float preferredSize)
        {
            var gridView = (GridView)evt.target;
            if (gridView.resolvedStyle.display == DisplayStyle.None)
                return;

            var scrollView = gridView.Q<ScrollView>();
            if (scrollView == null)
                return;

            var width = scrollView.contentViewport.layout.width;
            if (float.IsNaN(width))
                return;

            // Decide how many items should fit horizontally based on the preferred size.
            var horizontalItemCount = Mathf.Max(1, Mathf.FloorToInt(width / preferredSize));
            var newFixedSize = Mathf.FloorToInt(width / horizontalItemCount);

            // Only rebuild if the new fixed size differs from the stored value by 1 or more.
            if (Mathf.Abs(gridView.fixedItemWidth - newFixedSize) < 1 && Mathf.Abs(gridView.fixedItemHeight - newFixedSize) < 1)
                return;

            // Store the new value and update the grid.
            gridView.Rebuild(newFixedSize, newFixedSize);
        }

        public static int GetTileGridMaxItemsInElement(this VisualElement element, float preferredSize)
        {
            var width = element.layout.width;
            var height = element.layout.height;
            if (float.IsNaN(width) || float.IsNaN(height))
                return 0;

            // Calculate the number of columns (tiles that fit horizontally)
            var horizontalItemCount = Mathf.Max(1, Mathf.FloorToInt(width / preferredSize));
            // Derive the actual tile size based on the computed column count.
            var tileSize = Mathf.FloorToInt(width / horizontalItemCount);
            // Calculate the number of rows required to cover the viewport.
            // Use CeilToInt to count partially visible rows.
            var verticalItemCount = Mathf.CeilToInt(height / tileSize);

            return horizontalItemCount * verticalItemCount;
        }
    }
}
