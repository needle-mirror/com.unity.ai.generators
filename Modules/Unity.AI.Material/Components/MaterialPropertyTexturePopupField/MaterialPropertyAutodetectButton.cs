using System;
using Unity.AI.Material.Services.Stores.Actions;
using Unity.AI.Material.Services.Utilities;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Material.Components
{
    [UxmlElement]
    partial class MaterialPropertyAutodetectButton : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Material/Components/MaterialPropertyTexturePopupField/MaterialPropertyAutodetectButton.uxml";

        public MaterialPropertyAutodetectButton()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.SafeCloneTree(this);

            AddToClassList("material-property-autodetect");

            var button = this.Q<Button>();
            if (button != null)
                button.clickable = new Clickable(() => this.Dispatch(GenerationResultsActions.autodetectMaterialMapping, this.GetAsset()));
        }
    }
}
