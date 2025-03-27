using System;
using System.ComponentModel;

namespace Unity.AI.Generators.Asset
{
    [Serializable, EditorBrowsable(EditorBrowsableState.Never)]
    public record AssetReference
    {
        public string guid = string.Empty;
    }
}
