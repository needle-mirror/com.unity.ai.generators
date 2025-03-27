using System;
using Unity.AI.ModelTrainer.Services.Stores;
using Unity.AI.Generators.Contexts;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.ModelTrainer.Windows
{
    class ModelTrainerWindow : EditorWindow
    {
        [MenuItem("Window/AI/Model Trainer", false, 2)]
        public static void Display()
        {
            var window = GetWindow<ModelTrainerWindow>("Model Trainer");
            window.minSize = new Vector2(640, 480);
            window.maxSize = new Vector2(3840, 2160);
        }

        void CreateGUI()
        {
            rootVisualElement.ProvideContext(ModelTrainerStore.name, ModelTrainerStore.instance);
            rootVisualElement.Add(new Components.ModelTrainer());
        }
    }
}
