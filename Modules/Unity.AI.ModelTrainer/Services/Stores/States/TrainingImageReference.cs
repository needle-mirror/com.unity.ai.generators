using System;
using UnityEngine;

namespace Unity.AI.ModelTrainer.Services.Stores.States
{
    [Serializable]
    record TrainingImageReference
    {
        public string id;
        public string url;
        public string prompt;

        public Texture2D texture { get; }
    }
}
