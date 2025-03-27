using System.ComponentModel;
using Unity.AI.Generators.Redux;

namespace Unity.AI.Generators.Asset
{
    /// <summary>
    /// Interface for EditorWindows with asset context
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IAssetEditorWindow
    {
        /// <summary>
        /// Initial or current asset context
        /// </summary>
        AssetReference asset { get; set; }

        /// <summary>
        /// Context lock/unlock padlock button
        /// </summary>
        bool isLocked { get; set; }

        /// <summary>
        /// The store
        /// </summary>
        IStore store { get; }
    }
}
