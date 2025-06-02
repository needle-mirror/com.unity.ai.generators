using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Generators.UI.AIDropdownIntegrations
{
    class GenerativeSubMenu : VisualElement
    {
        internal const string animationMenuItem = "Assets/Create/Animation/Generate Animation Clip";
        internal const string materialMenuItem = "Assets/Create/Rendering/Generate Material";
        internal const string soundMenuItem = "Assets/Create/Audio/Generate Audio Clip";
        internal const string spriteMenuItem = "Assets/Create/Rendering/Generate Sprite"; // use this one, and not the one in 2D/ as it is not guaranteed to exist
        internal const string textureMenuItem = "Assets/Create/Rendering/Generate Texture 2D";

        public GenerativeSubMenu()
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.ai.generators/Modules/Unity.AI.Generators.UI/Components/AIDropdownIntegrations/AIDropdownIntegration.uss"));
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.ai.toolkit/Modules/Accounts/Components/AIDropdownRoot/AIDropdownRoot.uss"));
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.ai.toolkit/Modules/Accounts/Components/AIDropdown/AIDropdown.uss"));

            AddToClassList("sub-menu");
            Add(CreateStandardLabel("Animation", () => EditorApplication.ExecuteMenuItem(animationMenuItem)));
            Add(CreateStandardLabel("Material", () => EditorApplication.ExecuteMenuItem(materialMenuItem)));
            Add(CreateStandardLabel("Sound", () => EditorApplication.ExecuteMenuItem(soundMenuItem)));
            Add(CreateStandardLabel("Sprite", () => EditorApplication.ExecuteMenuItem(spriteMenuItem)));
            Add(CreateStandardLabel("Texture", () => EditorApplication.ExecuteMenuItem(textureMenuItem), false));
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
