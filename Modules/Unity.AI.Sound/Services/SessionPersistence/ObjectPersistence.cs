using System;
using System.Collections.Generic;
using Unity.AI.Generators.Redux.Toolkit;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Sound.Services.SessionPersistence
{
    [Serializable]
    class ObjectPersistence : ScriptableSingleton<ObjectPersistence>
    {
        [SerializeReference]
        SerializableDictionary<string, object> data = new();

        void OnEnable() => data ??= new();

        public T Get<T>(string key) where T: class, new() => data.GetValueOrDefault(key, new T()) as T;
        public void Set(string key, object obj) => data[key] = obj;
    }
}
