using System;
using Unity.AI.Animate.Services.Stores.States;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Animate.Services.SessionPersistence
{
    [FilePath("UserSettings/AI.Animate/AnimateGeneratorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    class AnimateGeneratorSettings : ScriptableSingleton<AnimateGeneratorSettings>
    {
        [SerializeField]
        Session m_Session = new();

        public Session session
        {
            get => m_Session;
            set
            {
                m_Session = value;
                MarkDirty();
            }
        }

        bool m_IsDirty;

        void MarkDirty()
        {
            m_IsDirty = true;
            Debouncer.DebounceAction(debounceKey, SaveSettings);
        }

        void SaveSettings()
        {
            if (!m_IsDirty)
                return;

            m_IsDirty = false;
            Save(true);
        }

        void OnEnable() => EditorApplication.quitting += OnEditorQuitting;

        void OnDisable()
        {
            EditorApplication.quitting -= OnEditorQuitting;
            Debouncer.Cancel(debounceKey);

            if (m_IsDirty)
                SaveSettings();
        }

        void OnEditorQuitting() => SaveSettings();

        string debounceKey => GetType().FullName;
    }
}
