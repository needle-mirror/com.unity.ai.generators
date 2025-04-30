using System;
using Unity.AI.Toolkit.Accounts.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using PopupWindow = UnityEditor.PopupWindow;

namespace Unity.AI.Generators.UI.AIDropdownIntegrations
{
    class GenerativeMenuRoot : VisualElement
    {
        const int k_DropdownPadding = 8;
        const int k_MenuSeparation = 8;

        public GenerativeMenuRoot()
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.ai.generators/Modules/Unity.AI.Generators.UI/Components/AIDropdownIntegrations/AIDropdownIntegration.uss"));
            var label = new Label("Generate new");
            AddToClassList("label-button");
            AddToClassList("text-menu-item");
            AddToClassList("text-menu-item-row");

            this.AddManipulator(new Clickable(() =>
            {
                var rect = worldBound;
                rect.x += rect.width + k_DropdownPadding + k_MenuSeparation;
                rect.y -= rect.height;
                PopupWindow.Show(rect, GenerativeSubMenuContent.Content());
            }));

            var chevron = new Image();
            chevron.image = EditorGUIUtility.IconContent("NodeChevronRight").image as Texture2D;
            chevron.AddToClassList("chevron");

            Add(label);
            Add(chevron);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Account.session.OnChange += Refresh;
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                Account.session.OnChange -= Refresh;
            });
        }

        void Refresh()
        {
            EnableInClassList("hide", !Account.settings.AiGeneratorsEnabled);
        }
    }
}
