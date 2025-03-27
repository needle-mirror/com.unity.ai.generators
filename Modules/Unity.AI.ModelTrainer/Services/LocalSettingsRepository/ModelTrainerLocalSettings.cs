using System;
using Unity.AI.ModelTrainer.Services.Stores.States;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.ModelTrainer.Services
{
    [FilePath("ProjectSettings/ModelTrainerSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    class ModelTrainerLocalSettings : ScriptableSingleton<ModelTrainerLocalSettings>
    {
        [SerializeField]
        Session m_Session = new();

        public Session session
        {
            get => m_Session;
            set
            {
                m_Session = value;
                Save(true);
            }
        }
    }
}
