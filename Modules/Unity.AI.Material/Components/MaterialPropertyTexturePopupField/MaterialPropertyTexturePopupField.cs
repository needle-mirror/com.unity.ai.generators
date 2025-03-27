using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Material.Services.Stores.Actions;
using Unity.AI.Material.Services.Stores.Actions.Payloads;
using Unity.AI.Material.Services.Stores.Selectors;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Material.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Material.Components
{
    [UxmlElement]
    partial class MaterialPropertyTexturePopupField : PopupField<string>
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Material/Components/MaterialPropertyTexturePopupField/MaterialPropertyTexturePopupField.uxml";
        const string k_Uss = "Packages/com.unity.ai.generators/modules/Unity.AI.Material/Components/MaterialPropertyTexturePopupField/MaterialPropertyTexturePopupField.uss";

        [UxmlAttribute]
        public string mapType { get; set; }
        
        public MapType mapTypeValue => (MapType)Enum.Parse(typeof(MapType), mapType);
        
        public MaterialPropertyTexturePopupField()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(k_Uss));

            AddToClassList("material-property-texture-popup-field");
            
            this.UseAsset(SetAsset);
            this.UseArray(state => Selectors.SelectGeneratedMaterialMapping(state, this), results => {
                var choice = results.FirstOrDefault(p => p.Key == mapTypeValue);
                if (!choice.Equals(default(KeyValuePair<MapType, string>)))
                    SetValueWithoutNotify(choice.Value);   
            });
            
            RegisterCallback<FocusEvent>(_ => SetAsset(this.GetAsset()));
            this.RegisterValueChangedCallback(evt =>
                this.Dispatch(GenerationResultsActions.setGeneratedMaterialMapping, new GenerationMaterialMappingData(this.GetAsset(), mapTypeValue, evt.newValue)));
        }
        
        void SetAsset(AssetReference asset) => choices = asset.IsValid()
            ? asset.GetObject<UnityEngine.Material>().GetTexturePropertyNames().Prepend(GenerationResult.noneMapping).ToList()
            : new List<string>();
    }
}
