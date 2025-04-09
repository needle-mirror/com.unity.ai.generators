using System;
using Unity.AI.Material.Services.Stores.States;

namespace Unity.AI.Material.Services.Utilities
{
    static class MapTypeUtils
    {
        public static MapType Parse(string mapType)
        {
            if (mapType == null)
                throw new ArgumentNullException(nameof(mapType));

            return mapType.ToLowerInvariant() switch
            {
                "preview" => MapType.Preview,
                "height" => MapType.Height,
                "normal" => MapType.Normal,
                "emission" => MapType.Emission,
                "metallic" => MapType.Metallic,
                "roughness" => MapType.Roughness, // 1P model uses roughness instead of smoothness but with smoothness (1 - v) values
                "delighted" => MapType.Delighted,
                "occlusion" => MapType.Occlusion,
                "smoothness" => MapType.Smoothness,
                "metallicsmoothness" => MapType.MetallicSmoothness,
                "nonmetallicsmoothness" => MapType.NonMetallicSmoothness,
                "maskmap" => MapType.MaskMap,
                _ => throw new ArgumentOutOfRangeException(nameof(mapType), mapType, "Invalid map type")
            };
        }
    }
}
