using System;
using UnityEngine;

namespace Unity.AI.ModelTrainer.Services.Stores.States
{
    [Serializable]
    record UserSetting
    {
        public string name;
        public SettingType type;
        public string value;
    }
}
