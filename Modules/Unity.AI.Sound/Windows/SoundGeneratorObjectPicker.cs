using Unity.AI.Sound.Services.Utilities;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Sound.Windows
{
    static class SoundGeneratorObjectPicker
    {
        [InitializeOnLoadMethod]
        static void ObjectPickerBlankGenerationHook()
        {
            Toolkit.GenerationObjectPicker.RegisterTemplate<AudioClip>(
                "Assets/AI Toolkit/Templates/!New Audio Asset from Generation....wav",
                AssetUtils.CreateBlankAudioClip,
                "Assets/New Audio Clip.wav",
                SoundGeneratorInspectorButton.OpenGenerationWindow
            );
        }
    }
}
