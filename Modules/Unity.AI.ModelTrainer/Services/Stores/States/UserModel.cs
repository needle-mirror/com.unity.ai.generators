using System;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine;

namespace Unity.AI.ModelTrainer.Services.Stores.States
{
    [Serializable]
    record UserModel
    {
        public Texture2D thumbnailTexture => string.IsNullOrEmpty(thumbnail) ? null : null;

        public string id;
        public string name;
        public string type;
        public string thumbnail;
        public DateTime trainingStartDateTime;
        public DateTime trainingEndDateTime;
        public TrainingStatus trainingStatus;
        public int trainingSteps;
        public ImmutableArray<TrainingImageReference> trainingImages = ImmutableArray<TrainingImageReference>.Empty;
        public float learningRate;
        public ImmutableArray<string> tags = ImmutableArray<string>.Empty;
        public string baseModelId;

        public ImmutableArray<UserSetting> settings = ImmutableArray<UserSetting>.Empty;
    }
}
