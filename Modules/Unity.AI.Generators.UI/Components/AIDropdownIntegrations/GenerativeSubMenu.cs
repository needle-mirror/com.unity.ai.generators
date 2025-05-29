using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Generators.UI.AIDropdownIntegrations
{
    class GenerativeSubMenu : VisualElement
    {
        public GenerativeSubMenu()
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.ai.generators/Modules/Unity.AI.Generators.UI/Components/AIDropdownIntegrations/AIDropdownIntegration.uss"));
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.ai.toolkit/Modules/Accounts/Components/AIDropdownRoot/AIDropdownRoot.uss"));
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.ai.toolkit/Modules/Accounts/Components/AIDropdown/AIDropdown.uss"));

            AddToClassList("sub-menu");
            Add(CreateStandardLabel("Animation", () => EditorApplication.ExecuteMenuItem("Assets/Create/Animation/Generate Animation Clip")));
            Add(CreateStandardLabel("Material", () => EditorApplication.ExecuteMenuItem("Assets/Create/Rendering/Generate Material")));
            Add(CreateStandardLabel("Sound", () => EditorApplication.ExecuteMenuItem("Assets/Create/Audio/Generate Audio Clip")));
            Add(CreateStandardLabel("Sprite", () => EditorApplication.ExecuteMenuItem("Assets/Create/Rendering/Generate Sprite"))); // use this one, and not the one in 2D/ as it is not guaranteed to exist
            Add(CreateStandardLabel("Texture", () => EditorApplication.ExecuteMenuItem("Assets/Create/Rendering/Generate Texture 2D"), false));
        }

        static Label CreateStandardLabel(string text, Action onClick, bool bottomMargin = true)
        {
            var label = new Label(text);
            label.AddToClassList("text-menu-item");
            label.AddToClassList("label-button");
            label.EnableInClassList("dropdown-item-with-margin", bottomMargin);
            label.AddManipulator(new Clickable(onClick));
            return label;
        }
    }
}
