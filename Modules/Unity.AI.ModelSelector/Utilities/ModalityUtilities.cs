using System;
using AiEditorToolsSdk.Components.Common.Enums;

namespace Unity.AI.ModelSelector.Services.Utilities
{
    static class ModalityUtilities
    {
        public static string GetModalityName(this ModalityEnum modality)
        {
            switch (modality)
            {
                case ModalityEnum.None:
                    break;
                case ModalityEnum.Image:
                    return "Texture 2D";
                case ModalityEnum.Texture2d:
                    return "Material";
                case ModalityEnum.Sound:
                    return "Audio";
                case ModalityEnum.Animate:
                    return "Animation";
            }

            return modality.ToString();
        }
    }
}
